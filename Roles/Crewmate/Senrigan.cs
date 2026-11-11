using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Crewmate;

/// <summary>
/// 千里眼<br/>
/// ・クールダウンが明けた瞬間の全員の居場所を、誰かは出さずに北から順に、自分の名前の下に出す(本人の画面だけ)<br/>
/// ・シェイプシフトの画面で誰かを選ぶと表示が消えて、もう一度クールダウン(変身はしない)<br/>
/// ・次にクールダウンが明けたら、その瞬間の居場所に更新される
/// </summary>
public sealed class Senrigan : RoleBase, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Senrigan),
            player => new Senrigan(player),
            CustomRoles.Senrigan,
            () => RoleTypes.Shapeshifter,
            CustomRoleTypes.Crewmate,
            11300,
            SetupOptionItem,
            "sen",
            "#8f7cff",
            (6, 4),
            true,
            introSound: () => GetIntroSound(RoleTypes.Tracker),
            from: From.TownOfHost_IntheMaking
        );
    public Senrigan(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        timer = Cooldown.GetFloat();
        usedCount = 0;
        snapshot = null;
    }

    static OptionItem Cooldown;
    static OptionItem UseLimit;
    static void SetupOptionItem()
    {
        Cooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, new(0f, 180f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        UseLimit = IntegerOptionItem.Create(RoleInfo, 11, GeneralOption.OptionCount, new(0, 99, 1), 0, false)
            .SetValueFormat(OptionFormat.Times).SetZeroNotation(OptionZeroNotation.Infinity);
    }

    /// <summary>クールダウンの残り(ホストで数える)</summary>
    float timer;
    int usedCount;
    /// <summary>クールダウンが明けた瞬間の居場所(北から順、誰かは出さない)。null なら見えていない</summary>
    string snapshot;

    bool HasUseLeft => UseLimit.GetInt() == 0 || usedCount < UseLimit.GetInt();

    // ---- キル・サボ・ベントは使えない(インポスター視点のボタンを消す) ----
    public bool CanKill => false;
    public bool CanUseKillButton() => false;
    float IKiller.CalculateKillCooldown() => 0f;
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;
    public override bool OnInvokeSabotage(SystemTypes systemType) => false;

    public override bool CanUseAbilityButton() => HasUseLeft;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.ShapeshifterCooldown = HasUseLeft ? Cooldown.GetFloat() : 255f;
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    /// <summary>タスクターンに出てきたとき(この後クールダウンがリセットされる)</summary>
    public override void OnSpawn(bool initialState = false)
    {
        timer = Cooldown.GetFloat();
        snapshot = null;
    }
    public override void OnStartMeeting()
    {
        snapshot = null;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || !Player.IsAlive()) return;
        if (snapshot != null || !HasUseLeft) return;
        timer -= Time.fixedDeltaTime;
        if (timer > 0) return;
        TakeSnapshot();
    }

    void TakeSnapshot()
    {
        //北(上)にいる人から順に、場所だけを並べる。同じ場所が続いたら「×人数」にまとめる
        var rooms = new List<(string room, int count)>();
        foreach (var pc in PlayerCatch.AllAlivePlayerControls
            .Where(pc => pc != null && pc.PlayerId != Player.PlayerId)
            .OrderByDescending(pc => pc.transform.position.y))
        {
            var room = pc.GetShipRoomName();
            if (room == "") room = "？";
            if (rooms.Count > 0 && rooms[^1].room == room) rooms[^1] = (room, rooms[^1].count + 1);
            else rooms.Add((room, 1));
        }
        var parts = rooms.Select(r => r.count > 1 ? $"{r.room}×{r.count}" : r.room).ToList();
        //4つごとに改行
        var lines = new List<string>();
        for (var i = 0; i < parts.Count; i += 4) lines.Add(string.Join("、", parts.Skip(i).Take(4)));
        snapshot = lines.Count == 0 ? "（だれもいない）" : string.Join("\n", lines);
        Logger.Info($"{Player.Data.GetLogPlayerName()} の千里眼(北から): {string.Join(" / ", parts)}", "Senrigan");
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }

    public override bool CheckShapeshift(PlayerControl target, ref bool animate)
    {
        animate = false;
        if (!HasUseLeft) return false;
        usedCount++;
        snapshot = null;
        timer = Cooldown.GetFloat();
        //変身の取り消しが届いたあとでクールダウンを始め直す
        Player.RpcResetAbilityCooldown(Sync: true);
        if (!HasUseLeft) Player.MarkDirtySettings();//使い切ったらボタンを使えなくする
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
        return false;//変身はしない
    }

    /// <summary>自分の名前の下に、北から順の場所を出す(自分の画面だけ)</summary>
    public override string GetSuffix(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        if (isForMeeting || (seen != null && seen != seer) || !Is(seer) || snapshot == null) return "";
        return $"<size=60%>{Utils.ColorString(RoleInfo.RoleColor, $"北から：{snapshot}")}</size>";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
        => UseLimit.GetInt() == 0 ? "" : Utils.ColorString(HasUseLeft ? RoleInfo.RoleColor : Color.gray, $"({UseLimit.GetInt() - usedCount})");

    public override string GetAbilityButtonText() => GetString("SenriganAbility");
}

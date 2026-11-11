using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Crewmate;

/// <summary>
/// 千里眼<br/>
/// ・クールダウンが明けた瞬間の全員の居場所(部屋)が、本人の画面の名前の横に出る<br/>
///   (シェイプシフトの画面を開くと、全員の居場所の一覧として見られる)<br/>
/// ・シェイプシフトの画面で誰かを選ぶと居場所が消えて、もう一度クールダウン(変身はしない)<br/>
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
    /// <summary>クールダウンが明けた瞬間の居場所。null なら見えていない</summary>
    Dictionary<byte, string> snapshot;

    bool HasUseLeft => UseLimit.GetInt() == 0 || usedCount < UseLimit.GetInt();

    // ---- キル・サボ・ベントは使えない(インポスター視点のボタンを消す) ----
    public bool CanKill => false;
    public bool CanUseKillButton() => false;
    float IKiller.CalculateKillCooldown() => 0f;
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;
    public override bool OnInvokeSabotage(SystemTypes systemType) => false;

    public override bool CanUseAbilityButton() => HasUseLeft;
    public override bool NotifyRolesCheckOtherName => true;

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
        snapshot = new();
        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            if (pc == null || pc.PlayerId == Player.PlayerId) continue;
            var room = pc.GetShipRoomName();
            snapshot[pc.PlayerId] = room == "" ? "？" : room;
        }
        Logger.Info($"{Player.Data.GetLogPlayerName()} の千里眼: {string.Join(", ", snapshot)}", "Senrigan");
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }

    public override bool CheckShapeshift(PlayerControl target, ref bool animate)
    {
        animate = false;
        if (!HasUseLeft) return false;
        usedCount++;
        snapshot = null;
        timer = Cooldown.GetFloat();
        Player.RpcResetAbilityCooldown();
        if (!HasUseLeft) Player.MarkDirtySettings();//使い切ったらボタンを使えなくする
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
        return false;//変身はしない
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        if (isForMeeting || seen == null || seen == seer || !Is(seer)) return "";
        if (snapshot == null || !snapshot.TryGetValue(seen.PlayerId, out var room)) return "";
        return $"<size=70%>{Utils.ColorString(RoleInfo.RoleColor, $"({room})")}</size>";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
        => UseLimit.GetInt() == 0 ? "" : Utils.ColorString(HasUseLeft ? RoleInfo.RoleColor : Color.gray, $"({UseLimit.GetInt() - usedCount})");

    public override string GetAbilityButtonText() => GetString("SenriganAbility");
}

using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Crewmate;

/// <summary>
/// 千里眼(エンジニア置き換え・タスクあり)<br/>
/// ・ベントから出ると、その瞬間の全員の居場所を、誰かは出さずに北から順に1つずつ画面の下に出す<br/>
///   (1か所 1秒 → 切替中 0.2秒 → 次の場所 …)。本人の画面だけ<br/>
/// ・ベントのクールダウンは「人数×1.1秒」より短くできない(1周を見終わる前に次が来ないように)
/// </summary>
public sealed class Senrigan : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Senrigan),
            player => new Senrigan(player),
            CustomRoles.Senrigan,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            11300,
            SetupOptionItem,
            "sen",
            "#8f7cff",
            (6, 4),
            introSound: () => GetIntroSound(RoleTypes.Engineer),
            from: From.TownOfHost_IntheMaking
        );
    public Senrigan(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        usedCount = 0;
        rooms = null;
    }

    static SenriganCooldownOptionItem Cooldown;
    static OptionItem UseLimit;
    static OptionItem BlockedByComms;
    enum OptionName
    {
        SenriganBlockedByComms
    }
    static void SetupOptionItem()
    {
        Cooldown = SenriganCooldownOptionItem.Create(RoleInfo, 10);
        UseLimit = IntegerOptionItem.Create(RoleInfo, 11, GeneralOption.OptionCount, new(0, 99, 1), 0, false)
            .SetValueFormat(OptionFormat.Times).SetZeroNotation(OptionZeroNotation.Infinity);
        BlockedByComms = BooleanOptionItem.Create(RoleInfo, 12, OptionName.SenriganBlockedByComms, true, false);
    }
    /// <summary>通信障害で居場所が分からない状態か</summary>
    static bool IsBlocked => BlockedByComms.GetBool() && Utils.IsActive(SystemTypes.Comms);

    /// <summary>1か所を出す秒数</summary>
    const float ShowTime = 1f;
    /// <summary>切り替えの秒数</summary>
    const float GapTime = 0.2f;

    int usedCount;
    /// <summary>ベントから出た瞬間の居場所(北から順)。null なら表示していない</summary>
    List<string> rooms;
    /// <summary>今出している番号</summary>
    int index;
    /// <summary>今の表示(場所 or 切替中)の残り秒数</summary>
    float timer;
    /// <summary>切替中か</summary>
    bool inGap;

    bool HasUseLeft => UseLimit.GetInt() == 0 || usedCount < UseLimit.GetInt();

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = Cooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    /// <summary>ベントから出たとき(ホストで呼ばれる)</summary>
    public void OnExitVent()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive() || !GameStates.IsInTask) return;
        if (rooms != null) return;//表示中(同じベントから出たのが2回届いたときなど)
        if (!HasUseLeft) return;
        if (IsBlocked) return;//通信障害中は分からない(回数も使わない)

        var list = PlayerCatch.AllAlivePlayerControls
            .Where(pc => pc != null && pc.PlayerId != Player.PlayerId)
            .OrderByDescending(pc => pc.transform.position.y)
            .Select(pc => { var r = pc.GetShipRoomName(); return r == "" ? "？" : r; })
            .ToList();
        Logger.Info($"{Player.Data.GetLogPlayerName()} の千里眼(北から): {string.Join(" / ", list)}", "Senrigan");
        if (list.Count == 0) return;
        usedCount++;
        rooms = list;
        index = 0;
        inGap = false;
        timer = ShowTime;
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || rooms == null) return;
        if (!Player.IsAlive() || !GameStates.IsInTask || IsBlocked)
        {
            Stop();
            return;
        }
        timer -= Time.fixedDeltaTime;
        if (timer > 0) return;
        if (inGap)
        {
            //次の場所へ
            inGap = false;
            index++;
            timer = ShowTime;
        }
        else if (index + 1 >= rooms.Count)
        {
            //最後まで出したら終わり
            Stop();
            return;
        }
        else
        {
            inGap = true;
            timer = GapTime;
        }
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }
    void Stop()
    {
        if (rooms == null) return;
        rooms = null;
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }
    public override void OnStartMeeting()
    {
        rooms = null;
    }

    /// <summary>画面の下(MODなしの人は名前の下)に出す。本人の画面だけ</summary>
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        if (isForMeeting || (seen != null && seen != seer) || !Is(seer) || rooms == null || !Player.IsAlive()) return "";
        var text = inGap ? "切替中" : rooms[index];
        return Utils.ColorString(RoleInfo.RoleColor, $"千里眼 {index + 1}/{rooms.Count}：{text}");
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
        => UseLimit.GetInt() == 0 ? "" : Utils.ColorString(HasUseLeft ? RoleInfo.RoleColor : Color.gray, $"({UseLimit.GetInt() - usedCount})");

    /// <summary>クールダウンの最小値(人数×1.1秒)。人数は今いるプレイヤー全員</summary>
    public static float MinCooldown()
    {
        var count = PlayerCatch.AllPlayerControls.Count();
        return Math.Min(180f, (float)(count * 11 / 10m));
    }
}

/// <summary>
/// 千里眼のクールダウン設定<br/>
/// ・「最小値」(人数×1.1秒)、0.1～19.9秒(0.1秒刻み)、20～180秒(0.5秒刻み)<br/>
/// ・最小値以下の値は飛ばして「最小値」にする(下げていくと 14.4 → 最小値 → 180)<br/>
/// ・設定した後に人が増えて最小値を下回ったときも「最小値」として扱う
/// </summary>
public class SenriganCooldownOptionItem : OptionItem
{
    /// <summary>0.1～19.9 の数</summary>
    const int FineCount = 199;
    /// <summary>一番大きい番号(180秒)</summary>
    const int MaxIndex = FineCount + 321;

    public SenriganCooldownOptionItem(int id, string name, TabGroup tab)
    : base(id, name, 0, tab, false) { }

    public static SenriganCooldownOptionItem Create(SimpleRoleInfo roleInfo, int idOffset)
    {
        var opt = new SenriganCooldownOptionItem(roleInfo.ConfigId + idOffset, "Cooldown", roleInfo.Tab);
        opt.SetValueFormat(OptionFormat.Seconds);
        opt.SetParent(roleInfo.RoleOption);
        opt.SetParentRole(roleInfo.RoleName);
        return opt;
    }

    /// <summary>番号 → 秒数(0番は最小値なので 0 を返す)</summary>
    static float ValueOf(int index)
    {
        if (index <= 0) return 0f;
        if (index <= FineCount) return (float)(index / 10m);
        return (float)(20m + (index - FineCount - 1) * 0.5m);
    }
    /// <summary>最小値より大きい一番小さい番号</summary>
    static int FirstIndexAbove(float min)
    {
        for (var i = 1; i <= MaxIndex; i++)
            if (ValueOf(i) > min + 0.001f) return i;
        return MaxIndex;
    }
    static bool IsMin(int index) => index <= 0 || ValueOf(index) <= Senrigan.MinCooldown() + 0.001f;

    public override int GetValue() => Math.Clamp(base.GetValue(), 0, MaxIndex);
    public override float GetFloat()
    {
        var index = GetValue();
        return IsMin(index) ? Senrigan.MinCooldown() : ValueOf(index);
    }
    public override int GetInt() => (int)GetFloat();
    public override string GetString()
    {
        var index = GetValue();
        return IsMin(index) ? $"最小値({ApplyFormat(Senrigan.MinCooldown().ToString())})" : ApplyFormat(ValueOf(index).ToString());
    }
    public override string GetValueString(bool coloroff) => GetString();

    public override void SetValue(int afterValue, bool doSync = true)
    {
        var before = GetValue();
        var min = Senrigan.MinCooldown();
        int next;
        if (afterValue > MaxIndex) next = 0;//180の次は最小値
        else if (afterValue < 0) next = MaxIndex;//最小値から下げると180
        else if (afterValue == 0 || ValueOf(afterValue) > min + 0.001f) next = afterValue;
        else next = afterValue < before ? 0 : FirstIndexAbove(min);//最小値以下は飛ばす
        base.SetValue(next, doSync);
    }
}

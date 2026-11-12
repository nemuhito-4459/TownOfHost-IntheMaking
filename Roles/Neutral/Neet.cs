using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Neutral;

/// <summary>
/// ニート(ニュートラル・クルーメイト置き換え・カウントはクルー)。リクルーターとのコンビ役職<br/>
/// ・タスクがない。設定で、サボタージュも直せない<br/>
/// ・リクルーター以外の全員から、ニートだと分かる<br/>
/// ・リクルーターに勧誘されると、ブラック企業かホワイト企業に就職して、ウォークタスク(ショート)とサボタージュの修理ができるようになる<br/>
/// ・勝利：就職していなければ生きていれば追加勝利。就職したら追加勝利(生死は問わない)<br/>
/// ・ブラック企業でタスクを終えると(生きていれば)、ニートとリクルーターの単独勝利
/// </summary>
public sealed class Neet : RoleBase, IAdditionalWinner, ISystemTypeUpdateHook
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Neet),
            player => new Neet(player),
            CustomRoles.Neet,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            14900,
            null,
            "neet",
            "#a0a0a0",
            (2, 3),
            tab: TabGroup.Combinations,
            countType: CountTypes.Crew,
            combination: CombinationRoles.RecruiterandNeet,
            from: From.TownOfHost_IntheMaking
        );
    public Neet(PlayerControl player)
    : base(
        RoleInfo,
        player,
        //就職したらタスクを持つ(クルーのタスク勝利には数えない)
        () => HiredIds.Contains(player.PlayerId) ? HasTask.ForRecompute : HasTask.False
    )
    {
        HiredIds.Remove(player.PlayerId);
        Hired = false;
        IsBlack = false;
        HiredBy = byte.MaxValue;
        notice = "";
        noticeTimer = 0;
    }
    /// <summary>就職したニートのId</summary>
    static readonly HashSet<byte> HiredIds = new();

    public bool Hired { get; private set; }
    public bool IsBlack { get; private set; }
    public byte HiredBy { get; private set; }
    string notice;
    float noticeTimer;

    /// <summary>そのリクルーターが勧誘したニート</summary>
    public static Neet GetHiredBy(byte recruiterId)
        => CustomRoleManager.AllActiveRoles.Values.OfType<Neet>().FirstOrDefault(n => n.Hired && n.HiredBy == recruiterId);

    /// <summary>リクルーターに勧誘された(ホスト)</summary>
    public void Hire(PlayerControl recruiter)
    {
        Hired = true;
        HiredBy = recruiter.PlayerId;
        HiredIds.Add(Player.PlayerId);
        //ブラック企業かホワイト企業か(それぞれの確率を重みにして選ぶ)
        var black = Recruiter.OptionNeetBlackChance.GetInt();
        var white = Recruiter.OptionNeetWhiteChance.GetInt();
        IsBlack = black + white > 0 && IRandom.Instance.Next(black + white) < black;
        var count = IsBlack ? Recruiter.OptionNeetBlackTasks.GetInt() : Recruiter.OptionNeetWhiteTasks.GetInt();
        RoomTaskSettings.AssignMidGame(Player, RoomTaskKind.Short, count);

        notice = IsBlack ? "ブラック企業に就職してしまった…" : "ホワイト企業に就職した！";
        noticeTimer = 5f;
        Logger.Info($"{Player.Data.GetLogPlayerName()} が {(IsBlack ? "ブラック" : "ホワイト")}企業に就職 (タスク{count}個)", "Neet");
        UtilsGameLog.AddGameLog("Neet", $"{UtilsName.GetPlayerColor(Player, true)} が{(IsBlack ? "ブラック" : "ホワイト")}企業に就職");
        RPC.PlaySoundRPC(Player.PlayerId, Sounds.TaskComplete);
        UtilsNotifyRoles.NotifyRoles();
    }

    /// <summary>ウォークタスクを1つ終えたとき(ホスト)。ブラック企業でタスクを全部終えたら単独勝利</summary>
    public void OnWalkTaskComplete()
    {
        if (!AmongUsClient.Instance.AmHost || !Hired || !IsBlack || !IsTaskFinished || !Player.IsAlive()) return;
        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Neet, Player.PlayerId, true))
        {
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
            //勧誘したリクルーターも一緒に勝つ
            if (HiredBy != byte.MaxValue)
            {
                CustomWinnerHolder.WinnerIds.Add(HiredBy);
                CustomWinnerHolder.NeutralWinnerIds.Add(HiredBy);
            }
        }
    }

    /// <summary>就職していなければ生きていれば、就職したら生死を問わず追加勝利</summary>
    public bool CheckWin(ref CustomRoles winnerRole) => Hired || Player.IsAlive();

    //リクルーター以外は、ニートだと分かる
    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seer, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        if (seer == null || seer.Is(CustomRoles.Recruiter)) return;
        enabled = true;
    }

    //就職するまで(設定でオフなら)サボタージュを直せない
    bool CanFix => Hired || Recruiter.OptionNeetCanFixSabotage.GetBool();
    bool ISystemTypeUpdateHook.UpdateReactorSystem(ReactorSystemType reactorSystem, byte amount) => CanFix;
    bool ISystemTypeUpdateHook.UpdateHeliSabotageSystem(HeliSabotageSystem heliSabotageSystem, byte amount) => CanFix;
    bool ISystemTypeUpdateHook.UpdateLifeSuppSystem(LifeSuppSystemType lifeSuppSystem, byte amount) => CanFix;
    bool ISystemTypeUpdateHook.UpdateHqHudSystem(HqHudSystemType hqHudSystemType, byte amount) => CanFix;
    bool ISystemTypeUpdateHook.UpdateSwitchSystem(SwitchSystem switchSystem, byte amount) => CanFix;
    bool ISystemTypeUpdateHook.UpdateHudOverrideSystem(HudOverrideSystemType hudOverrideSystem, byte amount) => CanFix;

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || noticeTimer <= 0) return;
        noticeTimer -= Time.fixedDeltaTime;
        if (noticeTimer > 0) return;
        notice = "";
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }
    public override void OnStartMeeting()
    {
        notice = "";
        noticeTimer = 0;
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        if (isForMeeting || (seen != null && seen != seer) || !Is(seer) || notice == "") return "";
        return Utils.ColorString(RoleInfo.RoleColor, notice);
    }
    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Hired) return Utils.ColorString(Color.gray, "(無職)");
        return IsBlack ? Utils.ColorString(new Color32(90, 90, 90, 255), "(ブラック企業)") : Utils.ColorString(Color.white, "(ホワイト企業)");
    }
}

using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Neutral;

/// <summary>
/// リクルーター(ニュートラル・インポスター置き換え・カウントはクルー)。ニートとのコンビ役職<br/>
/// ・キルボタンで「リクエスト」(勧誘)。相手がニートなら設定の確率で成功、ニート以外なら失敗。キルはできない<br/>
/// ・リクルーターだけは、どれがニートか分からない<br/>
/// ・回数の上限まで使い切って成功しなかったら、勝てなくなる<br/>
/// ・勧誘に成功したら、ニートと一緒に追加勝利(ブラック企業ならそのまま、ホワイト企業ならニートがタスクを終えていれば)<br/>
/// ・ブラック企業に就職したニートがタスクを終えると、ニートとリクルーターの単独勝利
/// </summary>
public sealed class Recruiter : RoleBase, IKiller, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Recruiter),
            player => new Recruiter(player),
            CustomRoles.Recruiter,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            14800,
            SetupOptionItem,
            "rcr",
            "#e0a030",
            (2, 2),
            true,
            tab: TabGroup.Combinations,
            introSound: () => GetIntroSound(RoleTypes.Crewmate),
            countType: CountTypes.Crew,
            assignInfo: new RoleAssignInfo(CustomRoles.Recruiter, CustomRoleTypes.Neutral)
            {
                AssignUnitRoles = [CustomRoles.Recruiter, CustomRoles.Neet]
            },
            combination: CombinationRoles.RecruiterandNeet,
            from: From.TownOfHost_IntheMaking
        );
    public Recruiter(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        usedCount = 0;
        succeeded = false;
        inherited = false;
        coexist = false;
        notice = "";
        noticeTimer = 0;
    }

    static OptionItem OptionCooldown;
    static OptionItem OptionSuccessChance;
    static OptionItem OptionNotifyNotNeet;
    static OptionItem OptionHasLimit;
    static OptionItem OptionLimit;
    //ニートの設定(リクルーターの設定の中で作って、ニートのページへ移す)
    public static OptionItem OptionNeetBlackChance;
    public static OptionItem OptionNeetWhiteChance;
    public static OptionItem OptionNeetBlackTasks;
    public static OptionItem OptionNeetWhiteTasks;
    public static OptionItem OptionNeetCanFixSabotage;
    public static OptionItem OptionNeetPunishTasks;
    public static OptionItem OptionNeetPunishKillQuota;
    /// <summary>ニートの設定(リクルーターの設定の中で作っているもの)</summary>
    public static List<OptionItem> NeetOptions = new();
    enum OptionName
    {
        RecruiterCooldown,
        RecruiterSuccessChance,
        RecruiterNotifyNotNeet,
        RecruiterHasLimit,
        RecruiterLimit,
        NeetBlackChance,
        NeetWhiteChance,
        NeetBlackTasks,
        NeetWhiteTasks,
        NeetCanFixSabotage,
        NeetPunishTasks,
        NeetPunishKillQuota,
    }
    static void SetupOptionItem()
    {
        //ブラック企業のニートがタスクを終えたときの単独勝利の優先順位
        SoloWinOption.Create(RoleInfo, 9, CustomRoles.Neet, defo: 1);
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.RecruiterCooldown, new(5f, 40f, 1f), 25f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionSuccessChance = IntegerOptionItem.Create(RoleInfo, 11, OptionName.RecruiterSuccessChance, new(10, 100, 1), 50, false)
            .SetValueFormat(OptionFormat.Percent);
        OptionNotifyNotNeet = BooleanOptionItem.Create(RoleInfo, 12, OptionName.RecruiterNotifyNotNeet, false, false);
        OptionHasLimit = BooleanOptionItem.Create(RoleInfo, 13, OptionName.RecruiterHasLimit, true, false);
        OptionLimit = IntegerOptionItem.Create(RoleInfo, 14, OptionName.RecruiterLimit, new(1, 10, 1), 3, false, OptionHasLimit)
            .SetValueFormat(OptionFormat.Times);

        //ここから下はニートの設定(CombinationOptionsでニートのページへ移す)
        var neetStart = OptionItem.AllOptions.Count;
        ObjectOptionitem.Create(RoleInfo, 19, "RecruiterNeet", true, null).SetOptionName(() => "Neet Setting").SetColor(Neet.RoleInfo.RoleColor);
        OptionNeetBlackChance = IntegerOptionItem.Create(RoleInfo, 20, OptionName.NeetBlackChance, new(10, 100, 5), 20, false)
            .SetValueFormat(OptionFormat.Percent);
        OptionNeetWhiteChance = IntegerOptionItem.Create(RoleInfo, 21, OptionName.NeetWhiteChance, new(5, 100, 5), 80, false)
            .SetValueFormat(OptionFormat.Percent);
        OptionNeetBlackTasks = IntegerOptionItem.Create(RoleInfo, 22, OptionName.NeetBlackTasks, new(1, 50, 1), 5, false)
            .SetValueFormat(OptionFormat.Pieces);
        OptionNeetWhiteTasks = IntegerOptionItem.Create(RoleInfo, 23, OptionName.NeetWhiteTasks, new(2, 10, 1), 2, false)
            .SetValueFormat(OptionFormat.Pieces);
        OptionNeetCanFixSabotage = BooleanOptionItem.Create(RoleInfo, 24, OptionName.NeetCanFixSabotage, false, false);
        OptionNeetPunishTasks = IntegerOptionItem.Create(RoleInfo, 25, OptionName.NeetPunishTasks, new(1, 10, 1), 2, false)
            .SetValueFormat(OptionFormat.Pieces);
        OptionNeetPunishKillQuota = IntegerOptionItem.Create(RoleInfo, 26, OptionName.NeetPunishKillQuota, new(0, 5, 1), 1, false)
            .SetValueFormat(OptionFormat.Players);
        NeetOptions = OptionItem.AllOptions.Skip(neetStart).ToList();
    }

    int usedCount;
    /// <summary>ニートの勧誘に成功したか</summary>
    bool succeeded;
    /// <summary>死んだニートのタスクを引き継いだか</summary>
    bool inherited;
    /// <summary>引き継いだタスクを終えて、共存勝利が確定したか</summary>
    bool coexist;
    /// <summary>画面の下に少しのあいだ出す知らせ</summary>
    string notice;
    float noticeTimer;

    bool HasUseLeft => !OptionHasLimit.GetBool() || usedCount < OptionLimit.GetInt();

    //キルはできない(キルボタンはリクエスト)
    public bool CanKill => false;
    public bool CanUseKillButton() => Player.IsAlive() && !succeeded && HasUseLeft;
    public float CalculateKillCooldown() => OptionCooldown.GetFloat();
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;
    public bool OverrideKillButtonText(out string text)
    {
        text = "リクエスト";
        return true;
    }
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(false);

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        info.DoKill = false;
        if (!AmongUsClient.Instance.AmHost) return;
        var (_, target) = info.AttemptTuple;
        if (!CanUseKillButton()) return;
        usedCount++;

        if (target.GetRoleClass() is Neet neet && !neet.Hired)
        {
            if (IRandom.Instance.Next(100) < OptionSuccessChance.GetInt())
            {
                succeeded = true;
                neet.Hire(Player);
                ShowNotice("勧誘に成功した！");
            }
            else ShowNotice("勧誘を断られた…");
        }
        else if (OptionNotifyNotNeet.GetBool()) ShowNotice("相手はニートではなかった");

        if (!succeeded && !HasUseLeft) ShowNotice("もう勧誘できない…");
        Logger.Info($"{Player.Data.GetLogPlayerName()} が {target.Data.GetLogPlayerName()} にリクエスト (成功:{succeeded} 残り:{(OptionHasLimit.GetBool() ? OptionLimit.GetInt() - usedCount : -1)})", "Recruiter");
        Player.SetKillCooldown();
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }

    void ShowNotice(string text)
    {
        notice = text;
        noticeTimer = 5f;
    }
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

    /// <summary>勧誘に成功した時点で追加勝利が確定(生死は問わない)</summary>
    public bool CheckWin(ref CustomRoles winnerRole) => succeeded;

    /// <summary>死んだニートの残りのタスクを引き継いだ(ホスト)</summary>
    public void Inherit(int count)
    {
        inherited = true;
        ShowNotice($"ニートが死んだ…残りのタスク{count}個を引き継いだ");
    }
    /// <summary>ウォークタスクを1つ終えたとき(ホスト)。引き継いだタスクを全部終えたら共存勝利が確定</summary>
    public void OnWalkTaskComplete(bool allDone)
    {
        if (!AmongUsClient.Instance.AmHost || !inherited || coexist || !allDone) return;
        coexist = true;
        ShowNotice("引き継いだタスクを終えた！共存勝利が確定");
        Logger.Info($"{Player.Data.GetLogPlayerName()} が引き継いだタスクを終えた(共存勝利が確定)", "Recruiter");
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        if (isForMeeting || (seen != null && seen != seer) || !Is(seer) || notice == "") return "";
        return Utils.ColorString(RoleInfo.RoleColor, notice);
    }
    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (coexist) return Utils.ColorString(RoleInfo.RoleColor, "(共存確定)");
        if (inherited) return Utils.ColorString(RoleInfo.RoleColor, "(タスク引き継ぎ中)");
        if (succeeded) return Utils.ColorString(RoleInfo.RoleColor, "(勧誘済み)");
        if (!OptionHasLimit.GetBool()) return "";
        var left = OptionLimit.GetInt() - usedCount;
        return Utils.ColorString(left > 0 ? RoleInfo.RoleColor : Color.gray, $"({left})");
    }
}

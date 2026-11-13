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
        deathHandled = false;
        KillQuota = 0;
        KillBase = 0;
    }
    /// <summary>就職したニートのId</summary>
    static readonly HashSet<byte> HiredIds = new();

    public bool Hired { get; private set; }
    public bool IsBlack { get; private set; }
    public byte HiredBy { get; private set; }
    string notice;
    float noticeTimer;
    /// <summary>死んだときの処理(リクルーターへの引き継ぎ)を済ませたか</summary>
    bool deathHandled;

    /// <summary>ブラック企業のニートが追放されたときの、インポスターのキルのノルマ(0ならなし)</summary>
    static int KillQuota;
    /// <summary>ノルマが付いた時点で、インポスターがキルしていた数</summary>
    static int KillBase;
    /// <summary>インポスターがキルした数(自殺は数えない)</summary>
    static int CountImpostorKills()
        => PlayerState.AllPlayerStates.Count(kv => kv.Value.IsDead && kv.Value.GetRealKiller() is var k && k != kv.Key && k != byte.MaxValue
            && (PlayerState.GetByPlayerId(k)?.MainRole.IsImpostor() ?? false));
    /// <summary>インポスターのキルのノルマの残り</summary>
    public static int KillQuotaLeft => KillQuota <= 0 || !CustomRoleManager.AllActiveRoles.Values.Any(r => r is Neet) ? 0 : System.Math.Max(0, KillQuota - (CountImpostorKills() - KillBase));
    /// <summary>キルのノルマが残っていて、インポスターが勝てないか</summary>
    public static bool BlockImpostorWin => KillQuotaLeft > 0;

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

    /// <summary>
    /// 試合が終わるとき(ホスト)。ホワイト企業のニートがタスクを終えて生きていたら、勝ちを乗っ取る(ニートとリクルーターだけの勝利)
    /// </summary>
    public static void CheckTakeover()
    {
        if (CustomWinnerHolder.WinnerTeam is CustomWinner.Default or CustomWinner.Draw or CustomWinner.None or CustomWinner.Neet) return;
        var neet = CustomRoleManager.AllActiveRoles.Values.OfType<Neet>()
            .FirstOrDefault(n => n.Hired && !n.IsBlack && n.IsTaskFinished && n.Player.IsAlive());
        if (neet == null) return;
        Logger.Info($"{neet.Player.Data.GetLogPlayerName()}(ホワイト企業)が {CustomWinnerHolder.WinnerTeam} の勝ちを乗っ取り", "Neet");
        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Neet);
        CustomWinnerHolder.WinnerIds.Add(neet.Player.PlayerId);
        CustomWinnerHolder.NeutralWinnerIds.Add(neet.Player.PlayerId);
        if (neet.HiredBy != byte.MaxValue)
        {
            CustomWinnerHolder.WinnerIds.Add(neet.HiredBy);
            CustomWinnerHolder.NeutralWinnerIds.Add(neet.HiredBy);
        }
    }

    /// <summary>死んだとき(ホスト)。タスクが残っていたら、勧誘したリクルーターに引き継ぐ</summary>
    void OnDead()
    {
        if (deathHandled) return;
        deathHandled = true;
        if (!Hired || IsTaskFinished) return;
        var left = 0;
        if (RoomTaskAssign.AllRoomTasker.TryGetValue(Player.PlayerId, out var data) && data.Generic)
            left = data.Queue.Count - data.completeroom;
        RoomTaskSettings.RemoveGeneric(Player.PlayerId);
        var recruiter = PlayerCatch.GetPlayerById(HiredBy);
        if (left <= 0 || recruiter == null || !recruiter.IsAlive()) return;
        (recruiter.GetRoleClass() as Recruiter)?.Inherit(left);
        RoomTaskSettings.AssignMidGame(recruiter, RoomTaskKind.Short, left);
        Logger.Info($"{Player.Data.GetLogPlayerName()} が死んだので、残りのタスク{left}個を {recruiter.Data.GetLogPlayerName()} が引き継ぐ", "Neet");
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: recruiter);
    }

    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (!AmongUsClient.Instance.AmHost || exiled == null || exiled.PlayerId != Player.PlayerId) return;
        //ブラック企業でタスクが終わっていないのに追放されたら、全員に罰
        var punish = Hired && IsBlack && !IsTaskFinished;
        OnDead();
        if (punish) Punish();
    }

    /// <summary>
    /// ブラック企業のニートがタスク未完了で追放されたときの罰(ホスト)<br/>
    /// 生きているインポスター以外の全員にウォークタスクを追加し、インポスターにはキルのノルマを付ける
    /// </summary>
    void Punish()
    {
        var tasks = Recruiter.OptionNeetPunishTasks.GetInt();
        var quota = Recruiter.OptionNeetPunishKillQuota.GetInt();
        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            if (pc.PlayerId == Player.PlayerId) continue;
            if (pc.GetCustomRole().IsImpostor()) continue;
            RoomTaskSettings.AddTasks(pc, RoomTaskKind.Short, tasks);
        }
        if (quota > 0)
        {
            KillQuota += quota;
            if (KillQuota == quota) KillBase = CountImpostorKills();
        }
        CustomRoleManager.LowerOthers.Add(GetLowerTextQuota);
        GameData.Instance?.RecomputeTaskCounts();
        Logger.Info($"ブラック企業のニートがタスク未完了で追放 → タスク{tasks}個追加・キルのノルマ{quota}人", "Neet");
        UtilsGameLog.AddGameLog("Neet", $"ブラック企業の{UtilsName.GetPlayerColor(Player, true)}が仕事を残して追放された");
        var text = $"ブラック企業のサラリーマンが、仕事を残したまま追放された！\n全員にタスクが{tasks}個追加される" + (quota > 0 ? $"\nインポスターは、あと{KillQuotaLeft}人キルするまで勝てない" : "");
        _ = new LateTask(() => Utils.SendMessage(text, title: Utils.ColorString(RoleInfo.RoleColor, "【ブラック企業】")), 1f, "NeetPunish", true);
        UtilsNotifyRoles.NotifyRoles();
    }

    /// <summary>インポスターの画面の下に、キルのノルマの残りを出す</summary>
    public static string GetLowerTextQuota(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seer == null || seen.PlayerId != seer.PlayerId || !seer.GetCustomRole().IsImpostor()) return "";
        var left = KillQuotaLeft;
        return left <= 0 ? "" : Utils.ColorString(Palette.ImpostorRed, $"キルのノルマ あと{left}人（達成するまで勝てない）");
    }

    //就職したら、役職名を「サラリーマン」にする
    public override void OverrideTrueRoleName(ref Color roleColor, ref string roleText)
    {
        if (Hired) roleText = roleText.Replace(GetString("Neet"), "サラリーマン");
    }

    //リクルーター以外は、ニートだと分かる(就職したことは、ほかの生きている人には分からない)
    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seer, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        if (seer == null || seer.Is(CustomRoles.Recruiter)) return;
        enabled = true;
        //ほかの人には、就職しても「ニート」のまま見せる(幽霊には本当の役職名)
        if (Hired && seer.PlayerId != Player.PlayerId && seer.IsAlive())
            roleText = roleText.Replace("サラリーマン", GetString("Neet"));
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
        if (!AmongUsClient.Instance.AmHost) return;
        //キルなどで死んだら、残りのタスクをリクルーターに引き継ぐ
        if (Hired && !deathHandled && !Player.IsAlive() && GameStates.IsInTask) OnDead();
        if (noticeTimer <= 0) return;
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

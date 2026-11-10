using System;
using System.Collections.Generic;
using AmongUs.GameOptions;

using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

/// <summary>
/// インポスター役職の能力を持つジャッカル(ジャッカル〇〇)の共通部分。<br/>
/// 持っているインポスター役職の中身(AddRole)を作って、能力の処理をそのまま渡す(ジャッカルウルフの仕組み)。<br/>
/// 能力の設定は元のインポスター役職の設定をそのまま使う。ジャッカルとしての設定(ベント・サボ・視界など)は役職ごと。<br/>
/// 新しく作るときは、このクラスを継承して RoleInfo と GetHaveRole() を書き、CustomRoles に名前を足すだけ。
/// </summary>
public abstract class JackalImpostorBase : RoleBase, ILNKiller, IUsePhantomButton, IDoubleTrigger, IKillFlashSeeable, ISidekickable, ISchrodingerCatOwner, ISelfVoter
{
    /// <summary>ジャッカルとしての設定(役職ごと)</summary>
    public class JackalOptions
    {
        public OptionItem CanVent, CanUseSabotage, HasImpostorVision;
        public OptionItem CanKilledByJackal, ExposedToJackal, JackalExposed;
    }
    static readonly Dictionary<CustomRoles, JackalOptions> AllOptions = new();
    //ジャッカルマフィア時代の設定名(翻訳キー)をそのまま使う
    enum JackalOption { JackalCanAlsoBeExposedToJMafia, JackalMafiaCanAlsoBeExposedToJackal, JackalCanKillMafia }

    /// <summary>ジャッカルとしての設定を作る(各役職の SetupOptionItem から呼ぶ)。IDは ConfigId + 11～16</summary>
    public static void SetupJackalOptions(SimpleRoleInfo info)
    {
        AllOptions[info.RoleName] = new JackalOptions
        {
            CanVent = BooleanOptionItem.Create(info, 11, GeneralOption.CanVent, true, false),
            CanUseSabotage = BooleanOptionItem.Create(info, 12, GeneralOption.CanUseSabotage, false, false),
            HasImpostorVision = BooleanOptionItem.Create(info, 13, GeneralOption.ImpostorVision, true, false),
            CanKilledByJackal = BooleanOptionItem.Create(info, 14, JackalOption.JackalCanKillMafia, false, false),
            ExposedToJackal = BooleanOptionItem.Create(info, 15, JackalOption.JackalMafiaCanAlsoBeExposedToJackal, false, false),
            JackalExposed = BooleanOptionItem.Create(info, 16, JackalOption.JackalCanAlsoBeExposedToJMafia, true, false),
        };
    }

    protected JackalImpostorBase(SimpleRoleInfo roleInfo, PlayerControl player)
    : base(roleInfo, player)
    {
        MyRole = roleInfo.RoleName;
        Opt = AllOptions[roleInfo.RoleName];
    }
    protected readonly CustomRoles MyRole;
    protected readonly JackalOptions Opt;
    /// <summary>持っているインポスター役職</summary>
    public CustomRoles HaveRole { get; private set; } = CustomRoles.NotAssigned;
    protected RoleBase AddRole;

    /// <summary>持たせるインポスター役職</summary>
    protected abstract CustomRoles GetHaveRole();

    #region 共通の判定(ほかのファイルから使う)
    /// <summary>インポスター役職を持つジャッカルか</summary>
    public static bool IsJackalImpostor(CustomRoles role)
        => role.GetRoleInfo()?.ClassType?.IsSubclassOf(typeof(JackalImpostorBase)) == true;
    /// <summary>持っている(持つ予定の)インポスター役職</summary>
    public static CustomRoles GetBaseRole(CustomRoles role)
        => IsJackalImpostor(role) ? role.GetRoleInfo()?.AddHaveRole?.Invoke() ?? CustomRoles.Impostor : CustomRoles.NotAssigned;
    /// <summary>インポスター役職を持つジャッカルを全部、勝利役職に足す</summary>
    public static void AddWinnerRoles()
    {
        foreach (var info in CustomRoleManager.AllRolesInfo.Values)
            if (IsJackalImpostor(info.RoleName)) CustomWinnerHolder.WinnerRoles.Add(info.RoleName);
    }
    /// <summary>どれかの役職が設定で有効か</summary>
    public static bool AnyEnabled()
    {
        foreach (var info in CustomRoleManager.AllRolesInfo.Values)
            if (IsJackalImpostor(info.RoleName) && info.RoleName.IsEnable()) return true;
        return false;
    }
    /// <summary>どれかの役職が試合にいるか</summary>
    public static bool AnyPresent()
    {
        foreach (var info in CustomRoleManager.AllRolesInfo.Values)
            if (IsJackalImpostor(info.RoleName) && info.RoleName.IsPresent()) return true;
        return false;
    }
    /// <summary>サイドキック(変身でジャッカルドールを作る)ができる役職が有効か</summary>
    public static bool AnyEnabledCanSidekick()
    {
        foreach (var info in CustomRoleManager.AllRolesInfo.Values)
            if (IsJackalImpostor(info.RoleName) && info.RoleName.IsEnable() && GetBaseRole(info.RoleName).CanMakeMadmate()) return true;
        return false;
    }
    #endregion

    public override void Add()
    {
        HaveRole = GetHaveRole();
        if (CustomRoleManager.AllRolesInfo.TryGetValue(HaveRole, out var roleinfo))
        {
            AddRole = roleinfo.CreateInstance(Player);
            Logger.Info($"{MyRole} + {HaveRole}", "JackalImpostor");
        }
        AddRole?.Add();
        Player.AddDoubleTrigger();
    }
    public override void OnDestroy()
    {
        AddRole?.OnDestroy();
        AddRole = null;
        HaveRole = CustomRoles.NotAssigned;
    }
    public override void OnFixedUpdate(PlayerControl player) => AddRole?.OnFixedUpdate(player);
    public override void OnSpawn(bool initialState = false) => AddRole?.OnSpawn(initialState);
    public override void OnStartMeeting() => AddRole?.OnStartMeeting();
    public override bool VotingResults(ref NetworkedPlayerInfo Exiled, ref bool IsTie, Dictionary<byte, int> vote, byte[] mostVotedPlayers, bool ClearAndExile)
        => AddRole?.VotingResults(ref Exiled, ref IsTie, vote, mostVotedPlayers, ClearAndExile) ?? false;
    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
        => AddRole?.CheckVoteAsVoter(votedForId, voter) ?? true;
    public override void OverrideTrueRoleName(ref Color roleColor, ref string roleText)
    {
        //ジャッカルウルフのように役職名に持っている役職が出ない場合だけ、小さく付け足す
        if (AddRole is null || HaveRole is CustomRoles.Impostor || !ShowHaveRoleInName) return;
        roleText += $"<size=50%>{GetString($"{HaveRole}")}</size>";
    }
    /// <summary>役職名の後ろに持っている役職を小さく出すか(ジャッカル〇〇は名前に入っているので出さない)</summary>
    protected virtual bool ShowHaveRoleInName => false;
    public override CustomRoles HaveAddRole() => HaveRole;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => AddRole?.OnEnterVent(physics, ventId) ?? true;
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => AddRole?.CanVentMoving(physics, ventId) ?? true;
    public override bool CanUseAbilityButton() => AddRole?.CanUseAbilityButton() ?? false;
    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(Opt.HasImpostorVision.GetBool());
        AddRole?.ApplyGameOptions(opt);
    }
    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        (var killer, var target) = info.AttemptTuple;
        if (killer.Is(CountTypes.Jackal) && !Opt.CanKilledByJackal.GetBool())
        {
            info.DoKill = false;
            killer.SetKillCooldown();
            return false;
        }
        return AddRole?.OnCheckMurderAsTarget(info) ?? true;
    }
    public override void OnMurderPlayerAsTarget(MurderInfo info) => AddRole?.OnMurderPlayerAsTarget(info);
    public override void OnShapeshift(PlayerControl target) => AddRole?.OnShapeshift(target);
    public override bool CheckShapeshift(PlayerControl target, ref bool shouldAnimate) => AddRole?.CheckShapeshift(target, ref shouldAnimate) ?? true;
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target) => AddRole?.OnReportDeadBody(reporter, target);
    public override bool CanClickUseVentButton => AddRole?.CanClickUseVentButton ?? true;
    public override string MeetingAddMessage() => AddRole?.MeetingAddMessage() ?? "";
    public override (byte? votedForId, int? numVotes, bool doVote) ModifyVote(byte voterId, byte sourceVotedForId, bool isIntentional) => AddRole?.ModifyVote(voterId, sourceVotedForId, isIntentional) ?? (null, null, true);
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner) => AddRole?.OnExileWrapUp(exiled, ref DecidedWinner);
    public override void AfterMeetingTasks() => AddRole?.AfterMeetingTasks();
    public override void StartGameTasks() => AddRole?.StartGameTasks();
    public override bool OnInvokeSabotage(SystemTypes systemType) => AddRole?.OnInvokeSabotage(systemType) ?? true;
    public override bool OnSabotage(PlayerControl player, SystemTypes systemType) => AddRole?.OnSabotage(player, systemType) ?? true;
    public override void AfterSabotage(SystemTypes systemType) => AddRole?.AfterSabotage(systemType);
    public override bool NotifyRolesCheckOtherName => AddRole?.NotifyRolesCheckOtherName ?? false;
    public override void OverrideProgressTextAsSeer(PlayerControl seen, ref bool enabled, ref string text)
        => AddRole?.OverrideProgressTextAsSeer(seen, ref enabled, ref text);
    public override void OverrideProgressTextAsSeen(PlayerControl seer, ref bool enabled, ref string text)
        => AddRole?.OverrideProgressTextAsSeen(seer, ref enabled, ref text);
    static bool IsJackalLeader(PlayerControl pc) => pc.Is(CustomRoles.Jackal) || IsJackalImpostor(pc.GetCustomRole());
    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        if (IsJackalLeader(seen) && Opt.ExposedToJackal.GetBool())
        {
            enabled = true;
            addon = false;
        }
        AddRole?.OverrideDisplayRoleNameAsSeer(seen, ref enabled, ref roleColor, ref roleText, ref addon);
    }
    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        if (IsJackalLeader(seen) && Opt.JackalExposed.GetBool())
        {
            enabled = true;
            addon = false;
        }
        AddRole?.OverrideDisplayRoleNameAsSeen(seen, ref enabled, ref roleColor, ref roleText, ref addon);
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false) => AddRole?.GetProgressText(comms, GameLog);
    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false) => AddRole?.GetMark(seer, seen, isForMeeting);
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false) => AddRole?.GetLowerText(seer, seen, isForMeeting, isForHud);
    public override string GetSuffix(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false) => AddRole?.GetSuffix(seer, seen, isForMeeting);
    public override string GetAbilityButtonText() => AddRole?.GetAbilityButtonText();
    public override bool OverrideAbilityButton(out string text)
    {
        if (AddRole?.OverrideAbilityButton(out var abilitytext) ?? false)
        {
            text = abilitytext;
            return true;
        }
        text = default;
        return false;
    }
    public override bool CancelReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, ref DontReportreson reason)
        => AddRole?.CancelReportDeadBody(reporter, target, ref reason) ?? false;
    public override CustomRoles TellResults(PlayerControl player) => AddRole?.TellResults(player) ?? CustomRoles.NotAssigned;
    public override RoleTypes? AfterMeetingRole => AddRole?.AfterMeetingRole ?? null;
    public override void CheckWinner(GameOverReason reason) => AddRole?.CheckWinner(reason);
    public bool CanKill => AddRole is IKiller killer ? killer.CanKill : true;
    public bool IsKiller => AddRole is IKiller killer ? killer.IsKiller : true;
    public bool CanUseKillButton() => AddRole is IKiller killer ? killer.CanUseKillButton() : CanKill;
    public float CalculateKillCooldown() => AddRole is IKiller killer ? killer.CalculateKillCooldown() : Options.DefaultKillCooldown;
    public bool CanUseSabotageButton() => Opt.CanUseSabotage.GetBool() && (AddRole is IKiller killer ? killer.CanUseSabotageButton() : true);
    public bool CanUseImpostorVentButton() => Opt.CanVent.GetBool() && (AddRole is IKiller killer ? killer.CanUseImpostorVentButton() : true);
    public void OnCheckMurderAsKiller(MurderInfo info) { if (AddRole is IKiller killer) killer.OnCheckMurderAsKiller(info); }
    public void OnCheckMurderDontKill(MurderInfo info) { if (AddRole is IKiller killer) killer.OnCheckMurderDontKill(info); }
    public void OnMurderPlayerAsKiller(MurderInfo info) { if (AddRole is IKiller killer) killer.OnCheckMurderAsKiller(info); }
    public bool OverrideKillButtonText(out string text)
    {
        if ((AddRole as IKiller)?.OverrideKillButtonText(out var killbuttontext) is true)
        {
            text = killbuttontext;
            return true;
        }
        text = default;
        return false;
    }
    public bool OverrideKillButton(out string text)
    {
        if ((AddRole as IImpostor)?.OverrideKillButton(out var killbutton) is true)
        {
            text = killbutton;
            return true;
        }
        text = default;
        return false;
    }
    public bool OverrideImpVentButton(out string text)
    {
        if ((AddRole as IImpostor)?.OverrideImpVentButton(out var ventbutton) is true)
        {
            text = ventbutton;
            return true;
        }
        text = default;
        return false;
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        try
        {
            AddRole.ReceiveRPC(reader);
        }
        catch (Exception ex)
        {
            Logger.Error($"{ex}", "JackalImpostor");
        }
    }
    public ISchrodingerCatOwner.TeamType SchrodingerCatChangeTo => ISchrodingerCatOwner.TeamType.Jackal;
    bool IUsePhantomButton.IsPhantomRole => AddRole is IUsePhantomButton iusephantom && iusephantom?.IsPhantomRole is true;
    bool IUsePhantomButton.UseOneclickButton => AddRole is IUsePhantomButton iusephantom && iusephantom?.UseOneclickButton is true;
    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        if (AddRole is IUsePhantomButton iusephantom)
        {
            iusephantom.OnClick(ref AdjustKillCooldown, ref ResetCooldown);
        }
    }
    public bool CheckAction => (AddRole as IDoubleTrigger)?.CheckAction ?? false;
    public bool SingleAction(PlayerControl killer, PlayerControl target)
        => (AddRole as IDoubleTrigger)?.SingleAction(killer, target) ?? true;
    public bool DoubleAction(PlayerControl killer, PlayerControl target)
        => (AddRole as IDoubleTrigger)?.DoubleAction(killer, target) ?? true;
    public bool? CheckKillFlash(MurderInfo info) => (AddRole as IKillFlashSeeable)?.CheckKillFlash(info) ?? false;

    //会議中の能力(ジャッジ方式): 持っている役職が使えるときだけ
    bool ISelfVoter.CanUseVoted() => AddRole is ISelfVoter selfVoter && selfVoter.CanUseVoted();
    bool ISelfVoter.IsCantUse() => AddRole is not ISelfVoter selfVoter || selfVoter.IsCantUse();
}

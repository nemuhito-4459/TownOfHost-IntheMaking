using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

using TownOfHost.Roles.Core;
using Hazel;

namespace TownOfHost.Roles.Neutral;

public sealed class JackalDoll : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalDoll),
            player => new JackalDoll(player),
            CustomRoles.Jackaldoll,
            () => CanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            13200,
            null,//設定は役職一覧に出さず、SetupCommonOptionで共通設定として作る
            "jacd",
            "#00b4eb",
            (1, 4),
                assignInfo: new RoleAssignInfo(CustomRoles.Jackaldoll, CustomRoleTypes.Neutral)
                {
                    AssignCountRule = new(0, 15, 1)
                },
            from: From.TownOfHost_K
        );
    public JackalDoll(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        BossAndSidekicks.Clear();
        CanPromotion = false;
        ExiledPlayerInfo = null;
    }
    static OptionItem OptionJackaldieMode;
    static OptionItem OptionChangeRole;
    static OptionItem OptionSideKickMaxmim;
    static OptionItem CanVent;
    static OptionItem VentCool;
    static OptionItem VentIntime;
    static OptionItem CanVentMove;
    static NetworkedPlayerInfo ExiledPlayerInfo;
    enum Option
    {
        JackaldolldieMode, JackaldollRoleChe, SideKickJackaldollMacCount
    }
    enum Diemode
    {
        NoProcessing,
        FollowingSuicide,
        ChangeRole,
    };
    public static int NowSideKickCount;
    bool CanPromotion;
    /// <summary>
    /// key→Sidekick
    /// Va→Owner
    /// </summary>
    /// <returns></returns>
    public static Dictionary<byte, (byte Owner, CustomRoles Ownerrole)> BossAndSidekicks = new();
    public static readonly CustomRoles[] ChangeRoles =
    {
        CustomRoles.Crewmate, CustomRoles.Madmate , CustomRoles.Jester, CustomRoles.Opportunist
    };
    public static int GetSideKickCount()
    {
        if ((CustomRoles.Jackal.IsEnable() && Jackal.OptionCanMakeSidekick.GetBool())
        || JackalImpostorBase.AnyEnabledCanSidekick())
        {
            //共通設定の最大人数をそのまま使う
            return OptionSideKickMaxmim.GetInt();
        }
        else return 0;
    }
    /// <summary>
    /// ジャッカルドールの共通設定(マッドメイトの共通設定と同じく、ジャッカル陣営タブに直接並べる)<br/>
    /// IDは以前の役職設定と同じ(13200+n)なので保存済みの値はそのまま引き継がれる
    /// </summary>
    public static void SetupCommonOption()
    {
        const int id = 13200;
        var tab = TabGroup.JackalRoles;
        var cRolesString = ChangeRoles.Select(x => x.ToString()).ToArray();
        OptionSideKickMaxmim = IntegerOptionItem.Create(id + 9, Option.SideKickJackaldollMacCount, new(0, 15, 1), 1, tab, false)
            .SetValueFormat(OptionFormat.Players)
            .SetHeader(true)
            .SetColorcode(RoleInfo.RoleColorCode);
        OptionJackaldieMode = StringOptionItem.Create(id + 10, Option.JackaldolldieMode, EnumHelper.GetAllNames<Diemode>(), 0, tab, false)
            .SetColorcode(RoleInfo.RoleColorCode);
        OptionChangeRole = StringOptionItem.Create(id + 15, Option.JackaldollRoleChe, cRolesString, 3, tab, false)
            .SetParent(OptionJackaldieMode)
            .SetEnabled(() => OptionJackaldieMode.GetValue() is 2);
        CanVent = BooleanOptionItem.Create(id + 16, GeneralOption.CanVent, false, tab, false)
            .SetColorcode(RoleInfo.RoleColorCode);
        VentCool = FloatOptionItem.Create(id + 17, GeneralOption.Cooldown, new(0f, 180f, 0.5f), 0f, tab, false).SetParent(CanVent).SetValueFormat(OptionFormat.Seconds);
        VentIntime = FloatOptionItem.Create(id + 18, GeneralOption.EngineerInVentCooldown, new(0f, 180f, 0.5f), 0f, tab, false).SetParent(CanVent).SetZeroNotation(OptionZeroNotation.Infinity).SetValueFormat(OptionFormat.Seconds);
        CanVentMove = BooleanOptionItem.Create(id + 19, "MadmateCanMovedByVent", false, tab, false).SetParent(CanVent);
    }
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => CanVentMove.GetBool();
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = VentCool.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = VentIntime.GetFloat();
    }
    public static void Sidekick(PlayerControl doll, PlayerControl owner)
    {
        NowSideKickCount++;
        if (BossAndSidekicks.ContainsKey(doll.PlayerId))
        {
            BossAndSidekicks.Remove(doll.PlayerId);
        }

        var state = PlayerState.GetByPlayerId(doll.PlayerId);

        if (owner.Is(CustomRoles.Jackal))
        {
            if (Jackal.OptionSidekickPromotion.GetBool())
            {
                Main.AllPlayerKillCooldown[doll.PlayerId] = Jackal.OptionKillCooldown.GetFloat();
                BossAndSidekicks.Add(doll.PlayerId, (owner.PlayerId, CustomRoles.Jackal));

                (doll.GetRoleClass() as JackalDoll).SetSidekick(owner.PlayerId, CustomRoles.Jackal);
            }
            if (Jackal.OptionSidekickCanSeeOldImpostorTeammates.GetBool())
            {
                foreach (var imp in PlayerCatch.AllPlayerFirstTypes.Where(x => x.Value is CustomRoleTypes.Impostor))
                {
                    if (state.TargetColorData.ContainsKey(imp.Key)) NameColorManager.Remove(doll.PlayerId, imp.Key);
                    NameColorManager.Add(doll.PlayerId, imp.Key, "ffffff");
                }
            }
            if (Jackal.OptionImpostorCanSeeNameColor.GetBool())
            {
                foreach (var imp in PlayerCatch.AllPlayerFirstTypes.Where(x => x.Value is CustomRoleTypes.Impostor))
                {
                    var impostorstate = PlayerState.GetByPlayerId(imp.Key);
                    if (impostorstate.TargetColorData.ContainsKey(doll.PlayerId)) NameColorManager.Remove(imp.Key, doll.PlayerId);
                    NameColorManager.Add(imp.Key, doll.PlayerId, "ffffff");
                }
            }
        }
        doll.RpcSetRole(CanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate, true);

        foreach (var jackals in PlayerCatch.AllPlayerControls)
        {
            if (jackals.Is(CountTypes.Jackal))
            {
                NameColorManager.Add(jackals.PlayerId, doll.PlayerId, UtilsRoleText.GetRoleColorCode(CustomRoles.Jackaldoll));
                NameColorManager.Add(doll.PlayerId, jackals.PlayerId, UtilsRoleText.GetRoleColorCode(CustomRoles.Jackaldoll));
            }
        }
        //どっちにしろ更新を
        UtilsNotifyRoles.NotifyRoles();
    }
    public override bool VotingResults(ref NetworkedPlayerInfo Exiled, ref bool IsTie, Dictionary<byte, int> vote, byte[] mostVotedPlayers, bool ClearAndExile)
    {
        ExiledPlayerInfo = Exiled;
        return false;
    }
    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        var (killer, target) = info.AppearanceTuple;
        if (BossAndSidekicks.TryGetValue(target.PlayerId, out var data))
        {//サイドキックされて親分に殺されそうとか言う事になるとキルガード
            if (data.Owner == killer.PlayerId)
            {
                info.CanKill = false;
                killer.RpcProtectedMurderPlayer(target);
                return false;
            }
        }
        return true;
    }
    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (BossAndSidekicks.ContainsKey(Player.PlayerId)) return;

        var id = ExiledPlayerInfo?.PlayerId ?? byte.MaxValue;

        if (PlayerCatch.AllAlivePlayerControls.Any(x => (x.Is(CustomRoles.Jackal) || x.Is(CustomRoles.JackalMafia) || x.GetCustomRole().IsJackalImpostor() || BossAndSidekicks.ContainsKey(x.PlayerId)) && x.PlayerId != id)) return;

        foreach (var Jd in PlayerCatch.AllAlivePlayerControls.Where(x => x.Is(CustomRoles.Jackaldoll) && !BossAndSidekicks.ContainsKey(x.PlayerId)))
        {
            switch ((Diemode)OptionJackaldieMode.GetValue())
            {
                case Diemode.FollowingSuicide:
                    //ガードなどは無視
                    _ = new LateTask(() =>
                    {
                        PlayerState.GetByPlayerId(Jd.PlayerId).DeathReason = CustomDeathReason.FollowingSuicide;
                        Jd.RpcExileV3();
                        PlayerState.GetByPlayerId(Jd.PlayerId).SetDead();
                    }, 3, "JackalDoolFollowSuicide", true);
                    break;
                case Diemode.ChangeRole:
                    UtilsGameLog.AddGameLog($"JackalDool", UtilsName.GetPlayerColor(Jd) + ":  " + string.Format(GetString("Executioner.ch"), Utils.ColorString(UtilsRoleText.GetRoleColor(CustomRoles.Jackal), GetString("Jackal")), Translator.GetRoleString($"{ChangeRoles[OptionChangeRole.GetValue()]}").Color(UtilsRoleText.GetRoleColor(ChangeRoles[OptionChangeRole.GetValue()]))));
                    if (!Utils.RoleSendList.Contains(Player.PlayerId)) Utils.RoleSendList.Add(Player.PlayerId);
                    Jd.RpcSetCustomRole(ChangeRoles[OptionChangeRole.GetValue()], log: null);
                    UtilsNotifyRoles.NotifyRoles();
                    break;
            }
        }
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!player.IsAlive()) return;
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.ExiledAnimate || AntiBlackout.IsSet) return;

        if (BossAndSidekicks.TryGetValue(player.PlayerId, out var data))
        {
            var oya = PlayerCatch.GetPlayerById(data.Owner);
            if ((!oya.IsAlive() || oya.GetCustomRole() != data.Ownerrole) && !CanPromotion)
            {
                MyState.SetCountType(CountTypes.Jackal);
                CanPromotion = true;
                if (!Utils.RoleSendList.Contains(Player.PlayerId)) Utils.RoleSendList.Add(Player.PlayerId);
                player.RpcSetCustomRole(data.Ownerrole, true, log: null);
            }
            CanPromotion = false;
        }
    }
    public override void OverrideTrueRoleName(ref Color roleColor, ref string roleText)
    {
        if (BossAndSidekicks.ContainsKey(Player.PlayerId))
        {
            roleText = $"☆" + GetString("Jackaldoll");
        }
    }
    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (GameLog) return "";

        if (BossAndSidekicks.TryGetValue(Player.PlayerId, out var data))
        {
            return Utils.ColorString(Main.PlayerColors[data.Owner], "◎");
        }
        return "";
    }
    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        addon = false;
        if (seen.Is(CountTypes.Jackal))
            enabled = true;
    }
    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        addon = false;
        if (seen.Is(CountTypes.Jackal))
            enabled = true;
    }

    public void SetSidekick(byte ownerId, CustomRoles ownerRole)
    {
        using var sender = CreateSender();
        sender.Writer.Write(NowSideKickCount);
        sender.Writer.Write(ownerId);
        sender.Writer.Write((int)ownerRole);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        NowSideKickCount = reader.ReadInt32();
        var ownerId = reader.ReadByte();
        var ownerRole = (CustomRoles)reader.ReadInt32();
        BossAndSidekicks[Player.PlayerId] = (ownerId, ownerRole);
    }
}
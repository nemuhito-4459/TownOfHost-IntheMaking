using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using Hazel;

namespace TownOfHost.Roles.Crewmate;

public sealed class Cakeshop : RoleBase, INekomata
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Cakeshop),
            player => new Cakeshop(player),
            CustomRoles.Cakeshop,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            21200,
            null,
            "cs",
            "#aacbff",
            (0, 15),
            introSound: () => GetIntroSound(RoleTypes.Crewmate),
            from: From.TownOfHost_K
        );
    public Cakeshop(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        Addedaddons.Clear();
        MyAddAddons = [];
        CustomRoleManager.LowerOthers.Add(GetLowerTextOthers);
    }

    public Dictionary<byte, CustomRoles> Addedaddons = new();
    static ICollection<CustomRoles> MyAddAddons;//非ホスト導入者用

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (Event.CheckRole(CustomRoles.Cakeshop) is false)
        {
            Logger.Info($"真っ黒こげのケーキ", "CakeShop");
            Player.RpcSetCustomRole(CustomRoles.Emptiness);
            Options.CustomRoleSpawnChances[CustomRoles.Cakeshop].SetValue(0, true, false);
            return;
        }
        _ = new LateTask(() =>
        {
            Logger.Info("あっ、ケーキの効果が切れちゃった!!", nameof(Cakeshop));
            PlayerState.AllPlayerStates.DoIf(
                x => Addedaddons.ContainsKey(x.Key),
                state => PlayerCatch.GetPlayerById(state.Key).RpcReplaceSubRole(Addedaddons[state.Key], true));

            if (0 < Addedaddons.Count)
            {
                PlayerCatch.AllPlayerControls.Do(pc =>
                {
                    if (pc.IsModClient())
                        SendRpc(pc.PlayerId, CustomRoles.NotAssigned);
                });
            }
            if (!Player.IsAlive())
            {
                Addedaddons.Clear();
                return;
            }
            Logger.Info("ケーキちょうど焼けたからあげる!!", nameof(Cakeshop));
            PlayerCatch.AllAlivePlayerControls.Do(pc =>
            {
                if (pc == null) return;
                var addons = GetAddons(pc.GetCustomRole().GetCustomRoleTypes());
                if (addons == null) return;
                var addon = addons.Where(x => !pc.GetCustomSubRoles().Contains(x) && x is not CustomRoles.Amnesia and not CustomRoles.Amanojaku and not CustomRoles.Absorb)
                                .OrderBy(x => Guid.NewGuid())
                                .FirstOrDefault();
                Addedaddons[pc.PlayerId] = addon;

                pc.RpcSetCustomRole(addon);
                if (pc.IsModClient())
                {
                    SendRpc(pc.PlayerId, addon);
                }
            });
            UtilsNotifyRoles.NotifyRoles();
        }, 5f, "CakeshopAssign", true);
    }

    CustomRoles[] GetAddons(CustomRoleTypes type)
    {
        //属性は削除済みなので、配れる属性はない
        return null;
    }

    public string GetLowerTextOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Player.IsAlive()) return "";
        if (seen != seer) return "";
        if (isForMeeting) return "";
        if (seer.IsModClient() && 0 < MyAddAddons.Count)
        {
            var text = "";
            foreach (var addon in MyAddAddons)
            {
                text += Utils.ColorString(UtilsRoleText.GetRoleColor(addon), GetString($"{addon}Info"));
            }
            return text is "" ? "" : $"<size=50%>{text}</size>";
        }
        return Addedaddons.TryGetValue(seen.PlayerId, out var role) ? $"<size=50%>{Utils.ColorString(UtilsRoleText.GetRoleColor(role), GetString($"{role}Info"))}</size>" : "";
    }

    public bool DoRevenge(CustomDeathReason deathReason)
        => true;

    public bool IsCandidate(PlayerControl player)
        => true;

    public override void OnDestroy()
    {
        if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Default) return;

        AfterMeetingTasks();
    }

    void SendRpc(byte playerid, CustomRoles addon)
    {
        var sender = RPC.RpcPublicRoleSync(playerid, RoleInfo.RoleName);
        sender.WritePacked((int)addon);
        AmongUsClient.Instance.FinishRpcImmediately(sender);
    }
    public static void ReceivePublickRPC(MessageReader reader)
    {
        var role = (CustomRoles)reader.ReadPackedInt32();
        if (role is CustomRoles.NotAssigned)
        {
            MyAddAddons = [];
        }
        else
        {
            MyAddAddons.Add(role);
        }
    }
}

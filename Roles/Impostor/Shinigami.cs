using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

/// <summary>
/// 死神(ファントム置き換え)<br/>
/// ・ファントムボタンで、キルが届く距離にいる一番近い人を「冥界送り」(陣営を問わず昇天させる。死体は残らない)<br/>
/// ・会議の始めに、昇天した人と、そのとき近く(設定の半径)にいた人が全員に知らされる(死神が近くにいれば死神も)<br/>
/// ・普通のキルもできる。冥界送りのクールダウンは別の設定<br/>
/// ・設定で、インポスターが分からない(相手のインポスターからは死神が分かる)・サボタージュできない
/// </summary>
public sealed class Shinigami : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Shinigami),
            player => new Shinigami(player),
            CustomRoles.Shinigami,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            6200,
            SetupOptionItem,
            "sg",
            OptionSort: (3, 10),
            isCantSeeTeammates: true,
            from: From.TownOfHost_IntheMaking
        );
    public Shinigami(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        SyncCantSeeTeammates();
        records.Clear();
    }

    static OptionItem OptionCooldown;
    static OptionItem OptionRadius;
    static OptionItem OptionCanSabotage;
    static OptionItem OptionCanSeeImpostor;
    enum OptionName
    {
        ShinigamiCooldown,
        ShinigamiRadius,
        ShinigamiCanSabotage,
        ShinigamiCanSeeImpostor,
    }
    static void SetupOptionItem()
    {
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.ShinigamiCooldown, new(0f, 180f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionRadius = FloatOptionItem.Create(RoleInfo, 11, OptionName.ShinigamiRadius, new(0.25f, 5f, 0.25f), 1.5f, false)
            .SetValueFormat(OptionFormat.Multiplier);
        OptionCanSabotage = BooleanOptionItem.Create(RoleInfo, 12, OptionName.ShinigamiCanSabotage, false, false);
        OptionCanSeeImpostor = BooleanOptionItem.Create(RoleInfo, 13, OptionName.ShinigamiCanSeeImpostor, false, false)
            .RegisterUpdateValueEvent((_, _) => SyncCantSeeTeammates());
    }
    /// <summary>「インポスターを視認できるか」を役職の情報に反映する</summary>
    public static void SyncCantSeeTeammates()
    {
        if (OptionCanSeeImpostor == null) return;
        RoleInfo.IsCantSeeTeammates = !OptionCanSeeImpostor.GetBool();
    }

    /// <summary>この会議で知らせる記録(昇天した人 → そのとき近くにいた人)</summary>
    static readonly List<(byte victim, List<byte> nearby)> records = new();

    public bool CanUseSabotageButton() => OptionCanSabotage.GetBool();

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = OptionCooldown.GetFloat();
    }

    bool IUsePhantomButton.IsresetAfterKill => false;
    /// <summary>ファントムボタン(冥界送り)</summary>
    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        ResetCooldown = true;
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;

        //キルが届く距離にいる一番近い人(陣営は問わない)
        var killDistance = NormalGameOptionsV12.KillDistances[Mathf.Clamp(GameManager.Instance.LogicOptions.currentGameOptions.GetInt(Int32OptionNames.KillDistance), 0, 2)];
        var myPos = Player.GetTruePosition();
        PlayerControl target = null;
        var minDis = float.MaxValue;
        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            if (pc.PlayerId == Player.PlayerId || pc.inVent) continue;
            var dis = Vector2.Distance(myPos, pc.GetTruePosition());
            if (dis > killDistance || dis >= minDis) continue;
            if (PhysicsHelpers.AnyNonTriggersBetween(myPos, (pc.GetTruePosition() - myPos).normalized, dis, Constants.ShipAndObjectsMask)) continue;
            target = pc;
            minDis = dis;
        }
        if (target == null)
        {
            //誰もいなければ使わなかったことにする
            ResetCooldown = false;
            return;
        }
        Ascend(target);
    }

    /// <summary>冥界送り(昇天させる。死体は残らない)</summary>
    void Ascend(PlayerControl target)
    {
        //そのとき近くにいた人(死神も含む)
        var radius = OptionRadius.GetFloat();
        var pos = target.GetTruePosition();
        var nearby = PlayerCatch.AllAlivePlayerControls
            .Where(pc => pc.PlayerId != target.PlayerId && Vector2.Distance(pc.GetTruePosition(), pos) <= radius)
            .Select(pc => pc.PlayerId).ToList();
        records.Add((target.PlayerId, nearby));

        var state = PlayerState.GetByPlayerId(target.PlayerId);
        state.DeathReason = CustomDeathReason.Ascend;
        target.SetRealKiller(Player);
        target.RpcExileV3();
        state.SetDead();
        var roomName = target.GetShipRoomName();
        state.KillRoom = roomName;
        UtilsGameLog.AddGameLog("Ascend", $"{UtilsName.GetPlayerColor(target.PlayerId, true)}({UtilsRoleText.GetTrueRoleName(target.PlayerId, false).RemoveSizeTags()}) [{Utils.GetVitalText(target.PlayerId, true)}]〔{roomName}〕");
        UtilsGameLog.AddGameLogsub($"\n\t⇐ {UtilsName.GetPlayerColor(Player.PlayerId, true)}({UtilsRoleText.GetTrueRoleName(Player.PlayerId, false)})");
        Logger.Info($"{Player.Data.GetLogPlayerName()} が {target.Data.GetLogPlayerName()} を冥界送り (近くにいた人: {string.Join(",", nearby)})", "Shinigami");
        Player.SetKillCooldown();
        _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(), 0.1f, "Shinigami", true);
    }

    /// <summary>会議の始めに、昇天した人と近くにいた人を全員に知らせる</summary>
    public override void OnStartMeeting()
    {
        if (!AmongUsClient.Instance.AmHost || records.Count == 0) return;
        var title = Utils.ColorString(RoleInfo.RoleColor, "【冥界送り】");
        foreach (var (victim, nearby) in records.ToArray())
        {
            var names = nearby.Count == 0 ? "（誰もいなかった）" : string.Join("、", nearby.Select(id => UtilsName.GetPlayerColor(id, true)));
            var text = $"{UtilsName.GetPlayerColor(victim, true)} が昇天した\nそのとき近くにいた人：{names}";
            _ = new LateTask(() => Utils.SendMessage(text, title: title), 4f, "ShinigamiSend");
            MeetingHudPatch.StartPatch.meetingsends.Add((byte.MaxValue, title, text));
        }
        records.Clear();
    }

    public override string GetAbilityButtonText() => "冥界送り";
}

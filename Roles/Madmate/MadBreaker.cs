using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Madmate;

/// <summary>
/// マッドブレイカー(クルーメイト置き換え・マッドメイト)<br/>
/// ・タスクを完了すると、設定の確率で「タスクに失敗」して、停電(ザ・フングルはキノコ)・リアクター・通信障害のどれかが起こる<br/>
/// ・どれが起こるかは、それぞれの確率(重み)で決める
/// </summary>
public sealed class MadBreaker : RoleBase, IKillFlashSeeable, IDeathReasonSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(MadBreaker),
            player => new MadBreaker(player),
            CustomRoles.MadBreaker,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Madmate,
            8500,
            SetupOptionItem,
            "mbr",
            OptionSort: (4, 4),
            from: From.TownOfHost_IntheMaking
        );
    public MadBreaker(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.ForRecompute
    )
    {
    }

    static OptionItem LightsChance;
    static OptionItem ReactorChance;
    static OptionItem CommsChance;
    static OptionItem TaskCount;
    static OptionItem FailChance;
    static OptionItem OptionCanSeeImpostor;
    static OptionItem DuringSabotage;
    enum OptionName
    {
        MadBreakerLightsChance,
        MadBreakerReactorChance,
        MadBreakerCommsChance,
        MadBreakerTaskCount,
        MadBreakerFailChance,
        MadBreakerCanSeeImpostor,
        MadBreakerDuringSabotage,
    }
    static void SetupOptionItem()
    {
        LightsChance = IntegerOptionItem.Create(RoleInfo, 10, OptionName.MadBreakerLightsChance, new(0, 100, 1), 35, false)
            .SetValueFormat(OptionFormat.Percent);
        ReactorChance = IntegerOptionItem.Create(RoleInfo, 11, OptionName.MadBreakerReactorChance, new(0, 100, 1), 35, false)
            .SetValueFormat(OptionFormat.Percent);
        CommsChance = IntegerOptionItem.Create(RoleInfo, 12, OptionName.MadBreakerCommsChance, new(0, 100, 1), 30, false)
            .SetValueFormat(OptionFormat.Percent);
        TaskCount = IntegerOptionItem.Create(RoleInfo, 13, OptionName.MadBreakerTaskCount, new(1, 10, 1), 2, false)
            .SetValueFormat(OptionFormat.Pieces);
        FailChance = IntegerOptionItem.Create(RoleInfo, 14, OptionName.MadBreakerFailChance, new(0, 95, 1), 30, false)
            .SetValueFormat(OptionFormat.Percent);
        OptionCanSeeImpostor = BooleanOptionItem.Create(RoleInfo, 15, OptionName.MadBreakerCanSeeImpostor, false, false);
        DuringSabotage = BooleanOptionItem.Create(RoleInfo, 16, OptionName.MadBreakerDuringSabotage, false, false);
    }

    /// <summary>マッドブレイカーのタスク数(ショートタスクだけ)</summary>
    public static int NumTasks => TaskCount.GetInt();
    /// <summary>この役職がインポスターを視認できるか(マッドメイト共通の設定とは別)</summary>
    public static bool CanSeeImpostor(CustomRoles role) => role is CustomRoles.MadBreaker && OptionCanSeeImpostor.GetBool();

    public bool? CheckKillFlash(MurderInfo info) => Options.MadmateCanSeeKillFlash.GetBool();
    public bool? CheckSeeDeathReason(PlayerControl seen) => Options.MadmateCanSeeDeathReason.GetBool();
    public override CustomRoles TellResults(PlayerControl player) => Options.MadTellOpt();

    public override bool OnCompleteTask(uint taskid)
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return true;
        if (IRandom.Instance.Next(100) >= FailChance.GetInt()) return true;
        if (!DuringSabotage.GetBool() && AnySabotageActive()) return true;

        //起こせるもの(今起きていないもの)から、確率の重みで選ぶ
        var map = (MapNames)Main.NormalOptions.MapId;
        var lights = map == MapNames.Fungle ? SystemTypes.MushroomMixupSabotage : SystemTypes.Electrical;
        var reactor = Utils.GetCriticalSabotageSystemType();
        var list = new List<(SystemTypes type, int weight)>
        {
            (lights, LightsChance.GetInt()),
            (reactor, ReactorChance.GetInt()),
            (SystemTypes.Comms, CommsChance.GetInt()),
        }.Where(x => x.weight > 0 && !Utils.IsActive(x.type)).ToList();
        var total = list.Sum(x => x.weight);
        if (total <= 0) return true;
        var r = IRandom.Instance.Next(total);
        foreach (var (type, weight) in list)
        {
            if (r < weight)
            {
                Crash(type);
                break;
            }
            r -= weight;
        }
        return true;
    }

    static bool AnySabotageActive()
        => new[] { SystemTypes.Electrical, SystemTypes.MushroomMixupSabotage, SystemTypes.Comms,
            SystemTypes.Reactor, SystemTypes.Laboratory, SystemTypes.HeliSabotage, SystemTypes.LifeSupp }
            .Any(Utils.IsActive);

    /// <summary>タスククラッシュ(サボタージュを起こす)</summary>
    void Crash(SystemTypes type)
    {
        Logger.Info($"{Player.Data.GetLogPlayerName()} のタスククラッシュ => {type}", "MadBreaker");
        var ship = ShipStatus.Instance;
        switch (type)
        {
            case SystemTypes.Electrical:
                //壊すスイッチ(1つ以上)をランダムに選ぶ
                var mask = (byte)IRandom.Instance.Next(1, 32);
                ship.RpcUpdateSystem(SystemTypes.Electrical, (byte)(SwitchSystem.DamageSystem | mask));
                break;
            case SystemTypes.MushroomMixupSabotage:
                ship.RpcUpdateSystem(SystemTypes.MushroomMixupSabotage, (byte)MushroomMixupSabotageSystem.Operation.TriggerSabotage);
                break;
            default:
                //リアクター(ポーラスは耐震、エアシップは衝突回避)・通信障害
                ship.RpcUpdateSystem(type, 128);
                break;
        }
        //サボタージュが起きたことを記録
        Main.SabotageType = type;
        if (!Main.IsActiveSabotage)
            UtilsGameLog.AddGameLog("Sabotage", string.Format(GetString("Log.Sabotage"), UtilsName.GetPlayerColor(Player, false), GetString($"sb.{type}")));
        Main.IsActiveSabotage = true;
        Main.LastSab = Player.PlayerId;
        Main.SabotageActivetimer = 0;
    }
}

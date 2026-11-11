using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

/// <summary>
/// ウェルダー(シェイプシフター置き換え)<br/>
/// ・シェイプシフトの画面で誰かを選ぶと「溶接」開始(見た目は変わらない)。もう一度選ぶと解除<br/>
/// ・溶接中は閉まっている扉が開かない(溶接中に新しく閉まった扉も)。自分は動けず、キルもできない<br/>
/// ・最大時間が来るか、会議が始まるか、死んだら自動で解除。解除したらクールダウン
/// </summary>
public sealed class Welder : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Welder),
            player => new Welder(player),
            CustomRoles.Welder,
            () => RoleTypes.Shapeshifter,
            CustomRoleTypes.Impostor,
            5600,
            SetupOptionItem,
            "wel",
            OptionSort: (7, 7),
            from: From.TownOfHost_IntheMaking
        );
    public Welder(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        welding = false;
        weldedDoors.Clear();
    }

    static OptionItem Cooldown;
    static OptionItem MaxTime;
    enum OptionName
    {
        WelderMaxTime
    }
    static void SetupOptionItem()
    {
        Cooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, new(0.5f, 180f, 0.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        MaxTime = FloatOptionItem.Create(RoleInfo, 11, OptionName.WelderMaxTime, new(0.5f, 180f, 0.5f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }

    bool welding;
    float timer;
    float speedBefore;
    /// <summary>溶接している扉のId</summary>
    static readonly HashSet<int> weldedDoors = new();

    /// <summary>誰かが溶接中か(扉を開けられなくする)</summary>
    public static bool IsWelding => CustomRoleManager.AllActiveRoles.Values.Any(r => r is Welder w && w.welding);

    public override void ApplyGameOptions(IGameOptions opt)
    {
        //溶接中はすぐ押せるように(押すと解除)、それ以外は設定のクールダウン
        AURoleOptions.ShapeshifterCooldown = welding ? 0f : Cooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    public override bool CheckShapeshift(PlayerControl target, ref bool animate)
    {
        animate = false;
        if (welding) StopWeld();
        else StartWeld();
        return false;//見た目は変えない
    }

    void StartWeld()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive() || GameStates.IsMeeting) return;
        welding = true;
        timer = MaxTime.GetFloat();
        speedBefore = Main.AllPlayerSpeed[Player.PlayerId];
        Main.AllPlayerSpeed[Player.PlayerId] = Main.MinSpeed;
        UpdateDoors();
        Logger.Info($"{Player.Data.GetLogPlayerName()} が溶接開始", "Welder");
        //ボタンをすぐ押せるようにする(もう一度押すと解除)
        Player.RpcResetAbilityCooldown(Sync: true);
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }
    void StopWeld()
    {
        if (!welding) return;
        welding = false;
        if (!IsWelding) weldedDoors.Clear();
        if (!AmongUsClient.Instance.AmHost) return;
        Main.AllPlayerSpeed[Player.PlayerId] = speedBefore;
        Logger.Info($"{Player.Data.GetLogPlayerName()} の溶接を解除", "Welder");
        if (GameStates.IsMeeting || !Player.IsAlive())
        {
            Player.MarkDirtySettings();
            return;
        }
        //解除したらクールダウン
        Player.RpcResetAbilityCooldown(Sync: true);
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || !welding) return;
        if (!Player.IsAlive() || !GameStates.IsInTask)
        {
            StopWeld();
            return;
        }
        timer -= Time.fixedDeltaTime;
        if (timer <= 0)
        {
            StopWeld();
            return;
        }
        UpdateDoors();
    }

    /// <summary>閉まっている扉を溶接に加え、溶接した扉が開いていたら閉め直す</summary>
    static void UpdateDoors()
    {
        var ship = ShipStatus.Instance;
        if (ship == null || ship.AllDoors == null) return;
        var changed = false;
        foreach (var door in ship.AllDoors)
        {
            if (door == null) continue;
            //エアシップのラウンジのトイレ・ポーラスの除染室は対象外
            if (door.Room is SystemTypes.Lounge or SystemTypes.Decontamination) continue;
            if (!door.IsOpen) weldedDoors.Add(door.Id);
            else if (weldedDoors.Contains(door.Id))
            {
                door.SetDoorway(false);
                changed = true;
            }
        }
        if (changed && ship.Systems.TryGetValue(SystemTypes.Doors, out var system))
        {
            var doors = system.TryCast<DoorsSystemType>();
            if (doors != null) doors.IsDirty = true;
        }
    }

    public override void OnStartMeeting() => StopWeld();
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target) => StopWeld();

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        //溶接中はキルできない
        if (welding) info.DoKill = false;
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        if (isForMeeting || (seen != null && seen != seer) || !Is(seer) || !welding) return "";
        return Utils.ColorString(Palette.ImpostorRed, "溶接中（もう一度シェイプシフトで解除）");
    }
    public override string GetAbilityButtonText() => welding ? "解除" : "溶接";
}

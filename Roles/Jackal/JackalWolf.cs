using System.Collections.Generic;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Neutral;

/// <summary>
/// ジャッカルウルフ: 設定で選んだインポスター役職の能力を持つジャッカル。<br/>
/// 中身は JackalImpostorBase(ジャッカル〇〇と共通)。
/// </summary>
public sealed class JackalWolf : JackalImpostorBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalWolf),
            player => new JackalWolf(player),
            CustomRoles.JackalWolf,
            () => OptionHaveRole.GetRole().GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            24700,
            SetupOptionItem,
            "Jaw",
            "#00b4eb",
            (1, 3),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalWolf, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            AddHaveRole: () => OptionHaveRole.GetRole(),
            from: From.TownOfHost_K);
    public JackalWolf(PlayerControl player)
    : base(RoleInfo, player)
    { }
    public static FilterOptionItem OptionHaveRole;

    static CustomRoles[] InvalidRoles()
    {
        List<CustomRoles> InvalidRoles = new();
        foreach (var data in CustomRoleManager.AllRolesInfo)
        {
            if (data.Key.IsImpostor() is false && data.Key is not CustomRoles.NotAssigned) continue;
            if (data.Key is CustomRoles.Limiter or CustomRoles.Assassin)
            {
                InvalidRoles.Add(data.Key);
                continue;
            }
        }
        return InvalidRoles.ToArray();
    }
    enum OptionName
    {
        AssassinHaveRole
    }
    public static void SetupOptionItem()
    {
        OptionHaveRole = FilterOptionItem.Create(RoleInfo, 17, OptionName.AssassinHaveRole, 0, false, null, true, false, false, false, false, () => InvalidRoles());
        SetupJackalOptions(RoleInfo);
    }
    protected override CustomRoles GetHaveRole() => OptionHaveRole.GetBool() ? OptionHaveRole.GetRole() : CustomRoles.Impostor;
    //役職名に持っている役職が入っていないので、後ろに小さく出す
    protected override bool ShowHaveRoleInName => true;
}

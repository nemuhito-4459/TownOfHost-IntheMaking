using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

public sealed class Opportunist : RoleBase, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Opportunist),
            player => new Opportunist(player),
            CustomRoles.Opportunist,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            15200,
            null,
            "op",
            "#00ff00",
            (6, 2),
            from: From.TOR_GM_Edition
        );
    public Opportunist(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        timer = 0;
        pos = new(0, 0);
    }
    float timer; Vector2 pos;
    public bool CheckWin(ref CustomRoles winnerRole)
    {
        if (Player.IsAlive())
        {
            return true;
        }
        return false;
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            var nowpos = player.GetTruePosition();
            if (nowpos == pos)
            {
                timer += Time.fixedDeltaTime;
            }
            pos = nowpos;
        }
    }
    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if ((seen == seer) && Player.IsAlive()) return Utils.AdditionalAliveWinnerMark;
        return "";
    }
}

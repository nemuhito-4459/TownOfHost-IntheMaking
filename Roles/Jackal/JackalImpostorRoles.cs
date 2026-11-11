using AmongUs.GameOptions;

using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Neutral;

// ジャッカル〇〇: インポスター役職の能力を持つジャッカル(中身は JackalImpostorBase)。
// 能力の設定は元のインポスター役職の設定をそのまま使い、ジャッカルとしての設定(ベント・サボ・視界など)は役職ごと。
// 設定IDは 30000 + 100刻み。新しく足すときは、ここに同じ形で1つ書き、CustomRoles と翻訳に名前を足す。

public sealed class JackalBountyHunter : JackalImpostorBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalBountyHunter),
            player => new JackalBountyHunter(player),
            CustomRoles.JackalBountyHunter,
            () => CustomRoles.BountyHunter.GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            30500,
            () => SetupJackalOptions(RoleInfo),
            "jbountyhu",
            "#00b4eb",
            (1, 15),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalBountyHunter, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            AddHaveRole: () => CustomRoles.BountyHunter,
            from: From.TownOfHost_IntheMaking);
    public JackalBountyHunter(PlayerControl player) : base(RoleInfo, player) { }
    protected override CustomRoles GetHaveRole() => CustomRoles.BountyHunter;
}

public sealed class JackalEraser : JackalImpostorBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalEraser),
            player => new JackalEraser(player),
            CustomRoles.JackalEraser,
            () => CustomRoles.Eraser.GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            30900,
            () => SetupJackalOptions(RoleInfo),
            "jeraser",
            "#00b4eb",
            (1, 19),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalEraser, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            AddHaveRole: () => CustomRoles.Eraser,
            from: From.TownOfHost_IntheMaking);
    public JackalEraser(PlayerControl player) : base(RoleInfo, player) { }
    protected override CustomRoles GetHaveRole() => CustomRoles.Eraser;
}

public sealed class JackalEvilButtoner : JackalImpostorBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalEvilButtoner),
            player => new JackalEvilButtoner(player),
            CustomRoles.JackalEvilButtoner,
            () => CustomRoles.EvilButtoner.GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            31000,
            () => SetupJackalOptions(RoleInfo),
            "jevilbutt",
            "#00b4eb",
            (1, 20),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalEvilButtoner, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            AddHaveRole: () => CustomRoles.EvilButtoner,
            from: From.TownOfHost_IntheMaking);
    public JackalEvilButtoner(PlayerControl player) : base(RoleInfo, player) { }
    protected override CustomRoles GetHaveRole() => CustomRoles.EvilButtoner;
}

public sealed class JackalEvilTeller : JackalImpostorBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalEvilTeller),
            player => new JackalEvilTeller(player),
            CustomRoles.JackalEvilTeller,
            () => CustomRoles.EvilTeller.GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            31100,
            () => SetupJackalOptions(RoleInfo),
            "jeviltell",
            "#00b4eb",
            (1, 21),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalEvilTeller, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            AddHaveRole: () => CustomRoles.EvilTeller,
            from: From.TownOfHost_IntheMaking);
    public JackalEvilTeller(PlayerControl player) : base(RoleInfo, player) { }
    protected override CustomRoles GetHaveRole() => CustomRoles.EvilTeller;
}

public sealed class JackalEvilgambler : JackalImpostorBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalEvilgambler),
            player => new JackalEvilgambler(player),
            CustomRoles.JackalEvilgambler,
            () => CustomRoles.Evilgambler.GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            31200,
            () => SetupJackalOptions(RoleInfo),
            "jevilgamb",
            "#00b4eb",
            (1, 22),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalEvilgambler, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            AddHaveRole: () => CustomRoles.Evilgambler,
            from: From.TownOfHost_IntheMaking);
    public JackalEvilgambler(PlayerControl player) : base(RoleInfo, player) { }
    protected override CustomRoles GetHaveRole() => CustomRoles.Evilgambler;
}

public sealed class JackalInsider : JackalImpostorBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalInsider),
            player => new JackalInsider(player),
            CustomRoles.JackalInsider,
            () => CustomRoles.Insider.GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            31300,
            () => SetupJackalOptions(RoleInfo),
            "jinsider",
            "#00b4eb",
            (1, 23),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalInsider, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            AddHaveRole: () => CustomRoles.Insider,
            from: From.TownOfHost_IntheMaking);
    public JackalInsider(PlayerControl player) : base(RoleInfo, player) { }
    protected override CustomRoles GetHaveRole() => CustomRoles.Insider;
}

public sealed class JackalPenguin : JackalImpostorBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalPenguin),
            player => new JackalPenguin(player),
            CustomRoles.JackalPenguin,
            () => CustomRoles.Penguin.GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            31800,
            () => SetupJackalOptions(RoleInfo),
            "jpenguin",
            "#00b4eb",
            (1, 28),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalPenguin, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            AddHaveRole: () => CustomRoles.Penguin,
            from: From.TownOfHost_IntheMaking);
    public JackalPenguin(PlayerControl player) : base(RoleInfo, player) { }
    protected override CustomRoles GetHaveRole() => CustomRoles.Penguin;
}

public sealed class JackalProgressKiller : JackalImpostorBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalProgressKiller),
            player => new JackalProgressKiller(player),
            CustomRoles.JackalProgressKiller,
            () => CustomRoles.ProgressKiller.GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            31900,
            () => SetupJackalOptions(RoleInfo),
            "jprogress",
            "#00b4eb",
            (1, 29),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalProgressKiller, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            AddHaveRole: () => CustomRoles.ProgressKiller,
            from: From.TownOfHost_IntheMaking);
    public JackalProgressKiller(PlayerControl player) : base(RoleInfo, player) { }
    protected override CustomRoles GetHaveRole() => CustomRoles.ProgressKiller;
}

public sealed class JackalPuppeteer : JackalImpostorBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalPuppeteer),
            player => new JackalPuppeteer(player),
            CustomRoles.JackalPuppeteer,
            () => CustomRoles.Puppeteer.GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            32000,
            () => SetupJackalOptions(RoleInfo),
            "jpuppetee",
            "#00b4eb",
            (1, 30),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalPuppeteer, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            AddHaveRole: () => CustomRoles.Puppeteer,
            from: From.TownOfHost_IntheMaking);
    public JackalPuppeteer(PlayerControl player) : base(RoleInfo, player) { }
    protected override CustomRoles GetHaveRole() => CustomRoles.Puppeteer;
}

public sealed class JackalQuickKiller : JackalImpostorBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalQuickKiller),
            player => new JackalQuickKiller(player),
            CustomRoles.JackalQuickKiller,
            () => CustomRoles.QuickKiller.GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            32100,
            () => SetupJackalOptions(RoleInfo),
            "jquickkil",
            "#00b4eb",
            (1, 31),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalQuickKiller, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            AddHaveRole: () => CustomRoles.QuickKiller,
            from: From.TownOfHost_IntheMaking);
    public JackalQuickKiller(PlayerControl player) : base(RoleInfo, player) { }
    protected override CustomRoles GetHaveRole() => CustomRoles.QuickKiller;
}

public sealed class JackalSerialKiller : JackalImpostorBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalSerialKiller),
            player => new JackalSerialKiller(player),
            CustomRoles.JackalSerialKiller,
            () => CustomRoles.SerialKiller.GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            32300,
            () => SetupJackalOptions(RoleInfo),
            "jserialki",
            "#00b4eb",
            (1, 33),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalSerialKiller, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            AddHaveRole: () => CustomRoles.SerialKiller,
            from: From.TownOfHost_IntheMaking);
    public JackalSerialKiller(PlayerControl player) : base(RoleInfo, player) { }
    protected override CustomRoles GetHaveRole() => CustomRoles.SerialKiller;
}

public sealed class JackalSpeedStar : JackalImpostorBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalSpeedStar),
            player => new JackalSpeedStar(player),
            CustomRoles.JackalSpeedStar,
            () => CustomRoles.SpeedStar.GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            32500,
            () => SetupJackalOptions(RoleInfo),
            "jspeedsta",
            "#00b4eb",
            (1, 35),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalSpeedStar, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            AddHaveRole: () => CustomRoles.SpeedStar,
            from: From.TownOfHost_IntheMaking);
    public JackalSpeedStar(PlayerControl player) : base(RoleInfo, player) { }
    protected override CustomRoles GetHaveRole() => CustomRoles.SpeedStar;
}

public sealed class JackalStealth : JackalImpostorBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalStealth),
            player => new JackalStealth(player),
            CustomRoles.JackalStealth,
            () => CustomRoles.Stealth.GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            32600,
            () => SetupJackalOptions(RoleInfo),
            "jstealth",
            "#00b4eb",
            (1, 36),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalStealth, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            AddHaveRole: () => CustomRoles.Stealth,
            from: From.TownOfHost_IntheMaking);
    public JackalStealth(PlayerControl player) : base(RoleInfo, player) { }
    protected override CustomRoles GetHaveRole() => CustomRoles.Stealth;
}

public sealed class JackalVampire : JackalImpostorBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalVampire),
            player => new JackalVampire(player),
            CustomRoles.JackalVampire,
            () => CustomRoles.Vampire.GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            32900,
            () => SetupJackalOptions(RoleInfo),
            "jvampire",
            "#00b4eb",
            (1, 39),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalVampire, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            AddHaveRole: () => CustomRoles.Vampire,
            from: From.TownOfHost_IntheMaking);
    public JackalVampire(PlayerControl player) : base(RoleInfo, player) { }
    protected override CustomRoles GetHaveRole() => CustomRoles.Vampire;
}

public sealed class JackalWarlock : JackalImpostorBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalWarlock),
            player => new JackalWarlock(player),
            CustomRoles.JackalWarlock,
            () => CustomRoles.Warlock.GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            33000,
            () => SetupJackalOptions(RoleInfo),
            "jwarlock",
            "#00b4eb",
            (1, 40),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalWarlock, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            AddHaveRole: () => CustomRoles.Warlock,
            from: From.TownOfHost_IntheMaking);
    public JackalWarlock(PlayerControl player) : base(RoleInfo, player) { }
    protected override CustomRoles GetHaveRole() => CustomRoles.Warlock;
}

public sealed class JackalWitch : JackalImpostorBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalWitch),
            player => new JackalWitch(player),
            CustomRoles.JackalWitch,
            () => CustomRoles.Witch.GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            33100,
            () => SetupJackalOptions(RoleInfo),
            "jwitch",
            "#00b4eb",
            (1, 41),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalWitch, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            AddHaveRole: () => CustomRoles.Witch,
            from: From.TownOfHost_IntheMaking);
    public JackalWitch(PlayerControl player) : base(RoleInfo, player) { }
    protected override CustomRoles GetHaveRole() => CustomRoles.Witch;
}

using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;

using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using Hazel;
using static TownOfHost.Modules.SelfVoteManager;

namespace TownOfHost.Roles.Impostor;

/// <summary>
/// イビルテラー: 会議中にジャッジの判決(または自投票→投票)で相手を占う。<br/>
/// 結果は陣営(設定で役職まで)で、占った相手の名前の上にも表示される
/// </summary>
public sealed class EvilTeller : RoleBase, IImpostor, ISelfVoter
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(EvilTeller),
            player => new EvilTeller(player),
            CustomRoles.EvilTeller,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            3700,
            SetUpOptionItem,
            "Et",
            OptionSort: (2, 6),
            from: From.TownOfHost_K
        );
    public EvilTeller(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        seentarget.Clear();
        killcooldown = optkillcooldown.GetFloat();
        tellroleteam = opttellroleteam.GetBool();
        tellrole = opttellrole.GetBool();
        maxtellcount = optCanTellCount.GetInt();
    }
    static OptionItem optkillcooldown;
    static OptionItem opttellroleteam;
    static OptionItem opttellrole;
    static OptionItem optCanTellCount;
    static float killcooldown;
    /// <summary>true: 役職まで分かる / false: 陣営だけ分かる</summary>
    static bool tellroleteam;
    /// <summary>true: 占ったときではなく、今の役職で表示する</summary>
    static bool tellrole;
    static int maxtellcount;
    /// <summary>占った相手と、そのときの役職</summary>
    Dictionary<byte, CustomRoles> seentarget = new();
    enum OptionName { EvilTellertellrole, EvilTellerCanTellCount }

    static void SetUpOptionItem()
    {
        //IDは前と同じ(保存値を引き継ぐ)。クールダウン・占う時間・距離・キルクールのリセットの設定は廃止
        optkillcooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 30f, false).SetValueFormat(OptionFormat.Seconds);
        optCanTellCount = IntegerOptionItem.Create(RoleInfo, 17, OptionName.EvilTellerCanTellCount, new(1, 99, 1), 3, false);
        opttellroleteam = BooleanOptionItem.Create(RoleInfo, 14, "TellRole", false, false);
        opttellrole = BooleanOptionItem.Create(RoleInfo, 15, OptionName.EvilTellertellrole, false, false);
    }
    public float CalculateKillCooldown() => killcooldown;

    int Remaining => maxtellcount - seentarget.Count;

    #region 会議中の能力(ジャッジの判決 / 自投票→投票)
    bool ISelfVoter.CanUseVoted() => Canuseability() && Remaining > 0;
    bool ISelfVoter.IsCantUse() => Remaining <= 0;

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Canuseability()) return true;
        if (!Is(voter) || Remaining <= 0) return true;

        if (CheckSelfVoteMode(Player, votedForId, out var status))
        {
            if (status is VoteStatus.Self)
                Utils.SendMessage(string.Format(GetString("SkillMode"), GetString("Mode.Divied"), GetString("Vote.Divied")) + GetString("VoteSkillMode"), Player.PlayerId);
            if (status is VoteStatus.Skip)
                Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
            if (status is VoteStatus.Vote)
                Tell(votedForId);
            SetMode(Player, status is VoteStatus.Self);
            return false;
        }
        return true;
    }

    void Tell(byte targetId)
    {
        var target = PlayerCatch.GetPlayerById(targetId);
        if (target == null || !target.IsAlive()) return;
        if (target.PlayerId == Player.PlayerId || target.IsTeammate(Player)) return;
        if (seentarget.ContainsKey(targetId) || Remaining <= 0) return;

        var role = target.GetCustomRole();
        seentarget[targetId] = role;
        RpcAddTarget(targetId);

        var result = tellroleteam
            ? "<b>" + GetString($"{role}").Color(UtilsRoleText.GetRoleColor(role)) + "</b>"
            : GetString($"{role.GetCustomRoleTypes()}");
        Utils.SendMessage(string.Format(GetString("Skill.Teller"), UtilsName.GetPlayerColor(target, true), result)
            + "\n\n" + string.Format(GetString("RemainingCount"), Remaining), Player.PlayerId);
        Logger.Info($"Player: {Player.name}, Target: {target.name}, Remaining: {Remaining}", "EvilTeller");
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }
    #endregion

    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        if (!seen) return;
        if (!Player.IsAlive()) return;
        if (!seentarget.TryGetValue(seen.PlayerId, out var role)) return;

        enabled = true;
        addon = false;
        if (tellrole) role = seen.GetCustomRole();
        if (tellroleteam)
        {
            roleText = GetString($"{role}");
            roleColor = UtilsRoleText.GetRoleColor(role);
            return;
        }
        //陣営だけ
        switch (role.GetCustomRoleTypes())
        {
            case CustomRoleTypes.Crewmate:
            case CustomRoleTypes.Madmate:
                roleColor = Palette.CrewmateBlue;
                roleText = GetString("Crewmate");
                break;
            case CustomRoleTypes.Impostor:
                roleColor = ModColors.ImpostorRed;
                roleText = GetString("Impostor");
                break;
            case CustomRoleTypes.Neutral:
                roleColor = ModColors.NeutralGray;
                roleText = GetString("Neutral");
                break;
        }
    }
    public override string GetProgressText(bool comms = false, bool GameLog = false) => Remaining <= 0 ? $"<color=#cccccc>({Remaining})</color>" : $"<color=#ff1919>({Remaining})</color>";

    public void RpcAddTarget(byte targetId)
    {
        using var sender = CreateSender();
        sender.Writer.Write(targetId);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        var targetId = reader.ReadByte();
        seentarget.TryAdd(targetId, PlayerCatch.GetPlayerById(targetId).GetCustomRole());
    }
}

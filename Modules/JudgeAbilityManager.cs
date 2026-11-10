using System.Collections.Generic;
using AmongUs.GameOptions;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost
{
    /// <summary>
    /// 会議中の能力を、バニラのジャッジの「判決(オーバールール)」で使えるようにする。<br/>
    /// ・会議を始める直前に、自投票で能力を使う役職(ISelfVoter)の人を、本人の画面だけジャッジにする<br/>
    /// ・ジャッジのタスク条件は、その人だけ0%にする(タスクを完了扱いにしないので、タスク勝ちにはならない)<br/>
    /// ・判決を押した相手を「自投票モードで投票した相手」として役職の処理に渡し、本来の判決は取り消す<br/>
    /// ・使ったら、もう一度ジャッジにして続けて選べるようにする(天秤の2人選びなど)<br/>
    /// ・会議が終わったら元の役職に戻す
    /// </summary>
    public static class JudgeAbilityManager
    {
        /// <summary>今の会議でジャッジにしている人</summary>
        public static HashSet<byte> JudgePlayers = new();

        /// <summary>会議を始める直前に呼ぶ(ホストのみ)</summary>
        public static void PrepareMeeting()
        {
            JudgePlayers.Clear();
            if (!AmongUsClient.Instance.AmHost) return;

            foreach (var pc in PlayerCatch.AllAlivePlayerControls)
            {
                if (pc.GetRoleClass() is not ISelfVoter selfVoter) continue;
                if (selfVoter.IsCantUse()) continue;
                JudgePlayers.Add(pc.PlayerId);
            }
            if (JudgePlayers.Count == 0) return;

            //タスク条件0%の設定を先に送ってから、ジャッジにする
            UtilsOption.SyncAllSettings();
            foreach (var id in JudgePlayers) AssignJudge(PlayerCatch.GetPlayerById(id));
        }

        static void AssignJudge(PlayerControl pc)
        {
            if (pc == null) return;
            var clientId = pc.GetClientId();
            if (clientId == -1) return;
            Logger.Info($"{pc.Data.GetLogPlayerName()}をジャッジにします", "JudgeAbility");
            pc.RpcSetRoleDesync(RoleTypes.Judge, clientId);
        }

        /// <summary>
        /// ジャッジの判決を受け取ったときに呼ぶ(MeetingHud.SetJudgeOverrule)。<br/>
        /// 能力として処理したら true(呼び出し側で本来の判決を取り消す)
        /// </summary>
        public static bool TryUse(PlayerControl voter, PlayerControl target)
        {
            if (voter == null || target == null) return false;
            if (!JudgePlayers.Contains(voter.PlayerId)) return false;
            if (voter.GetRoleClass() is not ISelfVoter || voter.GetRoleClass() is not RoleBase role) return false;

            Logger.Info($"{voter.Data.GetLogPlayerName()} → {target.Data.GetLogPlayerName()}", "JudgeAbility");
            if (target.PlayerId != voter.PlayerId)
            {
                //自投票で能力モードに入ってから、相手に投票したのと同じ扱いにする
                SelfVoteManager.SetMode(voter, true);
                role.CheckVoteAsVoter(target.PlayerId, voter);
            }

            //続けて選べるよう、少し待ってからもう一度ジャッジにする
            _ = new LateTask(() =>
            {
                if (!GameStates.IsMeeting || !voter.IsAlive()) return;
                if (voter.GetRoleClass() is not ISelfVoter selfVoter || selfVoter.IsCantUse()) return;
                AssignJudge(voter);
            }, 0.5f, "JudgeAbilityReassign", true);
            return true;
        }

        /// <summary>このプレイヤーのジャッジのタスク条件を0%にするか(PlayerGameOptionsSender)</summary>
        public static bool IsJudge(byte playerId) => JudgePlayers.Contains(playerId);

        /// <summary>会議が終わったら呼ぶ。生きている人は元の役職に戻す</summary>
        public static void OnMeetingEnd()
        {
            if (!AmongUsClient.Instance.AmHost || JudgePlayers.Count == 0) return;
            var ids = new List<byte>(JudgePlayers);
            JudgePlayers.Clear();
            _ = new LateTask(() =>
            {
                foreach (var id in ids)
                {
                    var pc = PlayerCatch.GetPlayerById(id);
                    if (pc == null || !pc.IsAlive()) continue;
                    var clientId = pc.GetClientId();
                    if (clientId == -1) continue;
                    var baseRole = pc.GetCustomRole().GetRoleInfo()?.BaseRoleType?.Invoke() ?? RoleTypes.Crewmate;
                    Logger.Info($"{pc.Data.GetLogPlayerName()}を{baseRole}に戻します", "JudgeAbility");
                    pc.RpcSetRoleDesync(baseRole, clientId);
                }
                UtilsOption.SyncAllSettings();
            }, 1.0f, "JudgeAbilityRestore", true);
        }
    }
}

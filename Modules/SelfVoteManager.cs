using System.Collections.Generic;
using System.Linq;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Madmate;
using static TownOfHost.Modules.MeetingVoteManager;

namespace TownOfHost.Modules
{
    public static class SelfVoteManager
    {
        ///<summary>
        ///MeetingVoteManagerのSkip
        ///</summary>
        public static byte SkipId = Skip;
        public static Dictionary<byte, bool> CheckVote = new();
        [Attributes.GameModuleInitializer]
        public static void Init()
        {
            CheckVote.Clear();
            RoomTaskAssign.AllRoomTasker.Clear();
        }
        public static void AddSelfVotes(PlayerControl player)
        {
            CheckVote.TryAdd(player.PlayerId, false);
        }

        public enum VoteStatus
        {
            Skip,
            Self,
            Vote,
        }
        ///<summary>
        /// 自投票モードのチェック
        ///</summary>
        ///<returns>自投票モードならtrueを返す</returns>
        /// <param name="status">投票のステータスを返す</param>
        public static bool CheckSelfVoteMode(PlayerControl player, byte id, out VoteStatus status)
        {
            Check(player);
            var mode = CheckVote[player.PlayerId];
            if (player.PlayerId == id)
            {
                status = VoteStatus.Self;
                CheckVote[player.PlayerId] = !mode;
                mode = !mode;
            }
            else if (Skip == id)
                status = VoteStatus.Skip;
            else
                status = VoteStatus.Vote;
            Logger.Info($"player: {Main.AllPlayerNames[player.PlayerId]} mode: {mode} status: {status}", "SelfVoteManager");
            return mode;
        }

        private static void Check(PlayerControl player)
        {
            if (!CheckVote.ContainsKey(player.PlayerId))
            {
                AddSelfVotes(player);
                Logger.Info($"×チェックに失敗 {player.PlayerId}を追加しました", "SelfVoteManager");
            }
        }

        public static void SetMode(PlayerControl player, bool mode)
            => CheckVote[player.PlayerId] = mode;

        /// <summary>
        /// チャットコマンド「/ab 番号」：投票しなくても、会議中の能力(自投票で使う能力)をその人に使う<br/>
        /// 「/ab」だけなら、生きている人の番号の一覧を出す
        /// </summary>
        public static void AbilityCommand(PlayerControl player, string[] args)
        {
            if (player == null) return;
            const string title = "<#ffd84a>【能力】</color>";
            if (!GameStates.IsMeeting)
            {
                Utils.SendMessage("会議中だけ使えます", player.PlayerId, title);
                return;
            }
            if (!player.IsAlive())
            {
                Utils.SendMessage("死んでいるので使えません", player.PlayerId, title);
                return;
            }
            if (player.GetRoleClass() is not ISelfVoter)
            {
                Utils.SendMessage("この役職には、会議中に人を選ぶ能力がありません", player.PlayerId, title);
                return;
            }
            if (args.Length < 2 || !byte.TryParse(args[1], out var id))
            {
                var list = string.Join("\n", PlayerCatch.AllAlivePlayerControls
                    .Where(pc => pc.PlayerId != player.PlayerId)
                    .Select(pc => $"{pc.PlayerId}：{UtilsName.GetPlayerColor(pc, true)}"));
                Utils.SendMessage($"「/ab 番号」で、その人に能力を使います\n\n{list}", player.PlayerId, title);
                return;
            }
            var target = PlayerCatch.GetPlayerById(id);
            if (target == null || !target.IsAlive() || id == player.PlayerId)
            {
                Utils.SendMessage($"{id}番の人は選べません（「/ab」で番号の一覧を見られます）", player.PlayerId, title);
                return;
            }
            //自投票で能力モードにしたのと同じ状態にしてから、その人に投票したことにする
            //(能力が使われると、役職の処理でモードが元に戻る。戻っていなければ使えなかった)
            SetMode(player, true);
            player.GetRoleClass().CheckVoteAsVoter(id, player);
            Check(player);
            if (CheckVote[player.PlayerId])
            {
                SetMode(player, false);
                Utils.SendMessage("今は能力を使えません（回数・タスク数・会議の状況を確認してください）", player.PlayerId, title);
            }
            Logger.Info($"{player.Data.GetLogPlayerName()} が /ab {id}", "AbilityCommand");
        }

        public static bool Canuseability()
        {
            if (MadAvenger.Skill) return false;
            if (Options.firstturnmeeting && Options.FirstTurnMeetingCantability.GetBool() && MeetingStates.FirstMeeting) return false;
            if (Assassin.NowUse) return false;

            return true;
        }

        public enum AbilityVoteMode
        {
            NomalVote,
            SelfVote,
        }
    }
}
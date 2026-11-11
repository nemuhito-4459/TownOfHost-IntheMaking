using System.Collections.Generic;
using System.Linq;
using Hazel;

using TownOfHost.Attributes;
using TownOfHost.Modules;
using TownOfHost.Patches;
using static TownOfHost.Utils;

namespace TownOfHost
{
    /// <summary>
    /// 死亡後の勝者予想<br/>
    /// ・死んだあとの最初の会議で、勝つと思う人に投票する(その会議が終わったら締め切り)<br/>
    /// ・死んだ人は本人の画面でだけ生きているように見せて、投票できるようにする<br/>
    /// ・予想の票は追放の集計には入れない。スキップに投票すると予想を取り消す<br/>
    /// ・ホストは投票できないので「/yosou 名前」で予想する<br/>
    /// ・当たるとレートが少し増える(RatingManager)
    /// </summary>
    public static class PredictionManager
    {
        /// <summary>予想した人 → 予想された人</summary>
        public static Dictionary<byte, byte> Predictions = new();
        /// <summary>今の会議で予想できる人</summary>
        static HashSet<byte> eligible = new();
        /// <summary>もう予想の会議が終わった人</summary>
        static HashSet<byte> used = new();
        /// <summary>本人の画面で生きているように見せた人</summary>
        static List<byte> desynced = new();

        [GameModuleInitializer]
        public static void Init()
        {
            Predictions = new();
            eligible = new();
            used = new();
            desynced = new();
        }

        static bool Enabled => Options.CurrentGameMode == CustomGameMode.Standard && !GameStates.IsFreePlay;

        /// <summary>会議が始まる直前(StartMeeting を送る前)に呼ぶ</summary>
        public static void BeforeMeetingStart()
        {
            eligible.Clear();
            desynced.Clear();
            if (!AmongUsClient.Instance.AmHost || !Enabled) return;
            if (Roles.Impostor.Assassin.NowUse) return;//アサシン会議は対象外(締め切りにも数えない)

            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                if (pc == null || pc.IsAlive() || used.Contains(pc.PlayerId)) continue;
                if (pc.Data == null || pc.Data.Disconnected) continue;
                if (pc.PlayerId == PlayerControl.LocalPlayer.PlayerId && Options.EnableGM.GetBool()) continue;
                eligible.Add(pc.PlayerId);
            }
            if (eligible.Count == 0) return;
            if (AntiBlackout.IsCached || AntiBlackout.IsSet) return;

            //本人の画面でだけ生きているように見せる
            foreach (var id in eligible)
            {
                var pc = PlayerCatch.GetPlayerById(id);
                if (pc == null || pc.PlayerId == PlayerControl.LocalPlayer.PlayerId || pc.IsModClient()) continue;
                var clientId = pc.GetClientId();
                if (clientId == -1) continue;
                pc.Data.IsDead = false;
                GameDataSerializePatch.SerializeMessageCount++;
                RPC.RpcSyncAllNetworkedPlayer(clientId, SendOption.Reliable);
                GameDataSerializePatch.SerializeMessageCount--;
                pc.Data.IsDead = true;
                desynced.Add(id);
            }
            Logger.Info($"予想できる人: {string.Join(",", eligible)}", "Prediction");
        }

        /// <summary>会議が始まったあとに案内を送る</summary>
        public static void OnMeetingStarted()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            foreach (var id in eligible)
            {
                var text = id == PlayerControl.LocalPlayer.PlayerId
                    ? "勝者予想ができます。\n「/yosou 名前」で勝つと思う人を選んでください（この会議の終わりまで）"
                    : "勝者予想ができます。\n勝つと思う人に投票してください（この会議の終わりまで）\nスキップに投票すると取り消し";
                SendMessage(text, id, "<#ffd84a>【勝者予想】</color>");
            }
        }

        /// <summary>CastVote の最初で呼ぶ。予想の票なら true(普通の投票として扱わない)</summary>
        public static bool TryPredict(MeetingHud meetingHud, byte voterId, byte targetId)
        {
            if (!eligible.Contains(voterId)) return false;
            var voter = PlayerCatch.GetPlayerById(voterId);
            if (voter == null || voter.IsAlive()) return false;

            if (targetId is MeetingVoteManager.Skip or MeetingVoteManager.NoVote || targetId == voterId)
            {
                Predictions.Remove(voterId);
                SendMessage("勝者予想を取り消しました", voterId, "<#ffd84a>【勝者予想】</color>");
            }
            else
            {
                Predictions[voterId] = targetId;
                var target = PlayerCatch.GetPlayerById(targetId);
                SendMessage($"{target?.Data?.GetLogPlayerName() ?? "???"} が勝つと予想しました\n（もう一度投票すると変えられます）", voterId, "<#ffd84a>【勝者予想】</color>");
            }
            Logger.Info($"{voter.Data.GetLogPlayerName()} の予想 => {targetId}", "Prediction");
            //投票済みの表示を消して、変えられるようにする
            meetingHud.RpcClearVote(voter.PlayerId);
            return true;
        }

        /// <summary>ホスト用「/yosou 名前」</summary>
        public static void HostCommand(string[] args)
        {
            var host = PlayerControl.LocalPlayer;
            if (!eligible.Contains(host.PlayerId) || !GameStates.IsMeeting)
            {
                SendMessage("今は勝者予想ができません", host.PlayerId);
                return;
            }
            var name = string.Join(" ", args.Skip(1)).Replace(" ", "").Replace("　", "");
            var target = PlayerCatch.AllPlayerControls.FirstOrDefault(pc => (pc.Data?.GetLogPlayerName() ?? "").RemoveHtmlTags().Replace(" ", "").Replace("　", "") == name);
            if (target == null)
            {
                Predictions.Remove(host.PlayerId);
                SendMessage(name == "" ? "勝者予想を取り消しました" : $"「{name}」という名前の人はいません", host.PlayerId);
                return;
            }
            Predictions[host.PlayerId] = target.PlayerId;
            SendMessage($"{target.Data.GetLogPlayerName()} が勝つと予想しました", host.PlayerId, "<#ffd84a>【勝者予想】</color>");
        }

        /// <summary>会議の終わり(投票の締め切り)に呼ぶ</summary>
        public static void OnMeetingEnd()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            used.UnionWith(eligible);
            eligible.Clear();
            if (desynced.Count == 0) return;
            //本人の画面の「生きている」を元に戻す(会議後の処理が落ち着いてから)
            var ids = desynced.ToArray();
            desynced.Clear();
            Resync(ids, 0);
        }
        static void Resync(byte[] ids, int retry)
        {
            _ = new LateTask(() =>
            {
                if ((GameStates.IsMeeting || AntiBlackout.IsCached || AntiBlackout.IsSet) && retry < 10)
                {
                    Resync(ids, retry + 1);
                    return;
                }
                if (!GameStates.InGame) return;
                foreach (var id in ids)
                {
                    var pc = PlayerCatch.GetPlayerById(id);
                    var clientId = pc?.GetClientId() ?? -1;
                    if (clientId == -1) continue;
                    GameDataSerializePatch.SerializeMessageCount++;
                    RPC.RpcSyncAllNetworkedPlayer(clientId, SendOption.Reliable);
                    GameDataSerializePatch.SerializeMessageCount--;
                }
            }, retry == 0 ? 12f : 2f, "PredictionResync", true);
        }
    }
}

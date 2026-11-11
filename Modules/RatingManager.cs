using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using TownOfHost.Attributes;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using static TownOfHost.Utils;

namespace TownOfHost
{
    /// <summary>
    /// レート<br/>
    /// ・フレンドコードごとに Rating.txt に保存(部屋に来た人全員分)<br/>
    /// ・初期値は B。1回でも試合を終えると A(0)になる<br/>
    /// ・A 0 / A+ 500 / S 1000 / S+ 1500 / 2000からは数字で表示<br/>
    /// ・勝つと増える(低いランクほど多く、長い試合ほど多く)。負けると一定量減る。0より下にはならない<br/>
    /// ・死亡後の勝者予想が当たると、勝ったときの増加量の1割が増える
    /// </summary>
    public static class RatingManager
    {
        // ====== 数値(ここを変えると調整できる) ======
        /*
         * 【勝って生き残った】
         *   基本レート = 試合時間 × 陣営ボーナス × ランク補正 × 基本値(勝ち40・負け25)
         *   クルーメイト         ＋ 人外に投票した回数 × 基本値 × タスク完了割合 × 1.5
         *   キルできる人外       ＋ キルした回数 × 基本値 × 0.6
         *   キルできない人外     ＋ 自分に賭けた人数 × 1.2
         *   マッドメイト         ＋ 勝ったインポスターが得た量の合計 × 0.1
         * 【勝って死んでいた】
         *   生存時間 ÷ 試合時間 × (賭けた人が勝ったなら、その人の基本レート)
         * 【負けた】
         *   － 基本レート × 0.75 (死んでいて賭けた人が勝ったなら、上の死亡時の分は足す)
         *   それでプラスになったときは、その2割だけ
         * 最後に小数点以下を四捨五入
         */
        /// <summary>基本値(勝ったとき)</summary>
        const float BaseValue = 40f;
        /// <summary>基本値(負けたとき)</summary>
        const float LoseBaseValue = 25f;
        /// <summary>試合時間の倍率。10分で1倍、20分で2倍…</summary>
        static float TimeRate(double minutes) => (float)(minutes / 10.0);
        /// <summary>ランク補正</summary>
        static float RankRate(int rating) => rating switch
        {
            < 1400 => 1.5f,  //A(Bも)
            < 1700 => 1.2f,  //A+
            < 1900 => 1.0f,  //S
            < 2000 => 0.8f,  //S+
            _ => 0.75f,      //2000～
        };
        /// <summary>クルーメイトのタスク完了割合にかける倍率</summary>
        const float TaskRate = 1.5f;
        /// <summary>キルできる人外の、1キルあたりの倍率</summary>
        const float KillRate = 0.6f;
        /// <summary>キルできない人外の、自分に賭けた人1人あたりの量</summary>
        const float BettorRate = 1.2f;
        /// <summary>生き残って勝ったマッドメイトが足す、インポスターが得た量の割合</summary>
        const float MadmateShareRate = 0.1f;
        /// <summary>負けたときに減る割合(基本レートの0.75倍)</summary>
        const float LoseRate = 0.75f;
        /// <summary>負けたのにプラスになったとき、もらえる割合</summary>
        const float LosePlusRate = 0.2f;
        /// <summary>最初のレート</summary>
        const int StartRating = 1000;
        /// <summary>陣営ごとの倍率(勝ったとき, 負けたとき)。勝ちの倍率が高い陣営は負けの倍率も高い</summary>
        static (float win, float lose) FactionRate(Faction f) => f switch
        {
            Faction.Crewmate => (1.0f, 1.0f),
            Faction.Neutral => (1.2f, 1.2f),
            Faction.Madmate => (1.2f, 1.2f),
            Faction.Impostor => (1.5f, 1.5f),
            Faction.Jackal => (1.5f, 1.5f),
            _ => (1.0f, 1.0f),
        };
        // ===========================================

        static readonly string FilePath = Main.BaseDirectory + "/Rating2.txt";
        /// <summary>最初のレートが0だったころのファイル。読むときに最初のレートの分を足す</summary>
        static readonly string OldFilePath = Main.BaseDirectory + "/Rating.txt";

        public enum Faction { Crewmate, Impostor, Madmate, Jackal, Neutral }
        static readonly string[] FactionNames = ["クルーメイト", "インポスター", "マッドメイト", "ジャッカル", "ニュートラル"];
        /// <summary>試合が終わったときの役職から陣営を決める</summary>
        static Faction GetFaction(byte id)
        {
            var role = PlayerState.GetByPlayerId(id)?.MainRole ?? CustomRoles.Crewmate;
            if (role.GetRoleInfo()?.CountType == CountTypes.Jackal) return Faction.Jackal;
            if (role.IsMadmate()) return Faction.Madmate;
            if (role.IsImpostor()) return Faction.Impostor;
            if (role.IsNeutral()) return Faction.Neutral;
            return Faction.Crewmate;
        }

        public class Data
        {
            public string FriendCode;
            public int Rating;
            public int Games;
            public int Wins;
            public string Name = "";
        }
        static Dictionary<string, Data> all;
        static Dictionary<string, Data> All
        {
            get
            {
                if (all != null) return all;
                all = new();
                try
                {
                    //新しいファイルがまだ無ければ、前のファイルを読んで最初のレートの分を足す
                    var old = !File.Exists(FilePath) && File.Exists(OldFilePath);
                    var path = old ? OldFilePath : FilePath;
                    if (File.Exists(path))
                        foreach (var line in File.ReadAllLines(path))
                        {
                            //フレンドコード,レート,試合数,勝利数,名前
                            var p = line.Split(',', 5);
                            if (p.Length < 4 || p[0].Trim() == "") continue;
                            all[p[0].Trim()] = new Data
                            {
                                FriendCode = p[0].Trim(),
                                Rating = (int.TryParse(p[1], out var r) ? r : 0) + (old ? StartRating : 0),
                                Games = int.TryParse(p[2], out var g) ? g : 0,
                                Wins = int.TryParse(p[3], out var w) ? w : 0,
                                Name = p.Length > 4 ? p[4] : "",
                            };
                        }
                }
                catch (Exception ex) { Logger.Error(ex.ToString(), "Rating"); }
                return all;
            }
        }
        static void Save()
        {
            try
            {
                Directory.CreateDirectory(Main.BaseDirectory);
                File.WriteAllLines(FilePath, All.Values.OrderByDescending(d => d.Rating)
                    .Select(d => $"{d.FriendCode},{d.Rating},{d.Games},{d.Wins},{d.Name.Replace("\n", " ").Replace("\r", "")}"));
            }
            catch (Exception ex) { Logger.Error(ex.ToString(), "Rating"); }
        }
        public static Data Get(string friendCode)
        {
            if (string.IsNullOrEmpty(friendCode)) return null;
            return All.TryGetValue(friendCode, out var d) ? d : null;
        }
        static Data GetOrCreate(string friendCode, string name)
        {
            if (!All.TryGetValue(friendCode, out var d))
                All[friendCode] = d = new Data { FriendCode = friendCode, Rating = StartRating };
            if (!string.IsNullOrEmpty(name)) d.Name = name;
            return d;
        }

        /// <summary>ランクの文字(色つき)</summary>
        /// <param name="prefix">ランクの前に付ける文字(ロビーの表示では「レート」)</param>
        public static string RankText(Data d, string prefix = "")
        {
            if (d == null || d.Games == 0) return $"<#9e9e9e>{prefix}B</color>";
            return d.Rating switch
            {
                < 1400 => $"<#5fd35f>{prefix}A</color>",
                < 1700 => $"<#3fd0ff>{prefix}A+</color>",
                < 1900 => $"<#ffd84a>{prefix}S</color>",
                < 2000 => $"<#ff8a3d>{prefix}S+</color>",
                _ => $"<#d27cff>{prefix}{d.Rating}</color>",
            };
        }
        public static string RankText(PlayerControl pc) => RankText(Get(pc?.GetClient()?.FriendCode));
        /// <summary>ロビーで名前の上に出す文字(「レートB」「レート3000」など)</summary>
        public static string LobbyRankText(PlayerControl pc) => RankText(Get(pc?.GetClient()?.FriendCode), "レート");

        // ------------------ 試合 ------------------
        /// <summary>試合に出ていた人(プレイヤーID → フレンドコード・名前)</summary>
        static Dictionary<byte, (string Code, string Name)> participants = new();
        /// <summary>死んだ時刻(生存の倍率に使う)</summary>
        static Dictionary<byte, DateTime> deathTimes = new();
        /// <summary>死んだとき(ホスト)。最初に死んだ時刻だけ記録する</summary>
        public static void OnDead(byte playerId)
        {
            if (!counting || deathTimes.ContainsKey(playerId)) return;
            deathTimes[playerId] = DateTime.Now;
        }
        /// <summary>人外(クルーメイト以外)に投票した回数</summary>
        static Dictionary<byte, int> badVotes = new();
        /// <summary>会議の投票が締め切られたとき(ホスト)。人外に投票した人を数える</summary>
        public static void OnMeetingVotes(IEnumerable<MeetingVoteManager.VoteData> votes)
        {
            if (!counting) return;
            foreach (var v in votes)
            {
                if (v == null || v.VotedFor >= 253) continue;
                var role = PlayerState.GetByPlayerId(v.VotedFor)?.MainRole ?? CustomRoles.NotAssigned;
                if (role is CustomRoles.NotAssigned || role.IsCrewmate()) continue;
                badVotes[v.Voter] = badVotes.GetValueOrDefault(v.Voter) + 1;
            }
        }
        static DateTime startTime;
        static bool counting;

        [GameModuleInitializer]
        public static void OnGameStart()
        {
            participants = new();
            deathTimes = new();
            badVotes = new();
            counting = false;
            if (!AmongUsClient.Instance.AmHost || GameStates.IsFreePlay) return;
            if (Options.CurrentGameMode != CustomGameMode.Standard) return;

            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                if (pc == null) continue;
                if (pc.PlayerId == PlayerControl.LocalPlayer.PlayerId && Options.EnableGM.GetBool()) continue;//GMは数えない
                if (pc.PlayerId == PlayerControl.LocalPlayer.PlayerId && GameStates.IsLocalGame) continue;//ローカルの部屋ではホストのレートを記録しない
                var code = pc.GetClient()?.FriendCode;
                if (string.IsNullOrEmpty(code)) continue;
                participants[pc.PlayerId] = (code, pc.Data.GetLogPlayerName().RemoveHtmlTags());
            }
            startTime = DateTime.Now;
            counting = true;
            Logger.Info($"レート対象: {participants.Count}人", "Rating");
        }

        /// <summary>試合の終わり(勝者が決まった後)に呼ぶ</summary>
        public static void OnGameEnd()
        {
            if (!AmongUsClient.Instance.AmHost || !counting) return;
            counting = false;
            //強制終了・全滅は数えない
            if (CustomWinnerHolder.WinnerTeam is CustomWinner.Draw or CustomWinner.None) return;

            var minutes = (DateTime.Now - startTime).TotalMinutes;
            var time = TimeRate(minutes);
            //試合前のレート(計算の途中で変わらないように先に控えておく)
            var ratingBefore = participants.ToDictionary(kv => kv.Key, kv => Get(kv.Value.Code)?.Rating ?? StartRating);
            //基本レート = 試合時間 × 陣営ボーナス × ランク補正 × 基本値
            float BaseRate(byte id, float baseValue = BaseValue)
            {
                var rating = ratingBefore.GetValueOrDefault(id);
                return time * FactionRate(GetFaction(id)).win * RankRate(rating) * baseValue;
            }
            //自分に賭けた人数
            int Bettors(byte id) => PredictionManager.Predictions.Count(kv => kv.Value == id && kv.Key != id);

            var results = new Dictionary<byte, (Data data, int before, int diff, bool win, Faction faction, string detail)>();
            //勝ったインポスターが得た量の合計(マッドメイトはこの1割を足す)。マッドメイトは最後に計算する
            var impostorGain = 0f;
            foreach (var (id, (code, name)) in participants.OrderBy(kv => GetFaction(kv.Key) == Faction.Madmate ? 1 : 0))
            {
                var d = GetOrCreate(code, name);
                var before = d.Rating;
                var win = Main.winnerList.Contains(id);
                var faction = GetFaction(id);
                var state = PlayerState.GetByPlayerId(id);
                var alive = state?.IsDead == false;
                var baseRate = BaseRate(id, win ? BaseValue : LoseBaseValue);

                //死んでいた人：生存時間 ÷ 試合時間 × (賭けた人が勝ったなら、その人の基本レート)
                var deadPart = 0f;
                var predictedWin = false;
                if (!alive && PredictionManager.Predictions.TryGetValue(id, out var predicted) && Main.winnerList.Contains(predicted))
                {
                    predictedWin = true;
                    var aliveRatio = deathTimes.TryGetValue(id, out var dt) && minutes > 0 ? Math.Clamp((dt - startTime).TotalMinutes / minutes, 0, 1) : 0;
                    deadPart = (float)aliveRatio * BaseRate(predicted);
                }

                float value;
                string detail;
                if (!win)
                {
                    value = -baseRate * LoseRate + deadPart;
                    detail = $"基本レート {baseRate:0.#} × 0.75 を失った";
                    //負けたのにプラスになったときは2割だけ
                    if (value > 0)
                    {
                        value *= LosePlusRate;
                        detail += $"\n負けたけどプラスなので2割だけ";
                    }
                }
                else if (!alive)
                {
                    value = deadPart;
                    detail = "死んでいたので、勝者予想の分だけ";
                }
                else
                {
                    value = baseRate;
                    detail = $"生き残った！ 基本レート {baseRate:0.#}";
                    if (faction == Faction.Crewmate)
                    {
                        //人外に投票した回数 × 基本値 × タスク完了割合 × 1.5
                        var votes = badVotes.GetValueOrDefault(id);
                        var ts = state?.GetTaskState();
                        var taskRatio = ts == null || ts.AllTasksCount <= 0 ? 0f : (float)ts.CompletedTasksCount / ts.AllTasksCount;
                        var add = votes * BaseValue * taskRatio * TaskRate;
                        value += add;
                        if (add > 0) detail += $"\n人外に投票 {votes}回 × タスク {taskRatio * 100:0}% × {TaskRate}（+{add:0.#}）";
                    }
                    else if (CanKill(id))
                    {
                        //キルした回数 × 基本値 × 0.6
                        var kills = PlayerState.AllPlayerStates.Values.Count(ps => ps.GetRealKiller() == id && ps.PlayerId != id);
                        var add = kills * BaseValue * KillRate;
                        value += add;
                        if (add > 0) detail += $"\nキル {kills}回（+{add:0.#}）";
                    }
                    else
                    {
                        //自分に賭けた人数 × 1.2
                        var bettors = Bettors(id);
                        var add = bettors * BettorRate;
                        value += add;
                        if (add > 0) detail += $"\n自分に賭けた人 {bettors}人（+{add:0.#}）";
                    }
                    if (faction == Faction.Madmate)
                    {
                        //勝ったインポスターが得た量 × 0.1
                        var add = impostorGain * MadmateShareRate;
                        value += add;
                        if (add > 0) detail += $"\nインポスターの1割（+{add:0.#}）";
                    }
                }
                if (predictedWin) detail += $"\n勝者予想が当たった！（+{deadPart:0.#}）";

                if (win && faction == Faction.Impostor && value > 0) impostorGain += value;
                var diff = (int)Math.Round(value, MidpointRounding.AwayFromZero);
                d.Rating = Math.Max(0, d.Rating + diff);
                d.Games++;
                if (win) d.Wins++;
                results[id] = (d, before, d.Rating - before, win, faction, detail);
                Logger.Info($"{name}: {before} → {d.Rating} ({faction} 勝ち:{win} 生存:{alive} 値:{value:0.00})", "Rating");
            }
            Save();

            //本人にだけ結果を送る(ロビーに戻ってから届く)
            foreach (var (id, r) in results)
            {
                if (PlayerCatch.GetPlayerById(id) == null) continue;//抜けた人には送らない
                var sb = new StringBuilder();
                sb.Append($"{FactionNames[(int)r.faction]}で{(r.win ? "勝利" : "敗北")}\n");
                sb.Append($"レート {r.before} → {r.data.Rating}（{(r.diff >= 0 ? "+" : "")}{r.diff}）\n");
                sb.Append($"ランク {RankText(r.data)}\n");
                sb.Append($"<size=80%>{r.detail}</size>");
                SendMessage(sb.ToString(), id, "<#ffd84a>【レート】</color>");
            }
        }

        /// <summary>キルできる役職か</summary>
        static bool CanKill(byte id)
        {
            var pc = PlayerCatch.GetPlayerById(id);
            if (pc?.GetRoleClass() is Roles.Core.Interfaces.IKiller killer) return killer.CanKill;
            var role = PlayerState.GetByPlayerId(id)?.MainRole ?? CustomRoles.Crewmate;
            return role.IsImpostor();
        }

        // ------------------ ロビーの表示 ------------------
        /// <summary>
        /// ロビーで名前の上にランクを出す(MOD未導入の人向けに名前を書き換えて送る)<br/>
        /// 前に送った名前と同じなら送らない。changed(入ってきた人など)が関わる分は必ず送る
        /// </summary>
        public static void SendLobbyNames(PlayerControl changed = null)
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsLobby || GameStates.IsCountDown) return;
            foreach (var target in PlayerCatch.AllPlayerControls)
            {
                if (target == null || target.name == "Player(Clone)") continue;
                if (target.PlayerId == PlayerControl.LocalPlayer.PlayerId) continue;//ホストの名前は ApplySuffix で付けている
                var name = target.Data?.PlayerName;
                if (string.IsNullOrEmpty(name)) continue;
                var text = LobbyName(name, LobbyRankText(target));
                foreach (var seer in PlayerCatch.AllPlayerControls)
                {
                    if (seer == null || seer.PlayerId == PlayerControl.LocalPlayer.PlayerId || seer.IsModClient()) continue;
                    if (seer.name == "Player(Clone)") continue;
                    var force = changed != null && (changed == seer || changed == target);
                    target.RpcSetNamePrivate(text, true, seer, force);
                }
            }
        }
        /// <summary>名前の上に小さくランクを乗せる(ホストのMOD名と同じ書き方)</summary>
        public static string LobbyName(string name, string rank)
            => "<line-height=-100%>\n</line-height>" + name + $"\n<line-height=-200%>\n<size=60%><line-height=0%>{rank}<line-height=100%></size>\n<size=0> ";

        /// <summary>ゲームが始まる前に、名前を元に戻す</summary>
        public static void RestoreLobbyNames()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            foreach (var target in PlayerCatch.AllPlayerControls)
            {
                if (target == null || target.PlayerId == PlayerControl.LocalPlayer.PlayerId) continue;
                var name = target.Data?.PlayerName;
                if (string.IsNullOrEmpty(name)) continue;
                foreach (var seer in PlayerCatch.AllPlayerControls)
                {
                    if (seer == null || seer.PlayerId == PlayerControl.LocalPlayer.PlayerId || seer.IsModClient()) continue;
                    target.RpcSetNamePrivate(name, true, seer);
                }
            }
        }

        // ------------------ コマンド ------------------
        public static string ShowText(PlayerControl pc)
        {
            var d = Get(pc?.GetClient()?.FriendCode);
            if (d == null || d.Games == 0) return $"{pc?.Data?.GetLogPlayerName()}：ランク {RankText(d)}（まだ試合をしていません）";
            return $"{pc.Data.GetLogPlayerName()}：ランク {RankText(d)}　レート {d.Rating}\n{d.Games}試合 {d.Wins}勝";
        }
        /// <summary>部屋にいる全員のレート</summary>
        public static string ShowAllText()
            => "レート一覧\n" + string.Join("\n", PlayerCatch.AllPlayerControls
                .Select(pc => (pc, d: Get(pc.GetClient()?.FriendCode)))
                .OrderByDescending(x => x.d?.Games > 0 ? x.d.Rating : -1)
                .Select(x => $"{RankText(x.d)} {x.pc.Data.GetLogPlayerName()}{(x.d?.Games > 0 ? $"（{x.d.Rating}）" : "")}"));
    }
}

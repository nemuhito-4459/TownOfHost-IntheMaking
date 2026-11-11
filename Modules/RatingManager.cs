using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using TownOfHost.Attributes;
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
        /// <summary>勝ったときの増加量(ランクごと、10分の試合での値)</summary>
        static int WinBase(int rating) => rating switch
        {
            < 500 => 40,   //A
            < 1000 => 30,  //A+
            < 1500 => 25,  //S
            < 2000 => 20,  //S+
            _ => 15,       //2000～
        };
        /// <summary>試合時間の倍率(勝ったときだけ)。10分で1倍、0.5～2倍</summary>
        static float TimeRate(double minutes) => Math.Clamp((float)(minutes / 10.0), 0.5f, 2f);
        /// <summary>負けたときの減少量</summary>
        const int LoseAmount = 20;
        /// <summary>勝者予想が当たったときの割合(勝ったときの増加量の1割)</summary>
        const float PredictionRate = 0.1f;
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

        static readonly string FilePath = Main.BaseDirectory + "/Rating.txt";

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
                    if (File.Exists(FilePath))
                        foreach (var line in File.ReadAllLines(FilePath))
                        {
                            //フレンドコード,レート,試合数,勝利数,名前
                            var p = line.Split(',', 5);
                            if (p.Length < 4 || p[0].Trim() == "") continue;
                            all[p[0].Trim()] = new Data
                            {
                                FriendCode = p[0].Trim(),
                                Rating = int.TryParse(p[1], out var r) ? r : 0,
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
                All[friendCode] = d = new Data { FriendCode = friendCode };
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
                < 500 => $"<#5fd35f>{prefix}A</color>",
                < 1000 => $"<#3fd0ff>{prefix}A+</color>",
                < 1500 => $"<#ffd84a>{prefix}S</color>",
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
        static DateTime startTime;
        static bool counting;

        [GameModuleInitializer]
        public static void OnGameStart()
        {
            participants = new();
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
            var results = new Dictionary<byte, (Data data, int before, int diff, bool win, int bonus, Faction faction)>();
            foreach (var (id, (code, name)) in participants)
            {
                var d = GetOrCreate(code, name);
                var before = d.Rating;
                var win = Main.winnerList.Contains(id);
                var faction = GetFaction(id);
                var (winRate, loseRate) = FactionRate(faction);
                var diff = win ? (int)Math.Round(WinBase(d.Rating) * TimeRate(minutes) * winRate) : -(int)Math.Round(LoseAmount * loseRate);

                //死亡後の勝者予想
                var bonus = 0;
                if (PredictionManager.Predictions.TryGetValue(id, out var predicted) && Main.winnerList.Contains(predicted))
                    bonus = Math.Max(1, (int)Math.Round(WinBase(d.Rating) * TimeRate(minutes) * PredictionRate));

                d.Rating = Math.Max(0, d.Rating + diff + bonus);
                d.Games++;
                if (win) d.Wins++;
                results[id] = (d, before, d.Rating - before, win, bonus, faction);
                Logger.Info($"{name}: {before} → {d.Rating} ({faction} 勝ち:{win} 予想:{bonus})", "Rating");
            }
            Save();

            //本人にだけ結果を送る(ロビーに戻ってから届く)
            foreach (var (id, r) in results)
            {
                if (PlayerCatch.GetPlayerById(id) == null) continue;//抜けた人には送らない
                var sb = new StringBuilder();
                sb.Append($"{FactionNames[(int)r.faction]}で{(r.win ? "勝利" : "敗北")}\n");
                sb.Append($"レート {r.before} → {r.data.Rating}（{(r.diff >= 0 ? "+" : "")}{r.diff}）\n");
                sb.Append($"ランク {RankText(r.data)}");
                if (r.bonus > 0) sb.Append($"\n勝者予想が当たった！（+{r.bonus}）");
                SendMessage(sb.ToString(), id, "<#ffd84a>【レート】</color>");
            }
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

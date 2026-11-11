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
    /// 肩書<br/>
    /// ・条件(タスクを10個終わらせる など)を満たすと肩書が手に入る。フレンドコードごとに Titles.txt に保存<br/>
    /// ・「/title 番号」で付けると、ロビーでランクの前に表示される<br/>
    /// ・数えるのはレートと同じ人(GM・ローカルの部屋のホスト・フレンドコードのない人は数えない)<br/>
    /// ・肩書を追加するときは <see cref="Titles"/> に足すだけでよい
    /// </summary>
    public static class TitleManager
    {
        /// <summary>記録している数</summary>
        public class Stats
        {
            public int Tasks;
            public int WalkTasks;
            public int Games;
            public int Wins;
            public int Predicts;
            /// <summary>役職ごとの勝利数(役職の内部名 → 回数)</summary>
            public Dictionary<string, int> RoleWins = new();
            public int RoleWin(CustomRoles role) => RoleWins.TryGetValue(role.ToString(), out var n) ? n : 0;
        }
        public class Data
        {
            public string FriendCode;
            public string Equipped = "";
            public HashSet<string> Unlocked = new();
            public Stats Stats = new();
        }
        /// <summary>肩書の定義</summary>
        public record Title(string Id, string Name, string Color, string Condition, Func<Stats, int> Progress, int Goal);

        /// <summary>肩書の一覧(この順で表示する)</summary>
        public static readonly List<Title> Titles = new()
        {
            //タスク
            new("task10", "見習い作業員", "#8fd3ff", "タスクを10個終わらせる", s => s.Tasks, 10),
            new("task100", "働き者", "#3fa9ff", "タスクを100個終わらせる", s => s.Tasks, 100),
            new("task500", "ブラック企業", "#1f6fff", "タスクを500個終わらせる", s => s.Tasks, 500),
            //ウォークタスク
            new("walk10", "お散歩好き", "#b4e86a", "ウォークタスクを10回終わらせる", s => s.WalkTasks, 10),
            new("walk100", "旅人", "#7cc63a", "ウォークタスクを100回終わらせる", s => s.WalkTasks, 100),
            //試合・勝利
            new("games10", "常連", "#d9d9d9", "10試合あそぶ", s => s.Games, 10),
            new("games100", "古参", "#bfbfbf", "100試合あそぶ", s => s.Games, 100),
            new("wins10", "勝負師", "#ffd84a", "10回勝つ", s => s.Wins, 10),
            new("wins50", "歴戦の猛者", "#ff9f1a", "50回勝つ", s => s.Wins, 50),
            //勝者予想
            new("predict1", "予言者", "#d27cff", "勝者予想を1回当てる", s => s.Predicts, 1),
            new("predict10", "大予言者", "#a64dff", "勝者予想を10回当てる", s => s.Predicts, 10),
            //役職で初勝利
            new("win_MadBreaker", "壊し屋", "#ff1919", "マッドブレイカーで初勝利", s => s.RoleWin(CustomRoles.MadBreaker), 1),
            new("win_Senrigan", "見通す者", "#8f7cff", "千里眼で初勝利", s => s.RoleWin(CustomRoles.Senrigan), 1),
            new("win_Welder", "溶接職人", "#ff1919", "ウェルダーで初勝利", s => s.RoleWin(CustomRoles.Welder), 1),
        };
        static Title Find(string id) => Titles.FirstOrDefault(t => t.Id == id);

        // ------------------ 保存 ------------------
        static readonly string FilePath = Main.BaseDirectory + "/Titles.txt";
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
                            //フレンドコード <tab> 付けている肩書 <tab> 手に入れた肩書(,区切り) <tab> 記録(名前=数;...)
                            var p = line.Split('\t');
                            if (p.Length < 4 || p[0].Trim() == "") continue;
                            var d = new Data { FriendCode = p[0].Trim(), Equipped = p[1].Trim() };
                            foreach (var id in p[2].Split(',', StringSplitOptions.RemoveEmptyEntries)) d.Unlocked.Add(id.Trim());
                            foreach (var kv in p[3].Split(';', StringSplitOptions.RemoveEmptyEntries))
                            {
                                var a = kv.Split('=');
                                if (a.Length != 2 || !int.TryParse(a[1], out var n)) continue;
                                switch (a[0])
                                {
                                    case "Tasks": d.Stats.Tasks = n; break;
                                    case "WalkTasks": d.Stats.WalkTasks = n; break;
                                    case "Games": d.Stats.Games = n; break;
                                    case "Wins": d.Stats.Wins = n; break;
                                    case "Predicts": d.Stats.Predicts = n; break;
                                    default:
                                        if (a[0].StartsWith("win.")) d.Stats.RoleWins[a[0][4..]] = n;
                                        break;
                                }
                            }
                            all[d.FriendCode] = d;
                        }
                }
                catch (Exception ex) { Logger.Error(ex.ToString(), "Title"); }
                return all;
            }
        }
        static void Save()
        {
            try
            {
                Directory.CreateDirectory(Main.BaseDirectory);
                File.WriteAllLines(FilePath, All.Values.Select(d =>
                {
                    var s = d.Stats;
                    var stats = $"Tasks={s.Tasks};WalkTasks={s.WalkTasks};Games={s.Games};Wins={s.Wins};Predicts={s.Predicts}"
                        + string.Concat(s.RoleWins.Select(kv => $";win.{kv.Key}={kv.Value}"));
                    return $"{d.FriendCode}\t{d.Equipped}\t{string.Join(",", d.Unlocked)}\t{stats}";
                }));
            }
            catch (Exception ex) { Logger.Error(ex.ToString(), "Title"); }
        }
        static Data Get(string friendCode)
        {
            if (string.IsNullOrEmpty(friendCode)) return null;
            return All.TryGetValue(friendCode, out var d) ? d : null;
        }
        static Data GetOrCreate(string friendCode)
        {
            if (!All.TryGetValue(friendCode, out var d))
                All[friendCode] = d = new Data { FriendCode = friendCode };
            return d;
        }

        // ------------------ 表示 ------------------
        /// <summary>付けている肩書(色つき、後ろに空白)。付けていなければ空</summary>
        public static string TitleText(PlayerControl pc)
        {
            var d = Get(pc?.GetClient()?.FriendCode);
            var t = d == null || d.Equipped == "" ? null : Find(d.Equipped);
            return t == null ? "" : $"<{t.Color}>{t.Name}</color> ";
        }

        // ------------------ 試合 ------------------
        /// <summary>試合に出ている人(プレイヤーID → フレンドコード)</summary>
        static Dictionary<byte, string> participants = new();
        /// <summary>この試合で終わらせた数</summary>
        static Dictionary<byte, int> tasks = new();
        static Dictionary<byte, int> walkTasks = new();
        static bool counting;

        [GameModuleInitializer]
        public static void OnGameStart()
        {
            participants = new();
            tasks = new();
            walkTasks = new();
            counting = false;
            if (!AmongUsClient.Instance.AmHost || GameStates.IsFreePlay) return;
            if (Options.CurrentGameMode != CustomGameMode.Standard) return;
            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                if (pc == null) continue;
                if (pc.PlayerId == PlayerControl.LocalPlayer.PlayerId && Options.EnableGM.GetBool()) continue;//GMは数えない
                if (pc.PlayerId == PlayerControl.LocalPlayer.PlayerId && GameStates.IsLocalGame) continue;//ローカルの部屋のホストは数えない
                var code = pc.GetClient()?.FriendCode;
                if (string.IsNullOrEmpty(code)) continue;
                participants[pc.PlayerId] = code;
            }
            counting = true;
        }

        /// <summary>タスクを1つ終わらせたとき(ホスト)</summary>
        public static void OnTask(byte playerId)
        {
            if (!counting || !participants.ContainsKey(playerId)) return;
            tasks[playerId] = tasks.GetValueOrDefault(playerId) + 1;
        }
        /// <summary>ウォークタスクを1回終わらせたとき(ホスト)</summary>
        public static void OnWalkTask(byte playerId)
        {
            if (!counting || !participants.ContainsKey(playerId)) return;
            walkTasks[playerId] = walkTasks.GetValueOrDefault(playerId) + 1;
        }

        /// <summary>試合の終わり(勝者が決まった後)に呼ぶ</summary>
        public static void OnGameEnd()
        {
            if (!AmongUsClient.Instance.AmHost || !counting) return;
            counting = false;
            //強制終了・全滅のときは、タスクの数だけ数える
            var finished = CustomWinnerHolder.WinnerTeam is not (CustomWinner.Draw or CustomWinner.None);

            foreach (var (id, code) in participants)
            {
                var d = GetOrCreate(code);
                var s = d.Stats;
                s.Tasks += tasks.GetValueOrDefault(id);
                s.WalkTasks += walkTasks.GetValueOrDefault(id);
                if (finished)
                {
                    s.Games++;
                    if (Main.winnerList.Contains(id))
                    {
                        s.Wins++;
                        var role = PlayerState.GetByPlayerId(id)?.MainRole ?? CustomRoles.NotAssigned;
                        if (role != CustomRoles.NotAssigned) s.RoleWins[role.ToString()] = s.RoleWin(role) + 1;
                    }
                    if (PredictionManager.Predictions.TryGetValue(id, out var predicted) && Main.winnerList.Contains(predicted))
                        s.Predicts++;
                }

                //新しく手に入れた肩書
                var got = Titles.Where(t => !d.Unlocked.Contains(t.Id) && t.Progress(s) >= t.Goal).ToList();
                foreach (var t in got) d.Unlocked.Add(t.Id);
                if (got.Count > 0 && PlayerCatch.GetPlayerById(id) != null)
                {
                    var text = string.Join("\n", got.Select(t => $"「<{t.Color}>{t.Name}</color>」（{t.Condition}）"))
                        + "\n\n「/title」で付けられます";
                    SendMessage(text, id, "<#ffd84a>【肩書を手に入れた！】</color>");
                    Logger.Info($"{code}: 肩書 {string.Join(",", got.Select(t => t.Id))}", "Title");
                }
            }
            Save();
        }

        // ------------------ コマンド ------------------
        /// <summary>
        /// 「/title」：手に入れた肩書の一覧<br/>
        /// 「/title 番号」：その肩書を付ける　「/title 0」：外す
        /// </summary>
        public static void Command(PlayerControl player, string[] args)
        {
            var code = player?.GetClient()?.FriendCode;
            if (string.IsNullOrEmpty(code))
            {
                SendMessage("フレンドコードがないので肩書は使えません", player.PlayerId);
                return;
            }
            var d = GetOrCreate(code);
            var mine = Titles.Where(t => d.Unlocked.Contains(t.Id)).ToList();

            if (args.Length >= 2 && int.TryParse(args[1], out var num))
            {
                if (num == 0)
                {
                    d.Equipped = "";
                    SendMessage("肩書を外しました", player.PlayerId, "<#ffd84a>【肩書】</color>");
                }
                else if (num < 1 || num > mine.Count)
                {
                    SendMessage($"{num}番の肩書はありません（「/title」で一覧を見られます）", player.PlayerId, "<#ffd84a>【肩書】</color>");
                    return;
                }
                else
                {
                    var t = mine[num - 1];
                    d.Equipped = t.Id;
                    SendMessage($"肩書「<{t.Color}>{t.Name}</color>」を付けました", player.PlayerId, "<#ffd84a>【肩書】</color>");
                }
                Save();
                RefreshName(player);
                return;
            }

            var sb = new StringBuilder();
            if (mine.Count == 0) sb.Append("まだ肩書を持っていません\n");
            else
            {
                sb.Append("「/title 番号」で付ける　「/title 0」で外す\n");
                for (var i = 0; i < mine.Count; i++)
                {
                    var t = mine[i];
                    sb.Append($"{i + 1}. <{t.Color}>{t.Name}</color>{(d.Equipped == t.Id ? "（付けている）" : "")}\n");
                }
            }
            //まだ手に入れていないもの(進み具合)
            var locked = Titles.Where(t => !d.Unlocked.Contains(t.Id)).ToList();
            if (locked.Count > 0)
            {
                sb.Append("\n<#9e9e9e>まだ手に入れていない肩書</color>\n");
                foreach (var t in locked)
                    sb.Append($"<#9e9e9e>・{t.Name}：{t.Condition}（{Math.Min(t.Progress(d.Stats), t.Goal)}/{t.Goal}）</color>\n");
            }
            SendMessage(sb.ToString().TrimEnd(), player.PlayerId, "<#ffd84a>【肩書】</color>");
        }

        /// <summary>ロビーの名前の表示を更新する</summary>
        static void RefreshName(PlayerControl player)
        {
            if (!GameStates.IsLobby) return;
            if (player.PlayerId == PlayerControl.LocalPlayer.PlayerId) ApplySuffix(null, force: true);
            else RatingManager.SendLobbyNames(player);
        }
    }
}

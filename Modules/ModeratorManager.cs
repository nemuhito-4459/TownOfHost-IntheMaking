using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using static TownOfHost.Utils;

namespace TownOfHost
{
    /// <summary>
    /// モデレーター(ホストの代わりに一部のコマンドを使える人)<br/>
    /// ホスト: /mod add 名前 ・ /mod del 名前 ・ /mod (一覧)<br/>
    /// モデレーター: /cmd /kick 名前 ・ /cmd /start ・ /cmd /stop<br/>
    /// フレンドコードで覚えるので、名前を変えてもモデレーターのまま
    /// </summary>
    public static class ModeratorManager
    {
        static readonly string FilePath = Main.BaseDirectory + "/Moderators.txt";
        /// <summary>(フレンドコード, 登録したときの名前)</summary>
        static List<(string FriendCode, string Name)> list;

        static List<(string FriendCode, string Name)> Mods
        {
            get
            {
                if (list != null) return list;
                list = new();
                try
                {
                    if (File.Exists(FilePath))
                        foreach (var line in File.ReadAllLines(FilePath))
                        {
                            var parts = line.Split(',', 2);
                            if (parts[0].Trim() == "") continue;
                            list.Add((parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : ""));
                        }
                }
                catch (Exception ex) { Logger.Error(ex.ToString(), "ModeratorManager"); }
                return list;
            }
        }
        static void Save()
        {
            try
            {
                Directory.CreateDirectory(Main.BaseDirectory);
                File.WriteAllLines(FilePath, Mods.Select(m => $"{m.FriendCode},{m.Name}"));
            }
            catch (Exception ex) { Logger.Error(ex.ToString(), "ModeratorManager"); }
        }

        public static bool IsModerator(PlayerControl pc)
        {
            if (pc == null) return false;
            if (pc.PlayerId == PlayerControl.LocalPlayer.PlayerId) return true;//ホストは何でもできる
            var code = pc.GetClient()?.FriendCode;
            return !string.IsNullOrEmpty(code) && Mods.Any(m => m.FriendCode == code);
        }

        static string Normalize(string name) => (name ?? "").RemoveHtmlTags().Replace(" ", "").Replace("　", "");
        static PlayerControl FindPlayer(string name)
        {
            var target = Normalize(name);
            return PlayerCatch.AllPlayerControls.FirstOrDefault(pc => Normalize(pc?.Data?.GetLogPlayerName()) == target);
        }
        static void ToHost(string text) => SendMessage(text, PlayerControl.LocalPlayer.PlayerId);

        /// <summary>ホストのコマンド「/mod」</summary>
        public static void HostCommand(string[] args)
        {
            var sub = args.Length > 1 ? args[1] : "";
            var name = args.Length > 2 ? string.Join(" ", args.Skip(2)) : "";
            switch (sub)
            {
                case "add":
                    {
                        var pc = FindPlayer(name);
                        var client = pc?.GetClient();
                        if (pc == null || client == null) { ToHost($"「{name}」という名前の人はいません"); return; }
                        if (pc.PlayerId == PlayerControl.LocalPlayer.PlayerId) { ToHost("ホストは最初から全部のコマンドを使えます"); return; }
                        if (string.IsNullOrEmpty(client.FriendCode)) { ToHost($"{name} のフレンドコードが取れないので登録できません"); return; }
                        if (Mods.Any(m => m.FriendCode == client.FriendCode)) { ToHost($"{name} はもうモデレーターです"); return; }
                        Mods.Add((client.FriendCode, pc.Data.GetLogPlayerName().RemoveHtmlTags()));
                        Save();
                        ToHost($"{name} をモデレーターにしました");
                        SendMessage("あなたはモデレーターになりました\n/cmd /kick 名前 … キック\n/cmd /start … ゲーム開始\n/cmd /stop … 開始を取り消す", pc.PlayerId);
                        return;
                    }
                case "del":
                    {
                        //部屋にいる人ならフレンドコードで、いなければ登録したときの名前で探す
                        var code = FindPlayer(name)?.GetClient()?.FriendCode;
                        var removed = Mods.RemoveAll(m => (!string.IsNullOrEmpty(code) && m.FriendCode == code) || Normalize(m.Name) == Normalize(name));
                        if (removed == 0) { ToHost($"「{name}」はモデレーターではありません"); return; }
                        Save();
                        ToHost($"{name} をモデレーターから外しました");
                        return;
                    }
                default:
                    ToHost(Mods.Count == 0
                        ? "モデレーターはいません\n/mod add 名前 で追加、/mod del 名前 で削除"
                        : "モデレーター\n" + string.Join("\n", Mods.Select(m => $"・{m.Name}")) + "\n/mod add 名前 で追加、/mod del 名前 で削除");
                    return;
            }
        }

        /// <summary>モデレーターのコマンド(/cmd /kick など)。モデレーターでなければ断る</summary>
        public static void ModeratorCommand(PlayerControl player, string[] args)
        {
            if (!AmongUsClient.Instance.AmHost || player == null) return;
            if (!IsModerator(player))
            {
                SendMessage("このコマンドはモデレーターだけが使えます", player.PlayerId);
                return;
            }
            var by = player.Data.GetLogPlayerName();
            switch (args[0])
            {
                case "/kick":
                    {
                        var name = args.Length > 1 ? string.Join(" ", args.Skip(1)) : "";
                        var target = FindPlayer(name);
                        if (target == null) { SendMessage($"「{name}」という名前の人はいません", player.PlayerId); return; }
                        if (target.PlayerId == PlayerControl.LocalPlayer.PlayerId || target.PlayerId == player.PlayerId)
                        {
                            SendMessage("その人はキックできません", player.PlayerId);
                            return;
                        }
                        var clientId = target.GetClientId();
                        if (clientId == -1) return;
                        Logger.Info($"{by} が {target.Data.GetLogPlayerName()} をキック", "Moderator");
                        SendMessage($"{by} が {target.Data.GetLogPlayerName()} をキックしました");
                        AmongUsClient.Instance.KickPlayer(clientId, false);
                        return;
                    }
                case "/start":
                    if (!GameStates.IsLobby || GameStartManager.Instance == null) return;
                    if (GameStartManager.Instance.startState != GameStartManager.StartingStates.NotStarting) return;//二重で開始しない
                    Logger.Info($"{by} がゲームを開始", "Moderator");
                    SendMessage($"{by} がゲームを開始しました");
                    GameStartManager.Instance.BeginGame();
                    return;
                case "/stop":
                    if (!GameStates.IsCountDown) return;
                    Logger.Info($"{by} が開始を取り消し", "Moderator");
                    SendMessage($"{by} がゲームの開始を取り消しました");
                    GameStartManager.Instance.ResetStartState();
                    return;
            }
        }
    }
}

using System.Linq;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
using static TownOfHost.Translator;

namespace TownOfHost
{
    /// <summary>
    /// 追放画面に「〇〇が追放された。(インポスター/クルーメイト/人外)はx人残っている」を出す<br/>
    /// MOD未導入者には追放される人の名前を書き換えて見せる(バニラの「{0}は追放された。」の{0}に入る)
    /// </summary>
    public static class ExileRemainText
    {
        public static OptionItem Option;
        enum Mode { Off, Impostor, Crewmate, NonCrew, All }
        static readonly string[] Modes = ["ExileRemainOff", "ExileRemainImpostor", "ExileRemainCrewmate", "ExileRemainNonCrew", "ExileRemainAll"];

        /// <summary>ホストの追放画面に出す文(空なら使わない)</summary>
        public static string Text = "";
        /// <summary>MOD未導入者に送った名前と、追放される人</summary>
        static string sentName;
        static PlayerControl sentTarget;

        public static void SetupOption(OptionItem parent)
        {
            Option = StringOptionItem.Create(111007, "ExileRemainCount", Modes, 1, TabGroup.MainSettings, false)
                .SetColorcode("#ff8a8a")
                .SetParent(parent);
        }

        /// <summary>
        /// 会議終了時に呼ぶ。追放画面の文を作って Text に入れ、MOD未導入者に送る名前を返す(使わないときは null)
        /// </summary>
        public static string Create(NetworkedPlayerInfo exiled)
        {
            Text = "";
            sentName = null;
            sentTarget = null;
            if (exiled == null || Option == null) return null;
            var mode = (Mode)Option.GetValue();
            if (mode == Mode.Off) return null;
            //アサシン会議は別の文を出すので触らない
            if (exiled.Object?.GetRoleClass() is Assassin && Assassin.NowUse) return null;

            //追放される人・会議後に死ぬ人を除いた生存者
            var alive = PlayerCatch.AllAlivePlayerControls
                .Where(pc => pc.PlayerId != exiled.PlayerId && !Main.AfterMeetingDeathPlayers.ContainsKey(pc.PlayerId))
                .Select(pc => pc.GetCustomRole())
                .ToArray();
            var impostor = alive.Count(role => role.IsImpostor());
            var crewmate = alive.Count(role => role.IsCrewmate());
            var nonCrew = alive.Length - crewmate;//人外 = クルーメイト陣営以外

            var lines = mode switch
            {
                Mode.Impostor => Line("ExileRemainImpostor", impostor),
                Mode.Crewmate => Line("ExileRemainCrewmate", crewmate),
                Mode.NonCrew => Line("ExileRemainNonCrew", nonCrew),
                _ => string.Join("\n", Line("ExileRemainImpostor", impostor), Line("ExileRemainCrewmate", crewmate), Line("ExileRemainNonCrew", nonCrew)),
            };
            var name = Main.AllPlayerNames.TryGetValue(exiled.PlayerId, out var n) ? n : exiled.PlayerName;
            Text = $"{string.Format(GetString("fortuihou"), name)}\n{lines}";
            //バニラ側の「は追放された。」は<size=0>で見えなくする
            sentName = $"{Text}<size=0>";
            sentTarget = exiled.Object;
            return sentName;

            static string Line(string label, int count) => string.Format(GetString("ExileRemainLine"), GetString(label), count);
        }

        /// <summary>
        /// 名前を送り直す。<br/>
        /// 会議終了の4秒後に AntiBlackout が GameData を丸ごと送ると、名前が元に戻ってしまう(追放画面はその後に出る)
        /// </summary>
        public static void Resend()
        {
            if (!AmongUsClient.Instance.AmHost || sentName == null || sentTarget == null) return;
            foreach (var player in PlayerCatch.AllPlayerControls)
            {
                if (player == null || player.PlayerId == PlayerControl.LocalPlayer.PlayerId || player.IsModClient()) continue;
                var clientId = player.GetClientId();
                if (clientId == -1) continue;
                sentTarget.SetNameCheck(sentName, player, true);
                //GameData のあとに確実に届くように Reliable で送る
                var sender = CustomRpcSender.Create("ExileRemainName", SendOption.Reliable);
                sender.StartMessage(clientId);
                sender.StartRpc(sentTarget.NetId, RpcCalls.SetName)
                    .Write(sentTarget.Data.NetId)
                    .Write(sentName)
                    .Write(true)
                    .EndRpc();
                sender.EndMessage();
                sender.SendMessage();
            }
        }
    }
}

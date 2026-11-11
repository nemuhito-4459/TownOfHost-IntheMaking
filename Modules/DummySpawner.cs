using System;
using System.Linq;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using static TownOfHost.Translator;

namespace TownOfHost
{
    /// <summary>
    /// ローカルでのテスト用のダミー(クローン)<br/>
    /// ・ローカルの部屋では、部屋番号の位置の「－ ダミー n ＋」で数(0～14)を選ぶ。選んだ数は保存され、次に部屋を作ったときも自動で入る<br/>
    /// ・ロビーで「/dummy」… 全部で15人になるまでダミーを入れる(「/dummy 5」なら5体)<br/>
    /// ・ダミーにも役職が付く(「/dummy 14 norole」なら付けない)<br/>
    /// ・ローカルゲームのホストだけ。デバッグ版でなくても使える
    /// </summary>
    public static class DummySpawner
    {
        /// <summary>/dummy で入れたダミーに役職を付けるか</summary>
        public static bool AssignRole = true;

        /// <summary>ダミーを count 体になるまで出す(最大14体 = 全部で15人)。出した数を返す</summary>
        public static int Spawn(int count)
        {
            var spawned = 0;
            for (var i = PlayerCatch.AllPlayerControls.Where(pc => pc.isDummy).Count();
            i < count; i++)
            {
                //使われていない一番小さいIDを使う(ダミーを減らしたあとでも重ならないように)
                var id = NextFreeId();
                if (id > 14) break;
                var dummy = Object.Instantiate(AmongUsClient.Instance.PlayerPrefab);
                dummy.isDummy = true;
                dummy.PlayerId = id;
                var playerinfo = GameData.Instance.AddDummy(dummy);
                var colorid = (byte)((PlayerControl.LocalPlayer.Data.DefaultOutfit.ColorId + id) % 18);

                dummy.NetTransform.enabled = true;
                dummy.RpcSetColor(colorid);
                dummy.RpcSetName(GetString(StringNames.Dummy) + (i + 1));
                dummy.GetComponent<DummyBehaviour>().enabled = true;
                dummy.isDummy = true;
                switch (colorid)
                {
                    case 11:
                        dummy.SetHat("hat_paws_panda", colorid);
                        dummy.SetVisor("visor_EyepatchL", colorid);
                        dummy.SetSkin("skin_Sanskin", colorid);
                        dummy.SetPet("pet_EnmptyPet");
                        break;
                    case 4:
                        dummy.SetHat("hat_cat_snow", colorid);
                        dummy.SetVisor("visor_Blush", colorid);
                        dummy.SetSkin("skin_Science", colorid);
                        dummy.SetPet("pet_Enmptypet");
                        break;
                    case 13:
                        dummy.SetHat("hat_cat_snow", colorid);
                        dummy.SetVisor("visor_Blush", colorid);
                        dummy.SetSkin("skin_ChefBlue", colorid);
                        dummy.SetPet("pet_EnmptyPet");
                        break;
                    case 5:
                        dummy.SetHat("hat_NewYear2024", colorid);
                        dummy.SetVisor("visor_hattyHattington", colorid);
                        dummy.SetSkin("skin_D2Osiris", colorid);
                        dummy.SetPet("pet_EmptyPet");
                        break;
                    case 14:
                        dummy.SetHat("hat_paws_panda", colorid);
                        dummy.SetVisor("visor_bsb2_noteSad", colorid);
                        dummy.SetSkin("skin_Capt", colorid);
                        dummy.SetPet("pet_EmptyPet");
                        break;
                    default:
                        dummy.SetHat("", colorid);
                        dummy.SetVisor("", colorid);
                        dummy.SetSkin("", colorid);
                        dummy.SetPet("");
                        break;
                }
                AmongUsClient.Instance.Spawn(dummy);
                spawned++;
                playerinfo.RpcSetTasks(Array.Empty<byte>());
            }
            return spawned;
        }

        static byte NextFreeId()
        {
            for (byte i = 0; i < 15; i++)
            {
                if (PlayerControl.AllPlayerControls.ToArray().Any(p => p != null && p.PlayerId == i)) continue;
                if (GameData.Instance.AllPlayers.ToArray().Any(p => p != null && p.PlayerId == i)) continue;
                return i;
            }
            return byte.MaxValue;
        }

        /// <summary>ダミーを count 体にそろえる(多ければ消す、少なければ足す)</summary>
        public static void SetCount(int count)
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsLocalGame || !GameStates.IsLobby) return;
            count = Math.Clamp(count, 0, 14);
            var dummies = PlayerCatch.AllPlayerControls.Where(pc => pc.isDummy).OrderByDescending(pc => pc.PlayerId).ToArray();
            for (var i = 0; i < dummies.Length - count; i++) Remove(dummies[i]);
            if (dummies.Length < count) Spawn(count);
        }
        static void Remove(PlayerControl dummy)
        {
            try
            {
                var data = dummy.Data;
                AmongUsClient.Instance.Despawn(dummy);
                if (data != null) AmongUsClient.Instance.Despawn(data);
            }
            catch (Exception ex) { Logger.Error(ex.ToString(), "DummySpawner"); }
        }

        // ---------- ローカルの部屋の「ダミーの数」 ----------
        static TextMeshPro minus, plus;
        /// <summary>部屋番号の文字の左右に「－」「＋」を作る(GameStartManager.Start の後に呼ぶ)</summary>
        public static void SetupLobbyUI(GameStartManager gsm)
        {
            minus = plus = null;
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsLocalGame) return;
            var code = gsm.GameRoomNameCode;
            minus = MakeButton(code, "－", -1.35f, () => Change(-1));
            plus = MakeButton(code, "＋", 1.35f, () => Change(+1));
            //部屋ができたら、保存してある数だけダミーを入れる
            _ = new LateTask(() => SetCount(Main.LocalDummyCount.Value), 1f, "LocalDummyAuto", true);
        }
        static TextMeshPro MakeButton(TextMeshPro code, string text, float x, Action onClick)
        {
            var b = Object.Instantiate(code, code.transform);
            //部屋番号の文字の子(HideNameなど)まで複製されるので消す
            for (var i = b.transform.childCount - 1; i >= 0; i--) Object.Destroy(b.transform.GetChild(i).gameObject);
            b.name = "LocalDummy" + (x < 0 ? "Minus" : "Plus");
            b.text = text;
            b.transform.localPosition = new Vector3(x, 0f, -1f);
            b.transform.localScale = Vector3.one;
            b.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 0.5f);
            var button = b.gameObject.AddComponent<PassiveButton>();
            button.OnClick = new();
            button.OnMouseOut = new();
            button.OnMouseOver = new();
            button.OnClick.AddListener(onClick);
            return b;
        }
        static void Change(int diff)
        {
            var count = Math.Clamp(Main.LocalDummyCount.Value + diff, 0, 14);
            Main.LocalDummyCount.Value = count;
            SetCount(count);
        }
        /// <summary>GameStartManager.Update のあとに呼ぶ。部屋番号の代わりにダミーの数を出す</summary>
        public static void UpdateLobbyUI(GameStartManager gsm)
        {
            if (minus == null || !GameStates.IsLocalGame) return;
            gsm.GameRoomNameCode.text = $"ダミー {Main.LocalDummyCount.Value}";
        }

        /// <summary>ホストのコマンド「/dummy [体数] [norole]」</summary>
        public static void Command(string[] args)
        {
            var host = PlayerControl.LocalPlayer;
            if (!GameStates.IsLocalGame || !GameStates.IsLobby)
            {
                Utils.SendMessage("ダミーはローカルゲームのロビーでだけ入れられます", host.PlayerId);
                return;
            }
            var count = 14;
            if (args.Length > 1 && int.TryParse(args[1], out var n)) count = Math.Clamp(n, 0, 14);
            AssignRole = !args.Any(a => a == "norole");
            Main.LocalDummyCount.Value = count;//部屋番号の位置の数もそろえる
            SetCount(count);
            Utils.SendMessage($"ダミーを{count}体にしました\nダミーの役職：{(AssignRole ? "付ける" : "付けない（クルーメイト）")}", host.PlayerId);
        }
    }
}

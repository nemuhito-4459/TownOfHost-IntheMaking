using System;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using static TownOfHost.Translator;

namespace TownOfHost
{
    /// <summary>
    /// ローカルでのテスト用のダミー(クローン)<br/>
    /// ・ロビーで「/dummy」… 全部で15人になるまでダミーを入れる(「/dummy 5」なら5体まで)<br/>
    /// ・ダミーにも役職が付く(「/dummy 14 norole」なら付けない)<br/>
    /// ・ローカルゲームのホストだけ。デバッグ版でなくても使える
    /// </summary>
    public static class DummySpawner
    {
        /// <summary>/dummy で入れたダミーに役職を付けるか</summary>
        public static bool AssignRole;

        /// <summary>ダミーを count 体になるまで出す(最大14体 = 全部で15人)。出した数を返す</summary>
        public static int Spawn(int count)
        {
            var spawned = 0;
            byte id = 0;
            foreach (var p in PlayerControl.AllPlayerControls)
                id++;
            for (var i = PlayerCatch.AllPlayerControls.Where(pc => pc.isDummy).Count();
            i < count; i++)
            {
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
                id++;
                AmongUsClient.Instance.Spawn(dummy);
                spawned++;
                playerinfo.RpcSetTasks(Array.Empty<byte>());
            }
            return spawned;
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
            var added = Spawn(count);
            Utils.SendMessage($"ダミーを{added}体入れました（全部で{PlayerCatch.AllPlayerControls.Count()}人）\nダミーの役職：{(AssignRole ? "付ける" : "付けない（クルーメイト）")}", host.PlayerId);
        }
    }
}

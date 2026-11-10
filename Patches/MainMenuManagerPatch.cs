using System;
using System.Text.RegularExpressions;

using TMPro;
using HarmonyLib;
using UnityEngine;
using AmongUs.Data;
using Assets.InnerNet;

using TownOfHost.Templates;
using Object = UnityEngine.Object;
using TownOfHost.Modules;
using System.Linq;

namespace TownOfHost
{
    [HarmonyPatch(typeof(MainMenuManager))]
    public class MainMenuManagerPatch
    {
        private static SimpleButton discordButton;
        private static SimpleButton StatisticsButton;
        private static GameObject Statistics_ScrollStuff;
        private static SimpleButton gitHubButton;
        private static SimpleButton TwitterXButton;
        private static SimpleButton TOHkBOTButton;
        private static SimpleButton RoleInfoButton;
        //private static SimpleButton VersionChangeButton;
        public static TextMeshPro Statistics_TMP;
        public static GameObject VersionMenu;

        [HarmonyPatch(nameof(MainMenuManager.Start)), HarmonyPostfix, HarmonyPriority(Priority.Normal)]
        public static void StartPostfix(MainMenuManager __instance)
        {
            SimpleButton.SetBase(__instance.quitButton);
            if (SimpleButton.IsNullOrDestroyed(RoleInfoButton))
            {
                RoleInfoButton = CreateButton(
                    "RoleInfoButton",
                    new(2.4f, 1, 1f),
                    new Color32(51, 156, 126, byte.MaxValue),
                    new Color32(103, 224, 190, byte.MaxValue),
                    () =>
                    {
                        RoleInfoShower.CreateMenu(__instance);
                    },
                    "Role/Achivement"
                );
            }
            //Discordボタンを生成
            if (SimpleButton.IsNullOrDestroyed(discordButton))
            {
                discordButton = CreateButton(
                    "DiscordButton",
                    new(-2.5f, -1f, 1f),
                    new(88, 101, 242, byte.MaxValue),
                    new(148, 161, byte.MaxValue, byte.MaxValue),
                    () => Application.OpenURL(Main.DiscordInviteUrl),
                    "Discord",
                    isActive: Main.ShowDiscordButton);
            }

            // GitHubボタンを生成
            if (SimpleButton.IsNullOrDestroyed(gitHubButton))
            {
                gitHubButton = CreateButton(
                    "GitHubButton",
                    new(-0.8f, -1f, 1f),//-1f
                    new(153, 153, 153, byte.MaxValue),
                    new(209, 209, 209, byte.MaxValue),
                    () => Application.OpenURL("https://github.com/KYMario/TownOfHost-K"),
                    "GitHub");
            }

            // TwitterXボタンを生成
            if (SimpleButton.IsNullOrDestroyed(TwitterXButton))
            {
                TwitterXButton = CreateButton(
                    "TwitterXButton",
                    new(0.9f, -1f, 1f),
                    new(0, 202, 255, byte.MaxValue),
                    new(60, 255, 255, byte.MaxValue),
                    () => Application.OpenURL("https://twitter.com/Tohkserver_k"),
                    "Twitter(X)");
            }
            // TOHkBOTボタンを生成
            if (SimpleButton.IsNullOrDestroyed(TOHkBOTButton))
            {
                TOHkBOTButton = CreateButton(
                    "TOHkBOTButton",
                    new(2.6f, -1f, 1f),
                    new(0, 201, 87, byte.MaxValue),
                    new(60, 201, 87, byte.MaxValue),
                    () => Application.OpenURL("https://discord.com/api/oauth2/authorize?client_id=1198276538563567716&permissions=8&scope=bot"),
                    "TOHkBOT");
            }
            if (SimpleButton.IsNullOrDestroyed(StatisticsButton))
            {
                StatisticsButton = CreateButton(
                    "StatisticsButton",
                    new Vector3(0, -2.6963f, -5f),
                    new(255, 242, 104, byte.MaxValue),
                    new(255, 248, 173, byte.MaxValue),
                    () =>
                    {
                        CredentialsPatch.TohkLogo.gameObject.SetActive(false);
                        __instance.screenTint.enabled = true;
                        Statistics_TMP.gameObject.SetActive(true);
                        Statistics_TMP.text = $"<size=60%>{SaveStatistics.ShowText()}";
                        Statistics_ScrollStuff.gameObject.SetActive(true);
                        var St_Scroller = Statistics_ScrollStuff.transform.GetChild(0);
                        Statistics_TMP.transform.parent = St_Scroller.GetChild(3).transform;
                        St_Scroller.GetChild(1).gameObject.SetActive(false);
                        St_Scroller.GetChild(2).gameObject.SetActive(false);
                        St_Scroller.localPosition = new(3.18f, -5.45f, 0.1473f);
                        St_Scroller.GetChild(0).localPosition = new(2.1f, 2.6f, 2f);
                        St_Scroller.GetChild(0).localScale = new Vector3(0.7f, 1, 0);
                        St_Scroller.GetChild(3).SetLocalY(0);
                        var ages = Statistics_TMP.text.Split("\n").Count();
                        St_Scroller.GetComponentInParent<Scroller>().ContentYBounds.max = ages > 16 ? (ages - 16) * 0.25f : 0;
                    },
                    Translator.GetString("Statistics")
                    );
            }

            if (Statistics_ScrollStuff == null || Statistics_ScrollStuff.gameObject == null)
            {
                var sc = GameObject.Find("StoreMenu/Background/Scroll Stuff");
                Statistics_ScrollStuff = Object.Instantiate(sc, __instance.transform);
                Statistics_ScrollStuff.gameObject.name = "stscroll";
                var Scroller = Statistics_ScrollStuff.transform.GetChild(0);
                Scroller.GetChild(3).DestroyChildren();//inner全削除
                Statistics_ScrollStuff.gameObject.SetActive(false);
            }

            CreateStreameMenu.CreateMenu(__instance);

            // フリープレイの無効化
            var howToPlayButton = __instance.howToPlayButton;
            var freeplayButton = howToPlayButton.transform.parent.Find("FreePlayButton");
#if RELEASE
            if (freeplayButton != null)
            {
                var textm = freeplayButton.transform.FindChild("Text_TMP").GetComponent<TextMeshPro>();
                textm.DestroyTranslator();
                textm.text = Translator.GetString("EditCSp");

                freeplayButton.GetComponent<PassiveButton>().OnClick.AddListener((Action)(() => CustomSpawnEditor.ActiveEditMode = true));
            }
            // フリープレイが消えるのでHowToPlayをセンタリング | 消えないのでしません☆
            //howToPlayButton.transform.SetLocalX(0);
#endif
#if DEBUG
            var csbutton = GameObject.Instantiate(freeplayButton, freeplayButton.parent);
            var textm = csbutton.transform.FindChild("Text_TMP").GetComponent<TextMeshPro>();
            textm.DestroyTranslator();
            textm.text = Translator.GetString("EditCSp");

            csbutton.transform.localPosition = new Vector3(2.8704f, -1.9916f);
            csbutton.transform.localScale = new Vector3(0.6f, 0.6f);
            var pb = csbutton.GetComponent<PassiveButton>();
            pb.inactiveSprites.GetComponent<SpriteRenderer>().color = new(88, 101, 242, byte.MaxValue);
            pb.activeSprites.GetComponent<SpriteRenderer>().color = new(148, 161, byte.MaxValue, byte.MaxValue);
            pb.OnClick.AddListener((Action)(() => CustomSpawnEditor.ActiveEditMode = true));
            freeplayButton.GetComponent<PassiveButton>().OnClick.AddListener((Action)(() => CustomSpawnEditor.ActiveEditMode = false));//ボタンを生成
#endif
        }

        /// <summary>TOHロゴの子としてボタンを生成</summary>
        /// <param name="name">オブジェクト名</param>
        /// <param name="normalColor">普段のボタンの色</param>
        /// <param name="hoverColor">マウスが乗っているときのボタンの色</param>
        /// <param name="action">押したときに発火するアクション</param>
        /// <param name="label">ボタンのテキスト</param>
        /// <param name="scale">ボタンのサイズ 変更しないなら不要</param>
        public static SimpleButton CreateButton(
            string name,
            Vector3 localPosition,
            Color32 normalColor,
            Color32 hoverColor,
            Action action,
            string label,
            Vector2? scale = null,
            bool isActive = true,
            Transform transform = null,
            string toolTip = null)
        {
            var button = new SimpleButton(transform == null ? CredentialsPatch.TohkLogo.transform : transform, name, localPosition, normalColor, hoverColor, action, label, isActive);
            if (scale.HasValue)
            {
                button.Scale = scale.Value;
            }
            if (toolTip != null)
            {
                button.Button.OnMouseOver.AddListener((Action)(() =>
                {
                    var pos = ToolTip.GetMoucePos(new(-1.8f, -0.2f, -30)); //ejectボタンに隠れないように
                    ToolTip.Show(button.Button, Translator.GetString(toolTip), pos);
                }));
                button.Button.OnMouseOut.AddListener((Action)ToolTip.Hide);
            }
            return button;
        }

        [HarmonyPatch(nameof(MainMenuManager.ClickBackOnline))]
        [HarmonyPostfix]
        public static void ClickBackOnline(MainMenuManager __instance)
        {
            __instance.ResetScreen();
        }
        // プレイメニュー，アカウントメニュー，クレジット画面が開かれたらロゴとボタンを消す
        [HarmonyPatch(nameof(MainMenuManager.OpenGameModeMenu))]
        [HarmonyPatch(nameof(MainMenuManager.OpenAccountMenu))]
        [HarmonyPatch(nameof(MainMenuManager.OpenCredits))]
        [HarmonyPatch(nameof(MainMenuManager.OpenOnlineMenu))]
        [HarmonyPatch(nameof(MainMenuManager.GoBackCreateGame))]
        [HarmonyPatch(nameof(MainMenuManager.OpenEnterCodeMenu))]
        [HarmonyPatch(nameof(MainMenuManager.OpenRedeemCodeMenu))]
        [HarmonyPostfix]
        public static void OpenMenuPostfix(MainMenuManager __instance)
        {
            CreateStreameMenu.CloseMenu();
            var Findbuttongo = GameObject.Find("MainMenuManager/MainUI/AspectScaler/RightPanel/MaskedBlackScreen/OnlineButtons/AspectSize/Scaler/Find Game Button");

            var codebuttongo = GameObject.Find("MainMenuManager/MainUI/AspectScaler/RightPanel/MaskedBlackScreen/OnlineButtons/AspectSize/Scaler/Enter Code Button");
            var Codebutton = codebuttongo.transform.GetComponent<PassiveButton>();

            var createbuttongameobject = GameObject.Find("MainMenuManager/MainUI/AspectScaler/RightPanel/MaskedBlackScreen/OnlineButtons/AspectSize/Scaler/Create Lobby Button");
            var createbutton = createbuttongameobject.transform.GetComponent<PassiveButton>();

            if (Findbuttongo)
            {
                // var disable = version == null || version.DisableMM || (allVersion != null && allVersion.DisableMM);
                //Findbuttongo.SetActive(!disable);
                Findbuttongo.SetActive(false);
            }
            if (Main.IsAndroid())
            {
                //    createbutton.GetComponent<BoxCollider2D>().offset = new(100f, 100);
            }

            if (CredentialsPatch.TohkLogo != null)
            {
                CredentialsPatch.TohkLogo.gameObject.SetActive(false);
            }
            if (VersionMenu != null)
                VersionMenu.SetActive(false);
            if (Statistics_TMP?.gameObject != null)
                Statistics_TMP?.gameObject.SetActive(false);
            if (Statistics_ScrollStuff?.gameObject != null)
                Statistics_ScrollStuff?.gameObject.SetActive(false);

            {
                var warning = GameObject.Find("MainMenuManager/MainUI/AspectScaler/RightPanel/MaskedBlackScreen/OnlineButtons/AspectSize/CrossplayWarning");
                warning.SetActive(true);
                var TMPobjct = GameObject.Find("MainMenuManager/MainUI/AspectScaler/RightPanel/MaskedBlackScreen/OnlineButtons/AspectSize/CrossplayWarning/CrossPlayText/Text_TMP");

                var TMP = TMPobjct.transform.GetComponent<TextMeshPro>();
                var text = Main.IsAndroid() ? Translator.GetString("CantAndroidCreateGame") : "";
                TMP.SetText(text);
                _ = new LateTask(() => TMP.SetText(text), 0.05f, "Set", true);
                if (text == "") warning.SetActive(false);
            }
            OptionsMenuBehaviourStartPatch.Instance = null;
        }
        [HarmonyPatch(nameof(MainMenuManager.ResetScreen)), HarmonyPostfix]
        public static void ResetScreenPostfix(MainMenuManager __instance)
        {
            if (CredentialsPatch.TohkLogo != null)
            {
                CredentialsPatch.TohkLogo?.gameObject?.SetActive(true);
            }
            if (VersionMenu != null)
                VersionMenu.SetActive(false);
            if (Statistics_TMP != null)
                Statistics_TMP?.gameObject.SetActive(false);
            if (Statistics_ScrollStuff != null)
                Statistics_ScrollStuff?.gameObject.SetActive(false);
            CreateStreameMenu.CloseMenu();
            _ = new LateTask(() =>
            {
                if (__instance?.ejectMenu?.ejectButton?.gameObject is not null)
                    __instance?.ejectMenu?.ejectButton?.gameObject?.SetActive(true);
            }, 0.5f, "ShowButton", true);
        }
        public static void DestroyButton()
        {
            VersionMenu = null;
        }
    }
    public class ModNews
    {
        public int Number;
        public int BeforeNumber;
        public string Title;
        public string SubTitle;
        public string ShortTitle;
        public string Text;
        public string Date;

        public Announcement ToAnnouncement()
        {
            var result = new Announcement
            {
                Number = Number,
                Title = Title,
                SubTitle = SubTitle,
                ShortTitle = ShortTitle,
                Text = Text,
                Language = (uint)DataManager.Settings.Language.CurrentLanguage,
                Date = Date,
                Id = "ModNews"
            };

            return result;
        }
    }
    public class JsonModNews
    {
        public JsonModNews(int Number, string Title, string SubTitle, string ShortTitle,
            string Text, string Date)
        {
            var news = new ModNews
            {
                Number = Number,
                Title = Title,
                SubTitle = SubTitle,
                ShortTitle = ShortTitle,
                Text = Text,
                Date = Date
            };
            ModNewsHistory.JsonAndAllModNews.Add(news);
        }
    }

    [HarmonyPatch(typeof(EjectMainMenu), nameof(EjectMainMenu.EjectCrewmate))]
    class EjectMainMenuEjectCrewmatePatch
    {
        public static int i = 0;
        public static bool Prefix(EjectMainMenu __instance)
        {
            //クールダウン処理を消したver  // pressStateがnullになっていてエラーが発生していたためエラーを発生させないためにっ
            PlayerParticle playerParticle = __instance.pool.Get<PlayerParticle>();
            PlayerMaterial.SetColors(IRandom.Instance.Next(0, 18), (Renderer)(object)playerParticle.myRend); // (0~17)
            __instance.PlacePlayer(playerParticle, initial: false);
            return false;
        }
        public static void Postfix(EjectMainMenu __instance)
        {
            try
            {
                i++;
                __instance.pressState?.SetActive(false);
                __instance.ejectButton?.SetActive(true);
                __instance.onCooldown = false;
                if (10 < i && i < 60)
                {
                    __instance.EjectCrewmate();
                }
                if (80 < i)
                {
                    i = 0;
                }

                if (IRandom.Instance.Next(3) is 1)
                {
                    __instance.EjectCrewmate();
                }
            }
            catch { }
        }
    }
    [HarmonyPatch(typeof(EjectMainMenu), nameof(EjectMainMenu.PlacePlayer))]
    class EjectMainMenuEjectPlacePlayerPatch
    {
        public static Shader Shader = null;
        public static void Postfix(EjectMainMenu __instance, PlayerParticle part)
        {
            var chance = IRandom.Instance.Next(3);
            var size = 30 + IRandom.Instance.Next(50);
            if (Shader is null)
            {
                Shader = part.myRend.material.shader;
            }
            part.myRend.material.shader = Shader;
            part.myRend.sharedMaterial.shader = Shader;
            Shader shader = Shader.Find("Sprites/Default");
            if (chance is 1)
            {
                var allrole = CustomRolesHelper.AllStandardRoles;
                var role = allrole[IRandom.Instance.Next(allrole.Count())];
                var sprite = UtilsSprite.LoadSprite($"TownOfHost.Resources.TOHK.Label.{role}.png", size);
                if (sprite is null) return;
                part.myRend.material.shader = shader;
                part.myRend.sharedMaterial.shader = shader;
                part.myRend.sprite = sprite;
            }
            if (chance is 2)
            {
                var allrole = CustomRolesHelper.AllRoles;
                var role = allrole[IRandom.Instance.Next(allrole.Count())];
                var sprite = UtilsSprite.LoadSprite($"TownOfHost.Resources.TOHK.Button.{role}_Ability.png", size);
                if (sprite is null)
                    sprite = UtilsSprite.LoadSprite($"TownOfHost.Resources.TOHK.Button.{role}_Kill.png", size);
                if (sprite is null)
                    sprite = UtilsSprite.LoadSprite($"TownOfHost.Resources.TOHK.Button.{role}_Vent.png", size);
                if (sprite is null) return;
                part.myRend.material.shader = shader;
                part.myRend.sharedMaterial.shader = shader;
                part.myRend.sprite = sprite;
            }
        }
    }
}
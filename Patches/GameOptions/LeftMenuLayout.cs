using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static TownOfHost.GameSettingMenuStartPatch;
using static TownOfHost.Translator;

namespace TownOfHost
{
    /// <summary>
    /// 設定画面のレイアウト変更<br/>
    /// ・左のメニューに「バニラ設定」「MODのタブ7つ」を2列で並べる(常に表示。ゲーム・プリセットは隠す)<br/>
    /// ・上のバー(人数・タイマー、有効な設定のみ表示、検索、プリセット名)を、元のタブの段へ移す(常に表示)<br/>
    /// 位置は今ある部品の実際の位置・大きさを基準に計算する。微調整は下の定数で行う。
    /// </summary>
    public static class LeftMenuLayout
    {
        //── 調整用の定数 ──
        /// <summary>左のボタンの大きさ(プリセットボタンの元は0.45)</summary>
        const float ButtonScale = 0.42f;
        /// <summary>ボタンの横の間隔(ボタンの幅に対する倍率)</summary>
        const float ColumnGap = 1.05f;
        /// <summary>ボタンの縦の間隔(ボタンの高さに対する倍率)</summary>
        const float RowGap = 1.3f;
        /// <summary>上のバーを移したあとの追加のずらし(ワールド座標)</summary>
        static readonly Vector3 BarExtraOffset = new(0f, 0.15f, 0f);

        /// <summary>
        /// 左に並べるMODのタブの順番(バニラ設定の次から、左→右・上→下)<br/>
        /// バニラ設定 / TOH-IMの設定、インポスター / マッドメイト、クルーメイト / アサシンとマーリン、ジャッカル / ニュートラル
        /// </summary>
        static readonly TabGroup[] LeftOrder =
        {
            TabGroup.MainSettings,
            TabGroup.ImpostorRoles, TabGroup.MadmateRoles,
            TabGroup.CrewmateRoles, TabGroup.Combinations,
            TabGroup.JackalRoles, TabGroup.NeutralRoles,
        };
        /// <summary>左に並べたMODのタブのボタン(LeftOrder順)</summary>
        public static List<PassiveButton> LeftTabButtons = new();
        static PassiveButton OfficialButton;
        /// <summary>有効な設定のみ表示ボタンの位置(Update.csで毎フレーム使う)</summary>
        public static Vector3? ActiveOnlyLocalPos;

        static readonly Dictionary<TabGroup, Color32> TabColors = new()
        {
            { TabGroup.MainSettings, ModColors.ModColor },
            { TabGroup.ImpostorRoles, ModColors.ImpostorRed },
            { TabGroup.MadmateRoles, ModColors.MadMateOrenge },
            { TabGroup.CrewmateRoles, new Color32(140, 255, 255, 255) },
            { TabGroup.NeutralRoles, ModColors.NeutralGray },
            { TabGroup.JackalRoles, ModColors.JackalColor },
            { TabGroup.Combinations, new Color32(255, 200, 0, 255) },
        };

        public static void Apply(GameSettingMenu menu, PassiveButton presetButton, PassiveButton gameSettingsButton)
        {
            try
            {
                LeftTabButtons = new();
                MoveTopBar();
                BuildLeftGrid(menu, presetButton, gameSettingsButton);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString(), "LeftMenuLayout");
            }
        }

        /// <summary>上のバーを、元のタブの段へまとめてずらす</summary>
        static void MoveTopBar()
        {
            if (tabButtons == null || tabButtons.Count == 0) return;

            //有効な設定のみ表示はUpdate.csで毎フレーム置き直しているので、同じ値にしてから測る
            if (activeonly)
            {
                activeonly.transform.localPosition = new Vector3(-2.05f, 3.3f);
                activeonly.transform.localScale = new Vector3(0.6f, 0.6f, 0f);
            }

            var bar = new List<Transform>();
            if (LobbyTimer) bar.Add(LobbyTimer);
            if (LobbyCount) bar.Add(LobbyCount);
            if (activeonly) bar.Add(activeonly.transform);
            if (search) bar.Add(search.transform);
            if (priset) bar.Add(priset.transform);
            if (bar.Count == 0) return;

            //タブの段の中心
            var first = tabButtons[0].transform.position;
            var last = tabButtons[tabButtons.Count - 1].transform.position;
            var tabCenter = (first + last) / 2f;
            //バーの中心
            var minX = bar.Min(t => t.position.x);
            var maxX = bar.Max(t => t.position.x);
            var minY = bar.Min(t => t.position.y);
            var maxY = bar.Max(t => t.position.y);
            var barCenter = new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, 0f);

            var delta = new Vector3(tabCenter.x - barCenter.x, tabCenter.y - barCenter.y, 0f) + BarExtraOffset;
            foreach (var t in bar) t.position += delta;
            if (activeonly) ActiveOnlyLocalPos = activeonly.transform.localPosition;

            //元のタブは隠す(押したときの処理は左のボタンから呼ぶ)
            for (var i = 0; i < tabButtons.Count; i++) tabButtons[i]?.gameObject.SetActive(false);
        }

        /// <summary>左のメニューにボタンを2列で並べる</summary>
        static void BuildLeftGrid(GameSettingMenu menu, PassiveButton presetButton, PassiveButton gameSettingsButton)
        {
            if (presetButton == null) return;

            presetButton.transform.localScale = new Vector3(ButtonScale, ButtonScale, 1f);
            var origin = presetButton.transform.position;
            var size = presetButton.inactiveSprites.GetComponent<SpriteRenderer>().bounds.size;
            var dx = size.x * ColumnGap;
            var dy = size.y * RowGap;

            //公式のゲーム設定(大きいボタンは隠して、プリセットと同じ形のボタンを作る)
            OfficialButton = UnityEngine.Object.Instantiate(presetButton, presetButton.transform.parent);
            OfficialButton.name = "OfficialGameSettings";
            SetLabel(OfficialButton, GetString("OfficialGameSettings"), null);
            OfficialButton.OnClick = new();
            OfficialButton.OnClick.AddListener((Action)(() =>
            {
                menu.ChangeTab(1, false);
                SelectLeft(OfficialButton);
            }));
            gameSettingsButton?.gameObject.SetActive(false);
            //「TOWNOFHOST-INTHEMAKING」ボタンも隠す(MODのタブのボタンで代わりに開く)
            if (ModSettingsButton) ModSettingsButton.transform.localPosition = new Vector3(1000f, 1000f, 0f);

            //ゲーム・プリセットはリストラ(位置と大きさの基準にだけ使う)
            var cells = new List<PassiveButton> { OfficialButton };

            foreach (var tab in LeftOrder)
            {
                var index = (int)tab;
                var button = UnityEngine.Object.Instantiate(presetButton, presetButton.transform.parent);
                button.name = $"LeftTab_{tab}";
                var label = tab switch
                {
                    TabGroup.MainSettings => GetString("LeftTab.MainSettings"),
                    TabGroup.Combinations => GetString("LeftTab.Combinations"),
                    _ => GetString("TabGroup." + tab)
                };
                SetLabel(button, label, TabColors.TryGetValue(tab, out var c) ? (Color32?)c : null);
                button.OnClick = new();
                button.OnClick.AddListener((Action)(() =>
                {
                    ShowModSetting = false;
                    menu.ChangeTab(3, false);
                    SelectLeft(button);
                    _ = new LateTask(() =>
                    {
                        if (!(ModSettingsTab?.gameObject?.active ?? false)) return;
                        if (tabButtons != null && index < tabButtons.Count) tabButtons[index]?.OnClick?.Invoke();
                    }, 0.05f, "LeftMenuTab", true);
                }));
                LeftTabButtons.Add(button);
                cells.Add(button);
            }

            //バニラ設定を左上(元のプリセットの位置)にして、左→右、上→下の順に並べる
            for (var i = 0; i < cells.Count; i++)
            {
                var col = i % 2;
                var row = i / 2;
                var pos = origin + new Vector3(col * dx, -row * dy, 0f);
                cells[i].transform.position = new Vector3(pos.x, pos.y, cells[i].transform.position.z);
                cells[i].transform.localScale = new Vector3(ButtonScale, ButtonScale, 1f);
                cells[i].gameObject.SetActive(true);
            }

            presetButton.gameObject.SetActive(false);
        }

        static void SetLabel(PassiveButton button, string text, Color32? color)
        {
            if (button.buttonText != null)
            {
                button.buttonText.DestroyTranslator();
                button.buttonText.enableAutoSizing = true;
                button.buttonText.fontSizeMin = 0.5f;
                button.buttonText.fontSizeMax = button.buttonText.fontSize;
                button.buttonText.text = text;
            }
            if (color is Color32 col)
            {
                button.inactiveSprites.GetComponent<SpriteRenderer>().color = col;
                button.activeSprites.GetComponent<SpriteRenderer>().color = col;
                button.selectedSprites.GetComponent<SpriteRenderer>().color = ((Color)col).ShadeColor(-0.3f);
            }
        }

        /// <summary>上のバー(人数・タイマー、有効な設定のみ表示、検索、プリセット名)の表示を切り替える</summary>
        public static void SetBarVisible(bool active)
        {
            if (LobbyTimer) LobbyTimer.gameObject.SetActive(active);
            if (LobbyCount) LobbyCount.gameObject.SetActive(active);
            if (search) search.gameObject.SetActive(active);
            if (priset) priset.gameObject.SetActive(active);
            if (searchtext) searchtext.gameObject.SetActive(active);
            if (prisettext) prisettext.gameObject.SetActive(active);
            if (activeonly) activeonly.gameObject.SetActive(active);
        }

        /// <summary>押したボタンだけを選択中の見た目にする</summary>
        static void SelectLeft(PassiveButton selected)
        {
            foreach (var b in LeftTabButtons) b?.SelectButton(b == selected);
            OfficialButton?.SelectButton(OfficialButton == selected);
        }
    }
}

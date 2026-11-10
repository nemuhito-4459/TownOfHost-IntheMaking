using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;

namespace TownOfHost
{
    /// <summary>
    /// バニラの設定のうち、陣営ごとの設定を各陣営タブの共通設定に持ってきたもの。<br/>
    /// 値はバニラの設定(Main.NormalOptions)へ書き込んで使う。バニラの設定画面からは隠す(<see cref="HideMovedVanillaOptionsPatch"/>)。
    /// </summary>
    public static class FactionCommonOptions
    {
        //インポスター
        public static OptionItem KillCooldown;
        public static OptionItem KillDistance;
        public static OptionItem ImpostorVision;
        public static OptionItem NumImpostors;
        //クルーメイト
        public static OptionItem CrewmateVision;
        public static OptionItem NumCommonTasks;
        public static OptionItem NumShortTasks;
        public static OptionItem NumLongTasks;

        const int IdStart = 106000;

        public static void SetupImpostorOptions()
        {
            var tab = TabGroup.ImpostorRoles;
            KillCooldown = FloatOptionItem.Create(IdStart, "ImpostorCommonKillCooldown", new(0.5f, 180f, 0.5f), 30f, tab, false)
                .SetValueFormat(OptionFormat.Seconds)
                .SetHeader(true)
                .SetColor(Palette.ImpostorRed)
                .RegisterUpdateValueEvent((_, _) => ApplyToVanilla());
            KillDistance = StringOptionItem.Create(IdStart + 1, "ImpostorCommonKillDistance", EnumHelper.GetAllNames<OverrideKilldistance.KillDistance>(), 0, tab, false)
                .SetColor(Palette.ImpostorRed)
                .RegisterUpdateValueEvent((_, _) => ApplyToVanilla());
            ImpostorVision = FloatOptionItem.Create(IdStart + 2, "ImpostorCommonVision", new(0.25f, 5f, 0.25f), 1.25f, tab, false)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(Palette.ImpostorRed)
                .RegisterUpdateValueEvent((_, _) => ApplyToVanilla());
            NumImpostors = IntegerOptionItem.Create(IdStart + 3, "ImpostorCommonNum", new(1, 15, 1), 2, tab, false)
                .SetValueFormat(OptionFormat.Players)
                .SetColor(Palette.ImpostorRed)
                .RegisterUpdateValueEvent((_, _) => ApplyToVanilla());
        }

        public static void SetupCrewmateOptions()
        {
            var tab = TabGroup.CrewmateRoles;
            var crew = new Color32(140, 255, 255, byte.MaxValue);
            CrewmateVision = FloatOptionItem.Create(IdStart + 10, "CrewmateCommonVision", new(0.25f, 5f, 0.25f), 0.5f, tab, false)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetHeader(true)
                .SetColor(crew)
                .RegisterUpdateValueEvent((_, _) => ApplyToVanilla());
            //範囲・初期値は VanillaOptionHolder(MOD側で広げてある)に合わせる
            NumCommonTasks = IntegerOptionItem.Create(IdStart + 11, "CrewmateCommonNumCommonTasks", new(0, 99, 1), 2, tab, false)
                .SetValueFormat(OptionFormat.Pieces)
                .SetColor(crew)
                .RegisterUpdateValueEvent((_, _) => ApplyToVanilla());
            NumShortTasks = IntegerOptionItem.Create(IdStart + 12, "CrewmateCommonNumShortTasks", new(0, 99, 1), 4, tab, false)
                .SetValueFormat(OptionFormat.Pieces)
                .SetColor(crew)
                .RegisterUpdateValueEvent((_, _) => ApplyToVanilla());
            NumLongTasks = IntegerOptionItem.Create(IdStart + 13, "CrewmateCommonNumLongTasks", new(0, 99, 1), 2, tab, false)
                .SetValueFormat(OptionFormat.Pieces)
                .SetColor(crew)
                .RegisterUpdateValueEvent((_, _) => ApplyToVanilla());
        }

        /// <summary>陣営タブの値をバニラの設定へ書き込む(設定変更時と試合開始時に呼ぶ)</summary>
        public static void ApplyToVanilla()
        {
            if (Main.NormalOptions == null || KillCooldown == null || CrewmateVision == null) return;
            var opt = Main.NormalOptions.Cast<IGameOptions>();
            opt.SetFloat(FloatOptionNames.KillCooldown, KillCooldown.GetFloat());
            opt.SetInt(Int32OptionNames.KillDistance, KillDistance.GetValue());
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, ImpostorVision.GetFloat());
            opt.SetInt(Int32OptionNames.NumImpostors, NumImpostors.GetInt());
            opt.SetFloat(FloatOptionNames.CrewLightMod, CrewmateVision.GetFloat());
            opt.SetInt(Int32OptionNames.NumCommonTasks, NumCommonTasks.GetInt());
            opt.SetInt(Int32OptionNames.NumShortTasks, NumShortTasks.GetInt());
            opt.SetInt(Int32OptionNames.NumLongTasks, NumLongTasks.GetInt());
            Options.DefaultKillCooldown = KillCooldown.GetFloat();
        }
    }

    /// <summary>
    /// 陣営タブへ移した設定を、バニラの設定画面から隠して、下の項目を詰める
    /// </summary>
    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.CreateSettings))]
    public static class HideMovedVanillaOptionsPatch
    {
        static readonly HashSet<Int32OptionNames> IntNames = new()
        {
            Int32OptionNames.NumImpostors, Int32OptionNames.KillDistance,
            Int32OptionNames.NumCommonTasks, Int32OptionNames.NumShortTasks, Int32OptionNames.NumLongTasks,
        };
        static readonly HashSet<FloatOptionNames> FloatNames = new()
        {
            FloatOptionNames.KillCooldown, FloatOptionNames.ImpostorLightMod, FloatOptionNames.CrewLightMod,
        };
        const float ItemHeight = 0.45f;

        public static void Postfix(GameOptionsMenu __instance)
        {
            try
            {
                if (__instance?.transform?.name != "GAME SETTINGS TAB") return;

                List<float> hiddenY = new();
                for (var i = 0; i < __instance.Children.Count; i++)
                {
                    var option = __instance.Children[i];
                    if (option == null) continue;
                    var hide = false;
                    var number = option.TryCast<NumberOption>();
                    if (number != null) hide = FloatNames.Contains(number.floatOptionName) || IntNames.Contains(number.intOptionName);
                    var str = option.TryCast<StringOption>();
                    if (str != null) hide = IntNames.Contains(str.stringOptionName);
                    if (!hide) continue;

                    hiddenY.Add(option.transform.localPosition.y);
                    option.gameObject.SetActive(false);
                }
                if (hiddenY.Count == 0) return;

                //隠した項目より下にあるものを、その数だけ上へ詰める
                var container = __instance.settingsContainer;
                for (var i = 0; i < container.childCount; i++)
                {
                    var child = container.GetChild(i);
                    if (!child.gameObject.activeSelf) continue;
                    var y = child.localPosition.y;
                    var above = hiddenY.Count(h => h > y + 0.01f);
                    if (above > 0) child.localPosition += new Vector3(0f, ItemHeight * above, 0f);
                }
                var scroller = __instance.GetComponentInParent<Scroller>();
                if (scroller != null) scroller.ContentYBounds.max -= ItemHeight * hiddenY.Count;
            }
            catch (System.Exception ex)
            {
                Logger.Error(ex.ToString(), "HideMovedVanillaOptions");
            }
        }
    }
}

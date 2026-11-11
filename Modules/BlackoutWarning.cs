using HarmonyLib;
using TMPro;
using UnityEngine;

using TownOfHost.Attributes;

namespace TownOfHost.Modules
{
    /// <summary>
    /// 暗転(MOD未導入の人の画面が真っ暗のまま進まない)が起きたかもしれないときに、
    /// ホストの画面の上側に警告を出す
    /// </summary>
    [HarmonyPatch]
    public static class BlackoutWarning
    {
        const float ShowTime = 20f;
        static TextMeshPro text;
        static float timer;
        static string reason = "";

        /// <summary>警告を出す(ホストだけ)</summary>
        public static void Show(string why)
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
            Logger.Warn($"暗転の可能性: {why}", "BlackoutWarning");
            //同じ間に何個も来たら並べる
            reason = timer > 0 && reason != "" && !reason.Contains(why) ? $"{reason}\n{why}" : why;
            timer = ShowTime;
        }

        [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update)), HarmonyPostfix]
        public static void HudUpdatePostfix(HudManager __instance)
        {
            if (timer <= 0 || !GameStates.InGame)
            {
                timer = 0;
                if (text != null) text.gameObject.SetActive(false);
                return;
            }
            timer -= Time.deltaTime;
            if (text == null)
            {
                text = Object.Instantiate(__instance.TaskPanel.taskText, __instance.transform);
                text.name = "BlackoutWarning";
                text.alignment = TextAlignmentOptions.Top;
                text.overflowMode = TextOverflowModes.Overflow;
                text.enableWordWrapping = false;
                text.fontSizeMin = text.fontSizeMax = text.fontSize = 2.2f;
            }
            text.gameObject.SetActive(true);
            text.transform.localPosition = new Vector3(0f, 2.6f, -200f);
            text.text = $"<#ff4040><b>⚠ 暗転の可能性</b></color>\n<size=80%>{reason}</size>\n<size=60%><#ffffffaa>(あと{Mathf.CeilToInt(timer)}秒で消えます)</color></size>";
        }

        [GameModuleInitializer]
        public static void Init()
        {
            timer = 0;
            reason = "";
        }
    }
}

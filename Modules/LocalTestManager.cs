using System;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

using TownOfHost.Roles.Core;

namespace TownOfHost
{
    /// <summary>
    /// ローカルでのテストプレイ用(ローカルの部屋のホストだけ)<br/>
    /// ・全員の役職が見える<br/>
    /// ・左下の「乗り移り」ボタンで、選んだプレイヤー(ダミー)に乗り移って、そのプレイヤーとして動ける<br/>
    ///   (元の自分を選ぶと戻る。試合が終わると自動で元に戻る)
    /// </summary>
    [HarmonyPatch]
    public static class LocalTestManager
    {
        /// <summary>ローカルの部屋のホストか</summary>
        public static bool IsLocalHost => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost && GameStates.IsLocalGame;
        /// <summary>全員の役職が見えるか(乗り移っている間も見える)</summary>
        public static bool SeeAllRoles => IsLocalHost;

        /// <summary>ホスト本人の体(乗り移っていないときは null)</summary>
        static PlayerControl hostBody;
        /// <summary>乗り移っている間の、元のホストの体か</summary>
        public static bool IsHostBodyWhilePossessed(PlayerControl pc) => pc != null && Possessed != null && hostBody != null && pc == hostBody;
        /// <summary>今乗り移っているプレイヤー(乗り移っていないときは null)</summary>
        public static PlayerControl Possessed { get; private set; }
        static bool picking;
        static ActionButton button;

        // ------------------ 乗り移り ------------------
        public static void Possess(PlayerControl target)
        {
            if (!IsLocalHost || target == null) return;
            var current = PlayerControl.LocalPlayer;
            if (current == null || target == current) return;
            hostBody ??= current;
            UndoRevive();
            try
            {
                //今の体は止める(ダミーなら元のダミーに戻す)
                current.MyPhysics.SetNormalizedVelocity(Vector2.zero);
                current.moveable = false;
                if (current != hostBody) SetDummy(current, true);

                //次の体を動かせるようにする
                if (target != hostBody) SetDummy(target, false);
                target.moveable = true;

                //視界(ライト)を移す
                var light = current.lightSource;
                if (light != null)
                {
                    var pos = light.transform.localPosition;
                    light.transform.SetParent(target.transform, false);
                    light.transform.localPosition = pos;
                    current.lightSource = target.lightSource;
                    target.lightSource = light;
                }

                PlayerControl.LocalPlayer = target;
                HudManager.Instance.PlayerCam.SetTarget(target);
                HudManager.Instance.PlayerCam.SnapToTarget();

                Possessed = target == hostBody ? null : target;
                if (Possessed == null) hostBody = null;

                //乗り移った先の役職のボタンなどを出す(自分の画面だけ)
                RoleManager.Instance.SetRole(target, GetRoleType(target));
                ResetHud(target);
                //会議中に勝者予想ができる人へ乗り移ったら投票できるようにする
                ReviveForPrediction(target);
                Logger.Info($"{target.Data.GetLogPlayerName()} に乗り移り", "LocalTest");
                UtilsNotifyRoles.NotifyRoles(true, true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString(), "LocalTest");
            }
        }
        /// <summary>
        /// 右下のボタンを、その役職のバニラの状態に戻す(前の役職のMODの画像・文字を消す)。<br/>
        /// 元の自分に戻ったときは、MODの表示をもう一度付け直す
        /// </summary>
        static void ResetHud(PlayerControl target)
        {
            var hud = HudManager.Instance;
            if (hud == null) return;
            if (CustomButtonHud.MotoKillButton) hud.KillButton.graphic.sprite = CustomButtonHud.MotoKillButton;
            if (CustomButtonHud.ImpVentButton) hud.ImpostorVentButton.graphic.sprite = CustomButtonHud.ImpVentButton;
            hud.KillButton.OverrideText(Translator.GetString(StringNames.KillLabel));
            var role = target.Data.Role;
            if (role != null && role.Ability != null) role.InitializeAbilityButton();
            else hud.AbilityButton.ToggleVisible(false);
            if (GameStates.IsMeeting) return;//会議中はボタンを出さない(会議が終わると元の処理で出る)
            //乗り移った先の役職のMODのボタン表示にする
            hud.SetHudActive(target, role, true);
            CustomButtonHud.BottonHud(true);
        }
        /// <summary>元の自分に戻る</summary>
        public static void Release()
        {
            if (hostBody != null && Possessed != null) Possess(hostBody);
            hostBody = null;
            Possessed = null;
        }
        static void SetDummy(PlayerControl pc, bool isDummy)
        {
            pc.isDummy = isDummy;
            var behaviour = pc.GetComponent<DummyBehaviour>();
            if (behaviour != null) behaviour.enabled = isDummy;
            SetOwner(pc, !isDummy);
        }
        /// <summary>元の持ち主(ダミーは持ち主なしで作られている)</summary>
        static readonly System.Collections.Generic.Dictionary<byte, (int pc, int physics, int net)> owners = new();
        /// <summary>
        /// 自分を持ち主にする/元に戻す。<br/>
        /// Among Us は自分が持ち主のプレイヤーにしか操作(移動)を流さないので、乗り移るときに切り替える
        /// </summary>
        static void SetOwner(PlayerControl pc, bool mine)
        {
            if (mine)
            {
                if (!owners.ContainsKey(pc.PlayerId))
                    owners[pc.PlayerId] = (pc.OwnerId, pc.MyPhysics.OwnerId, pc.NetTransform.OwnerId);
                var me = AmongUsClient.Instance.ClientId;
                pc.OwnerId = me;
                pc.MyPhysics.OwnerId = me;
                pc.NetTransform.OwnerId = me;
            }
            else if (owners.TryGetValue(pc.PlayerId, out var o))
            {
                pc.OwnerId = o.pc;
                pc.MyPhysics.OwnerId = o.physics;
                pc.NetTransform.OwnerId = o.net;
                owners.Remove(pc.PlayerId);
            }
        }
        /// <summary>
        /// 自分の画面での自分の役職(バニラの役職)。会議のあとにホストの画面で使う役職と同じ決め方にする
        /// (ExilePatch: インポスター扱いで動くクルー・ニュートラルはクルーメイト)
        /// </summary>
        static RoleTypes GetRoleType(PlayerControl pc)
        {
            var roleInfo = pc.GetCustomRole().GetRoleInfo();
            var role = (roleInfo?.IsDesyncImpostor == true) && roleInfo.BaseRoleType.Invoke() is RoleTypes.Impostor
                ? RoleTypes.Crewmate : (roleInfo?.BaseRoleType?.Invoke() ?? RoleTypes.Crewmate);
            if (!pc.IsAlive()) role = role.IsCrewmate() ? RoleTypes.CrewmateGhost : RoleTypes.ImpostorGhost;
            return role;
        }

        // ------------------ 会議中の乗り移り(死亡後の勝者予想のテスト) ------------------
        /// <summary>会議の間だけ、自分の画面で生きているように見せたプレイヤー</summary>
        static PlayerControl revived;
        /// <summary>
        /// 会議中に、勝者予想ができる死んだプレイヤーに乗り移ったら、自分の画面でだけ生きているように見せて投票できるようにする
        /// (MOD未導入の人にしている見せ方と同じ)
        /// </summary>
        static void ReviveForPrediction(PlayerControl target)
        {
            UndoRevive();
            if (!GameStates.IsMeeting || target == null || target.IsAlive() || !PredictionManager.IsEligible(target.PlayerId)) return;
            target.Data.IsDead = false;
            revived = target;
            foreach (var area in MeetingHud.Instance.playerStates)
                if (area.TargetPlayerId == target.PlayerId) area.AmDead = false;
            Logger.Info($"{target.Data.GetLogPlayerName()} は勝者予想ができるので、会議の間だけ生きているように見せる", "LocalTest");
        }
        /// <summary>生きているように見せたのを元に戻す(会議の終わり・別の人に乗り移ったとき)</summary>
        public static void UndoRevive()
        {
            if (revived == null) return;
            if (!revived.IsAlive()) revived.Data.IsDead = true;
            if (MeetingHud.Instance != null)
                foreach (var area in MeetingHud.Instance.playerStates)
                    if (area.TargetPlayerId == revived.PlayerId) area.AmDead = !revived.IsAlive();
            revived = null;
        }

        // ------------------ 左下のボタン ------------------
        [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update)), HarmonyPostfix]
        public static void HudUpdatePostfix(HudManager __instance)
        {
            //乗り移っている間は、元の自分の体を止めておく(会議のあとなどに動けるように戻されるため)
            if (Possessed != null && hostBody != null && hostBody.moveable)
            {
                hostBody.moveable = false;
                hostBody.MyPhysics.SetNormalizedVelocity(Vector2.zero);
            }
            //タスク中と会議中(追放の画面などは除く)
            var show = IsLocalHost && GameStates.InGame && (GameStates.IsInTask || GameStates.IsMeeting) && !GameStates.ExiledAnimate;
            if (!show)
            {
                if (button != null) button.gameObject.SetActive(false);
                return;
            }
            if (button == null) CreateButton(__instance);
            if (button == null) return;
            button.gameObject.SetActive(true);
            //会議の画面より手前に出す
            var aspect = button.GetComponent<AspectPosition>();
            if (aspect != null)
            {
                aspect.DistanceFromEdge = new Vector3(0.8f, 0.8f, GameStates.IsMeeting ? -150f : -5f);
                aspect.AdjustPosition();
            }
            button.SetEnabled();
            button.SetCoolDown(0f, 1f);
            button.OverrideText(Possessed == null ? "乗り移り" : "乗り移り中");
        }
        static void CreateButton(HudManager hud)
        {
            try
            {
                var source = hud.AbilityButton;
                if (source == null) return;
                var b = Object.Instantiate(source, hud.transform);
                b.name = "LocalTestPossessButton";
                //ボタンを押したときの処理は差し替える(AbilityButton の役職の能力は使わない)
                var shapeshifter = RoleManager.Instance.AllRoles.ToArray().FirstOrDefault(r => r.Role == RoleTypes.Shapeshifter);
                if (shapeshifter?.Ability?.Image != null) b.graphic.sprite = shapeshifter.Ability.Image;
                var passive = b.GetComponent<PassiveButton>();
                passive.OnClick = new();
                passive.OnClick.AddListener((Action)OpenPicker);
                var aspect = b.GetComponent<AspectPosition>() ?? b.gameObject.AddComponent<AspectPosition>();
                aspect.Alignment = AspectPosition.EdgeAlignments.LeftBottom;
                aspect.DistanceFromEdge = new Vector3(0.8f, 0.8f, -5f);
                aspect.AdjustPosition();
                button = b;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString(), "LocalTest");
            }
        }

        // ------------------ 乗り移る相手を選ぶ画面 ------------------
        static void OpenPicker()
        {
            if (!IsLocalHost || Minigame.Instance != null) return;
            var role = RoleManager.Instance.AllRoles.ToArray().FirstOrDefault(r => r.Role == RoleTypes.Shapeshifter)?.TryCast<ShapeshifterRole>();
            if (role == null) return;
            picking = true;
            var minigame = Object.Instantiate(role.ShapeshifterMenu, Camera.main.transform, false);
            minigame.transform.localPosition = new Vector3(0f, 0f, GameStates.IsMeeting ? -200f : -50f);//会議中は会議の画面より手前
            minigame.Begin(null);
        }

        [HarmonyPatch(typeof(ShapeshifterMinigame), nameof(ShapeshifterMinigame.Begin)), HarmonyPrefix]
        public static bool PickerBeginPrefix(ShapeshifterMinigame __instance)
        {
            if (!picking) return true;
            picking = false;

            //ミニゲームを開く処理(CustomSpawnEditor の MinigameBegin と同じ)
            Minigame.Instance = __instance;
            __instance.timeOpened = Time.realtimeSinceStartup;
            if (MapBehaviour.Instance) MapBehaviour.Instance.Close();
            PlayerControl.LocalPlayer?.MyPhysics.SetNormalizedVelocity(Vector2.zero);
            __instance.StartCoroutine(__instance.CoAnimateOpen());
            __instance.potentialVictims = new();
            Il2CppSystem.Collections.Generic.List<UiElement> selectable = new();
            //自分(今の体)以外の全員。元の自分もここから選べる
            var players = PlayerControl.AllPlayerControls.ToArray()
                .Where(pc => pc != null && pc != PlayerControl.LocalPlayer && pc.Data != null && !pc.Data.Disconnected)
                .OrderBy(pc => pc.PlayerId).ToArray();
            for (var i = 0; i < players.Length; i++)
            {
                var target = players[i];
                var panel = Object.Instantiate(__instance.PanelPrefab, __instance.transform);
                panel.transform.localPosition = new Vector3(__instance.XStart + i % 3 * __instance.XOffset, __instance.YStart + i / 3 * __instance.YOffset, -1f);
                Il2CppSystem.Action onShift = (Action)(() =>
                {
                    __instance.Close();
                    Possess(target);
                });
                panel.SetPlayer(i, target.Data, onShift);
                //役職(役職の色)・タスク数・元の自分かどうかを名前の下に出す
                panel.ColorBlindName.text = PanelText(target);
                panel.ColorBlindName.enabled = true;
                __instance.potentialVictims.Add(panel);
                selectable.Add(panel.Button);
            }
            ControllerManager.Instance.OpenOverlayMenu(__instance.name, __instance.BackButton, __instance.DefaultButtonSelected, selectable);
            return false;
        }

        /// <summary>選ぶ画面の名前の下の文字(役職は役職の色、タスクは水色、部屋タスクは黄緑)</summary>
        static string PanelText(PlayerControl pc)
        {
            var sb = new System.Text.StringBuilder();
            if (pc == hostBody) sb.Append("【元の自分】");
            var role = pc.GetCustomRole();
            sb.Append(Utils.ColorString(UtilsRoleText.GetRoleColor(role), UtilsRoleText.GetRoleName(role)));
            var state = PlayerState.GetByPlayerId(pc.PlayerId);
            var info = role.GetRoleInfo();
            //部屋タスク(ウォーカーのタスク)を持つ役職か
            var roomTask = RoomTaskSettings.GetTargetType(role) is not RoomTaskSettings.TargetType.None
                || (info?.ClassType != null && typeof(Roles.Core.Interfaces.IRoomTasker).IsAssignableFrom(info.ClassType));
            if (state != null && (roomTask || UtilsTask.HasTasks(pc.Data)))
            {
                var task = $"({state.taskState.CompletedTasksCount}/{state.taskState.AllTasksCount})";
                sb.Append(' ').Append(Utils.ColorString(roomTask ? new Color32(154, 205, 50, 255) : new Color32(0, 255, 255, 255), task));
            }
            if (!pc.IsAlive()) sb.Append("（死亡）");
            return sb.ToString();
        }

        // ------------------ 試合の終わり ------------------
        [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd)), HarmonyPrefix]
        public static void OnGameEndPrefix()
        {
            //試合が終わる前に元の自分に戻る(結果画面などはホストの体で動くため)
            if (Possessed != null) Release();
            if (button != null) Object.Destroy(button.gameObject);
            button = null;
        }
    }
}

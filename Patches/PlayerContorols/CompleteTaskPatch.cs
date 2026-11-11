using AmongUs.GameOptions;
using HarmonyLib;

using TownOfHost.Roles.Core;

namespace TownOfHost
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
    class PlayerControlCompleteTaskPatch
    {
        public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] uint taskid)
        {
            var pc = __instance;

            Logger.Info($"TaskComplete:{pc.GetNameWithRole().RemoveHtmlTags()} {taskid}", "CompleteTask");
            var taskState = pc.GetPlayerTaskState();
            taskState.Update(pc);
            TitleManager.OnTask(pc.PlayerId);//肩書用に数える

            var roleClass = pc.GetRoleClass();
            var roleinfo = pc.GetCustomRole().GetRoleInfo();
            var ret = true;

            //タスクバトルの処理　タスバトならこれ以外の処理いらない。
            if (Options.CurrentGameMode == CustomGameMode.TaskBattle)
            {
                ret &= TaskBattle.TaskBattleCompleteTask(pc, taskState);
                return ret;
            }
            if (Options.CurrentGameMode == CustomGameMode.MurderMystery)
            {
                ret &= true;
                UtilsNotifyRoles.NotifyRoles();
                UtilsGameLog.AddGameLog("Task", string.Format(Translator.GetString("Taskfin"), UtilsName.GetPlayerColor(pc, true)));
                return ret;
            }

            if (roleClass != null)
            {
                    ret = roleClass.OnCompleteTask(taskid);
            }

            CustomRoleManager.OnTaskCompleteOthers(__instance, ret);


            UtilsNotifyRoles.NotifyRoles();

            if (taskState.IsTaskFinished && pc.PlayerId == PlayerControl.LocalPlayer.PlayerId)
            {
                RemoveDisableDevicesPatch.UpdateDisableDevices();
            }
            if (ret)
            {
                if (taskState.CompletedTasksCount < taskState.AllTasksCount) return ret;
                if (!UtilsTask.HasTasks(pc.Data)) return ret;
                UtilsGameLog.AddGameLog("Task", string.Format(Translator.GetString("Taskfin"), UtilsName.GetPlayerColor(pc, true)));
            }
            return ret;
        }
        public static void Postfix()
        {
            //人外のタスクを排除して再計算
            GameData.Instance.RecomputeTaskCounts();
            Logger.Info($"TotalTaskCounts = {GameData.Instance.CompletedTasks}/{GameData.Instance.TotalTasks}", "TaskState.Update");
        }
    }
}
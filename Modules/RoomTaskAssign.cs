
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost;

public class RoomTaskAssign
{
    public static Dictionary<byte, RoomTaskAssign> AllRoomTasker = new();
    public byte playerid; public PlayerState Mystate;
    public float timer;
    public int completeroom;
    public SystemTypes? TaskRoom;
    public PlainShipRoom TaskPSR;
    public Vector2 RoomArrow;

    //── 汎用の部屋タスク(RoomTaskSettings。インポスター置き換えのクルー・ニュートラル用) ──
    /// <summary>役職クラスではなく RoomTaskSettings から割り当てた部屋タスクか</summary>
    public bool Generic;
    public RoomTaskSettings.TargetType TargetType;
    /// <summary>割り当てる部屋タスクの種類の並び(この数が総タスク数)</summary>
    public List<RoomTaskKind> Queue = new();
    /// <summary>今の部屋にいた合計秒数(部屋を出ても途中から再開)</summary>
    public float stay;
    RoomTaskKind NowKind => Queue.Count > 0 ? Queue[System.Math.Min(completeroom, Queue.Count - 1)] : RoomTaskKind.Short;
    float NowWait => RoomTaskSettings.GetWait(TargetType, NowKind);

    /// <summary>今、部屋タスクを割り当てるか</summary>
    bool IsAssign()
        => Generic ? completeroom < Queue.Count
        : (playerid.GetPlayerControl()?.GetRoleClass() as IRoomTasker)?.IsAssignRoomTask() is true;

    public RoomTaskAssign(byte playerid)
    {
        this.playerid = playerid;
        timer = 0;
        completeroom = 0;
        TaskRoom = null;
        TaskPSR = null;
        RoomArrow = new Vector2(50, 50);
        Mystate = playerid.GetPlayerState();
    }

    public void fixupdate()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!IsAssign()) return;

        //TaskRoomがnullの場合、再設定する
        if (!TaskRoom.HasValue)
        {
            ChengeRoom();
        }
        else //ある場合
        {
            if (Mystate.HasSpawned) timer += Time.fixedDeltaTime;
            if (timer <= 0.1f) return; //変わってすぐは処理しない...

            var player = playerid.GetPlayerControl();
            var nowroom = player.GetPlainShipRoom(true);
            if (nowroom == null) return;

            if (Generic)
            {
                //部屋にいる間だけ待ち時間を数え、設定秒数たまったら完了
                if (TaskRoom != nowroom.RoomId) return;
                if (Mystate.HasSpawned) stay += Time.fixedDeltaTime;
                if (stay < NowWait) return;
                CompleteGeneric(player);
                return;
            }

            if (TaskRoom == nowroom.RoomId)
            {
                if (timer > 0.5f)
                {
                    Logger.Info($"{TaskRoom}に{player.name}が来たよ", "RoomTaskAssign");
                    TaskRoom = null;
                    TaskPSR = null;
                    completeroom++;
                    timer = 0;
                    RPC.PlaySoundRPC(playerid, Sounds.TaskComplete);
                    TitleManager.OnWalkTask(playerid);//肩書用に数える

                    (player?.GetRoleClass() as IRoomTasker)?.OnComplete(completeroom);

                    var ret = true;
                    if (ret)
                    {
                        var taskstate = player.GetPlayerTaskState();
                        if (taskstate.CompletedTasksCount < taskstate.AllTasksCount || !UtilsTask.HasTasks(player.Data))
                        { }
                        else UtilsGameLog.AddGameLog("Task", string.Format(Translator.GetString("Taskfin"), UtilsName.GetPlayerColor(player, true)));
                    }
                }
                else
                    Logger.Info($"{TaskRoom}にはもう既にいたから変更するよ", "RoomTaskAssign");
                ChengeRoom();
            }
        }
    }
    void CompleteGeneric(PlayerControl player)
    {
        Logger.Info($"{TaskRoom}で{player.name}が{NowKind}タスク完了({stay:0.0}秒)", "RoomTaskAssign");
        TaskRoom = null;
        TaskPSR = null;
        completeroom++;
        timer = 0;
        stay = 0;
        RPC.PlaySoundRPC(playerid, Sounds.TaskComplete);
        TitleManager.OnWalkTask(playerid);//肩書用に数える

        var taskstate = player.GetPlayerTaskState();
        taskstate.Update(player);
        if (taskstate.IsTaskFinished)
            UtilsGameLog.AddGameLog("Task", string.Format(Translator.GetString("Taskfin"), UtilsName.GetPlayerColor(player, true)));

        if (IsAssign()) ChengeRoom();
        else
        {
            if (RoomArrow != Vector2.zero) GetArrow.Remove(playerid, RoomArrow);
            RoomArrow = Vector2.zero;
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: player);
        }
    }
    void ChengeRoom()
    {
        if (!IsAssign()) return;
        stay = 0;
        //汎用の部屋タスクは、今いる部屋も行き先から外す(着いた瞬間に数え始めないように)
        var nowRoomId = Generic ? playerid.GetPlayerControl()?.GetPlainShipRoom(true)?.RoomId : null;
        List<PlainShipRoom> rooms = new();
        ShipStatus.Instance.AllRooms.Where(room => room?.RoomId is not null and not SystemTypes.Hallway && room?.RoomId != TaskRoom && room?.RoomId != nowRoomId).Do(r => rooms.Add(r));

        var rand = IRandom.Instance;
        TaskPSR = rooms[rand.Next(0, rooms.Count)];
        if (RoomArrow != Vector2.zero) GetArrow.Remove(playerid, RoomArrow);

        RoomArrow = TaskPSR.transform.position;
        if (Main.NormalOptions.MapId is 4)//矢印が変なところにある場所の応急手当。
        {
            switch (TaskPSR.RoomId)
            {
                case SystemTypes.Brig: RoomArrow = new Vector2(-0.7f, 8.5f); break;
                case SystemTypes.VaultRoom: RoomArrow = new Vector2(-8.9f, 12.2f); break;
                case SystemTypes.Ventilation: RoomArrow = new Vector2(27, -1); break;
            }
        }
        GetArrow.Add(playerid, RoomArrow);
        TaskRoom = TaskPSR.RoomId;

        (playerid.GetPlayerControl()?.GetRoleClass() as IRoomTasker)?.ChangeRoom(TaskPSR);

        Logger.Info($"NextTask : {TaskRoom}", "Walker");
        _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: playerid.GetPlayerControl()), 0.3f, "RoomTaskAssign_ChangeRoom", null);
    }
    public void ReceiveRoom(MessageReader reader)
    {
        var roomId = (SystemTypes)reader.ReadByte();
        TaskPSR = ShipStatus.Instance.AllRooms.FirstOrDefault(x => x.RoomId == roomId);

        if (TaskPSR == null)
        {
            Logger.Error($"{roomId}の部屋を見つけることができませんでした ShipStatus:{ShipStatus.Instance.name}", "RoomTaskAssign_ReceiveRoom");
            return;
        }

        if (RoomArrow != Vector2.zero) GetArrow.Remove(playerid, RoomArrow);

        RoomArrow = TaskPSR.transform.position;
        GetArrow.Add(playerid, RoomArrow);
        TaskRoom = TaskPSR.RoomId;

        Logger.Info($"{playerid}-NextTask : {TaskRoom}", "RoomTaskAssign_ReceiveRoom");
    }

    public void ReceiveCompleteRoom(MessageReader reader)
    {
        TaskRoom = null;
        TaskPSR = null;
        if (reader.BytesRemaining > 0)
        {
            completeroom = reader.ReadInt32();
        }
    }

    public string GetLowerText(PlayerControl seer, string colorcode = "#ffffff")
    {
        if (TaskRoom == null || !IsAssign()) return "";
        var text = $"<{colorcode}>{GetArrow.GetArrows(seer, [RoomArrow])}" +
            $"{string.Format(Translator.GetString("FoxRoomMission"), $"<color=#cccccc><b>{Translator.GetString($"{TaskRoom}")}<b></color>")}</color>";
        //汎用の部屋タスクは種類と必要な秒数も出す
        if (Generic) text += $" <size=80%>({Translator.GetString($"RoomTaskKind.{NowKind}")} {NowWait}{Translator.GetString("RoomTaskSeconds")})</size>";
        return text;
    }

    public void OnStartMeeting()
    {
        timer = 0;
        stay = 0;
        TaskRoom = null;
        TaskPSR = null;
        GetArrow.Remove(playerid, RoomArrow);
        RoomArrow = Vector2.zero;
    }
}
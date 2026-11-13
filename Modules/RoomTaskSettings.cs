using System.Collections.Generic;
using System.Linq;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost
{
    /// <summary>ウォークタスクの種類(クルーのタスク数と同じ分け方)</summary>
    public enum RoomTaskKind { Common, Short, Long }

    /// <summary>
    /// インポスター置き換え(デシンクインポスター)のクルー・ニュートラル役職に割り当てる「ウォークタスク」の設定と判定。<br/>
    /// 設定で、普通のクルー(クルーメイト置き換えなど)にも普通のタスクの代わりに割り当てられる。<br/>
    /// 部屋へ行って、種類ごとに設定した秒数その部屋にいると1タスク完了(部屋を出ても待ち時間は途中から再開)。<br/>
    /// 役職のクラスに手を入れずに割り当てるので、今後追加する置き換え役職にも自動で付く。
    /// </summary>
    public static class RoomTaskSettings
    {
        public enum TargetType { None, Crewmate, Neutral }

        //クルーメイト(タスク数はクルーメイトの共通設定を使う)
        public static OptionItem CrewWaitCommon, CrewWaitShort, CrewWaitLong;
        /// <summary>インポスター置き換え以外のクルー(クルーメイト・千里眼など)にもウォークタスクを割り当てるか</summary>
        public static OptionItem CrewAll;
        //ニュートラル
        public static OptionItem NeutralEnable, NeutralOverride;
        public static OptionItem NeutralNumCommon, NeutralNumShort, NeutralNumLong;
        public static OptionItem NeutralWaitCommon, NeutralWaitShort, NeutralWaitLong;

        const int IdStart = 106020;

        /// <summary>クルーメイトタブ(陣営共通設定の続き)</summary>
        public static void SetupCrewmateOptions()
        {
            var tab = TabGroup.CrewmateRoles;
            var crew = new Color32(140, 255, 255, byte.MaxValue);
            CrewWaitCommon = FloatOptionItem.Create(IdStart, "RoomTaskWaitCommon", new(0.5f, 180f, 0.5f), 10f, tab, false)
                .SetValueFormat(OptionFormat.Seconds).SetColor(crew);
            CrewWaitShort = FloatOptionItem.Create(IdStart + 1, "RoomTaskWaitShort", new(0.5f, 180f, 0.5f), 5f, tab, false)
                .SetValueFormat(OptionFormat.Seconds).SetColor(crew);
            CrewWaitLong = FloatOptionItem.Create(IdStart + 2, "RoomTaskWaitLong", new(0.5f, 180f, 0.5f), 15f, tab, false)
                .SetValueFormat(OptionFormat.Seconds).SetColor(crew);
            CrewAll = BooleanOptionItem.Create(IdStart + 3, "CrewRoomTaskAll", false, tab, false)
                .SetColor(crew);
        }

        /// <summary>ニュートラルタブ: ジャッカル陣営以外の置き換え役職にタスクを割り当てるか → 個別に設定するか → 個別の値</summary>
        public static void SetupNeutralOptions()
        {
            var tab = TabGroup.NeutralRoles;
            NeutralEnable = BooleanOptionItem.Create(IdStart + 10, "NeutralRoomTask", false, tab, false)
                .SetHeader(true)
                .SetColorcode("#cccccc");
            NeutralOverride = BooleanOptionItem.Create(IdStart + 11, "NeutralRoomTaskOverride", false, tab, false)
                .SetParent(NeutralEnable);
            NeutralNumCommon = IntegerOptionItem.Create(IdStart + 12, "CrewmateCommonNumCommonTasks", new(0, 99, 1), 2, tab, false)
                .SetValueFormat(OptionFormat.Pieces).SetParent(NeutralOverride);
            NeutralNumShort = IntegerOptionItem.Create(IdStart + 13, "CrewmateCommonNumShortTasks", new(0, 99, 1), 4, tab, false)
                .SetValueFormat(OptionFormat.Pieces).SetParent(NeutralOverride);
            NeutralNumLong = IntegerOptionItem.Create(IdStart + 14, "CrewmateCommonNumLongTasks", new(0, 99, 1), 2, tab, false)
                .SetValueFormat(OptionFormat.Pieces).SetParent(NeutralOverride);
            NeutralWaitCommon = FloatOptionItem.Create(IdStart + 15, "RoomTaskWaitCommon", new(0.5f, 180f, 0.5f), 10f, tab, false)
                .SetValueFormat(OptionFormat.Seconds).SetParent(NeutralOverride);
            NeutralWaitShort = FloatOptionItem.Create(IdStart + 16, "RoomTaskWaitShort", new(0.5f, 180f, 0.5f), 5f, tab, false)
                .SetValueFormat(OptionFormat.Seconds).SetParent(NeutralOverride);
            NeutralWaitLong = FloatOptionItem.Create(IdStart + 17, "RoomTaskWaitLong", new(0.5f, 180f, 0.5f), 15f, tab, false)
                .SetValueFormat(OptionFormat.Seconds).SetParent(NeutralOverride);
        }

        /// <summary>この役職がウォークタスクの対象か(クルー/ニュートラル)</summary>
        public static TargetType GetTargetType(CustomRoles role)
        {
            if (Options.CurrentGameMode is not CustomGameMode.Standard) return TargetType.None;
            var info = role.GetRoleInfo();
            //設定がオンなら、普通のクルー(バニラのクルー役職・クルーメイト置き換え)にも普通のタスクの代わりに割り当てる
            if (CrewAll?.GetBool() == true && (info == null ? role.IsVanilla() && !role.IsImpostor() && role.IsCrewmate() : !info.IsDesyncImpostor && info.CustomRoleType == CustomRoleTypes.Crewmate))
            {
                if (info?.ClassType != null && typeof(IRoomTasker).IsAssignableFrom(info.ClassType)) return TargetType.None;
                return TargetType.Crewmate;
            }
            if (info == null || !info.IsDesyncImpostor) return TargetType.None;
            //自前でウォークタスクを持つ役職(マーリン・妖狐など)は対象外
            if (info.ClassType != null && typeof(IRoomTasker).IsAssignableFrom(info.ClassType)) return TargetType.None;
            switch (info.CustomRoleType)
            {
                case CustomRoleTypes.Crewmate:
                    return TargetType.Crewmate;
                case CustomRoleTypes.Neutral:
                    if (role.IsJackalTeam()) return TargetType.None;
                    return NeutralEnable?.GetBool() == true ? TargetType.Neutral : TargetType.None;
                default:
                    return TargetType.None;
            }
        }

        static bool UseNeutralValues(TargetType type) => type is TargetType.Neutral && NeutralOverride?.GetBool() == true;

        public static int GetCount(TargetType type, RoomTaskKind kind)
        {
            if (type is TargetType.None) return 0;
            var neutral = UseNeutralValues(type);
            return kind switch
            {
                RoomTaskKind.Common => (neutral ? NeutralNumCommon : FactionCommonOptions.NumCommonTasks)?.GetInt() ?? 0,
                RoomTaskKind.Short => (neutral ? NeutralNumShort : FactionCommonOptions.NumShortTasks)?.GetInt() ?? 0,
                RoomTaskKind.Long => (neutral ? NeutralNumLong : FactionCommonOptions.NumLongTasks)?.GetInt() ?? 0,
                _ => 0
            };
        }

        public static float GetWait(TargetType type, RoomTaskKind kind)
        {
            var neutral = UseNeutralValues(type);
            return kind switch
            {
                RoomTaskKind.Common => (neutral ? NeutralWaitCommon : CrewWaitCommon)?.GetFloat() ?? 5f,
                RoomTaskKind.Short => (neutral ? NeutralWaitShort : CrewWaitShort)?.GetFloat() ?? 5f,
                RoomTaskKind.Long => (neutral ? NeutralWaitLong : CrewWaitLong)?.GetFloat() ?? 5f,
                _ => 5f
            };
        }

        /// <summary>対象プレイヤーならウォークタスクの総数、対象外なら null</summary>
        public static int? GetTotal(byte playerId)
            => RoomTaskAssign.AllRoomTasker.TryGetValue(playerId, out var data) && data.Generic ? data.Queue.Count : null;

        /// <summary>役職を作ったときに呼ぶ(CustomRoleManager.CreateInstance)</summary>
        public static void AddTasker(PlayerControl player, CustomRoles role)
        {
            var type = GetTargetType(role);
            if (type is TargetType.None)
            {
                //役職が変わって対象外になったら、前のウォークタスクを消す
                if (RoomTaskAssign.AllRoomTasker.TryGetValue(player.PlayerId, out var old) && old.Generic)
                {
                    if (old.RoomArrow != Vector2.zero) GetArrow.Remove(player.PlayerId, old.RoomArrow);
                    RoomTaskAssign.AllRoomTasker.Remove(player.PlayerId);
                }
                return;
            }

            var queue = new List<RoomTaskKind>();
            foreach (var kind in EnumHelper.GetAllValues<RoomTaskKind>())
                for (var i = 0; i < GetCount(type, kind); i++) queue.Add(kind);
            //種類の順番はランダム
            var rand = IRandom.Instance;
            queue = queue.OrderBy(_ => rand.Next(int.MaxValue)).ToList();

            RoomTaskAssign.AllRoomTasker[player.PlayerId] = new RoomTaskAssign(player.PlayerId)
            {
                Generic = true,
                TargetType = type,
                Queue = queue,
            };
            CustomRoleManager.LowerOthers.Add(GetLowerTextOthers);
            Logger.Info($"{player.name}にウォークタスクを{queue.Count}個割り当て({type})", "RoomTaskSettings");
        }

        /// <summary>
        /// 試合の途中でウォークタスクを割り当てる(ニートが就職したときなど)。待ち時間はクルーの設定を使う
        /// </summary>
        public static void AssignMidGame(PlayerControl player, RoomTaskKind kind, int count)
        {
            if (player == null || count <= 0) return;
            var queue = Enumerable.Repeat(kind, count).ToList();
            RoomTaskAssign.AllRoomTasker[player.PlayerId] = new RoomTaskAssign(player.PlayerId)
            {
                Generic = true,
                TargetType = TargetType.Crewmate,
                Queue = queue,
            };
            CustomRoleManager.LowerOthers.Add(GetLowerTextOthers);
            //タスクの数を数え直す(ウォークタスクの数になる)
            var taskState = player.GetPlayerTaskState();
            if (taskState != null)
            {
                //試合開始時にタスクが無かった人は「必要なタスク数」が0になっていて、(1/0)のように出てしまうので戻す
                taskState.NeedTaskCount = -1;
                taskState.Init(player);
            }
            Logger.Info($"{player.name}に途中からウォークタスクを{count}個割り当て({kind})", "RoomTaskSettings");
        }

        /// <summary>自分の画面の下に「〇〇へ行け(ショート 5秒)」を出す</summary>
        public static string GetLowerTextOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
        {
            seen ??= seer;
            if (isForMeeting || seer == null || seen.PlayerId != seer.PlayerId) return "";
            if (!RoomTaskAssign.AllRoomTasker.TryGetValue(seer.PlayerId, out var data) || !data.Generic) return "";
            var color = seer.GetCustomRole().GetRoleInfo()?.RoleColorCode ?? "#ffffff";
            return data.GetLowerText(seer, color);
        }
    }
}

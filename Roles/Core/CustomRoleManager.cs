using System;
using System.Linq;
using System.Collections.Generic;
using Il2CppSystem.Text;
using UnityEngine;
using Hazel;
using HarmonyLib;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using TownOfHost.Roles.Impostor;
using TownOfHost.Modules;
using TownOfHost.Roles.Vanilla;

namespace TownOfHost.Roles.Core;

public static class CustomRoleManager
{
    public static Type[] AllRolesClassType;
    public static Dictionary<CustomRoles, SimpleRoleInfo> AllRolesInfo = new(CustomRolesHelper.AllRoles.Length);
    public static Dictionary<byte, RoleBase> AllActiveRoles = new(15);

    public static SimpleRoleInfo GetRoleInfo(this CustomRoles role) => AllRolesInfo.ContainsKey(role) ? AllRolesInfo[role] : null;
    public static RoleBase GetRoleClass(this PlayerControl player) => player is null ? null : GetByPlayerId(player.PlayerId);
    public static RoleBase GetByPlayerId(byte playerId) => AllActiveRoles.TryGetValue(playerId, out var roleBase) ? roleBase : null;
    public static void Do<T>(this List<T> list, Action<T> action) => list.ToArray().Do(action);
    public static Dictionary<int, CustomRoles> CustomRoleIds = new();
    public static List<CustomRoles> SortCustomRoles = new();
    // == CheckMurder関連処理 ==
    public static Dictionary<byte, MurderInfo> CheckMurderInfos = new();

    /// <summary>
    ///
    /// </summary>
    /// <param name="attemptKiller">実際にキルを行ったプレイヤー 不変</param>
    /// <param name="attemptTarget">>Killerが実際にキルを行おうとしたプレイヤー 不変</param>
    public static bool OnCheckMurder(PlayerControl attemptKiller, PlayerControl attemptTarget)
    {
        if (attemptKiller.Is(CustomRoles.Powerful))
        {
            return OnCheckMurder(attemptKiller, attemptTarget, attemptKiller, attemptTarget, Killpower: 2);
        }
        return OnCheckMurder(attemptKiller, attemptTarget, attemptKiller, attemptTarget);
    }
    /// <param name="attemptKiller">実際にキルを行ったプレイヤー 不変</param>
    /// <param name="attemptTarget">Killerが実際にキルを行おうとしたプレイヤー 不変</param>
    /// <param name="appearanceKiller">見た目上でキルを行うプレイヤー 可変</param>
    /// <param name="appearanceTarget">見た目上でキルされるプレイヤー 可変</param>
    /// <param name="force">RoleClassのCheckMurder/Guardのチェックをスルーしてキルを行うか</param>
    /// <param name="RoleAbility">キル後のMurderPlayerの処理を行うか</param>
    /// <param name="Killpower">キルの強さ</param>
    /// <param name="deathReason">死因</param>
    /// <returns></returns>
    public static bool OnCheckMurder(PlayerControl attemptKiller, PlayerControl attemptTarget, PlayerControl appearanceKiller, PlayerControl appearanceTarget, bool? force = false, bool? DontRoleAbility = false, int Killpower = 1,
    CustomDeathReason deathReason = CustomDeathReason.Kill)
    {
        Logger.Info($"Attempt  :{attemptKiller.GetNameWithRole().RemoveHtmlTags()} => {attemptTarget.GetNameWithRole().RemoveHtmlTags()}", "CheckMurder");
        if (appearanceKiller != attemptKiller || appearanceTarget != attemptTarget)
            Logger.Info($"Apperance:{appearanceKiller.GetNameWithRole().RemoveHtmlTags()} => {appearanceTarget.GetNameWithRole().RemoveHtmlTags()}", "CheckMurder");

        var info = new MurderInfo(attemptKiller, attemptTarget, appearanceKiller, appearanceTarget, DontRoleAbility, Killpower, 0, deathReason);

        appearanceKiller.ResetKillCooldown();

        // 無効なキルをブロックする処理 必ず最初に実行する
        if (!CheckMurderPatch.CheckForInvalidMurdering(info, force == true))
        {
            return false;
        }

        var killerRole = attemptKiller.GetRoleClass();
        var targetRole = attemptTarget.GetRoleClass();
        int GuardreasonNumber = -1;

        // キラーがキル能力持ちなら
        if (killerRole is IKiller killer)
        {
            if (killer.IsKiller)
            {
                if (killerRole is EarnestWolf earnestWolf)//最優先
                {
                        if (earnestWolf.OnCheckMurderAsEarnestWolf(info))
                            return true;
                }

                if (targetRole != null && info.DontRoleAbility is not true)
                {
                    {
                        if (!targetRole.OnCheckMurderAsTarget(info))
                        {
                            killer.OnCheckMurderDontKill(info);
                            CheckMurderPatch.TimeSinceLastKill[attemptKiller.PlayerId] = 0f;//タゲ側でガードされるときってキルガードだけのはずだから。
                            return false;
                        }
                    }
                }

                //属性ガードのチェック
                if (info.KillPower > info.GuardPower)//消費する必要がある
                {
                    var state = attemptTarget.GetPlayerState();
                    var CanuseGuards = state.HaveGuard.Where(data => data.Value > 0).Where(data => info.KillPower <= data.Key);

                    if (CanuseGuards.Count() > 0)//今ここで使えるガードがある場合
                    {
                        info.GuardPower = CanuseGuards.First().Key;
                        GuardreasonNumber = 0;
                    }
                }
            }

            // キラーのキルチェック処理実行
            //ダブルトリガー無効なら通常処理
            if (!DoubleTrigger.OnCheckMurderAsKiller(info) && force is false)//特殊強制キルの場合は処理しない
            {
                killer.OnCheckMurderAsKiller(info);
            }

            /* キル可能かのチェック */
            if (info.KillPower <= info.GuardPower && killer.IsKiller)
            {
                info.IsGuard = true;
                info.CanKill = false;
                if (GuardreasonNumber is -1) GuardreasonNumber = 3;
            }

            if (info.IsGuard && killer.IsKiller)
            {
                switch (GuardreasonNumber)
                {
                    case 0: //AddonGuard
                        var state = attemptTarget.GetPlayerState();
                        var CanuseGuards = state.HaveGuard.Where(data => data.Value > 0).Where(data => info.KillPower <= data.Key);

                        if (CanuseGuards.Count() > 0)//今ここで使えるガードがある場合
                        {
                            state.HaveGuard[CanuseGuards.First().Key] += -1;
                        }
                        var HaveGuardCount = 0;
                        state.HaveGuard.Do(data =>
                        {
                            HaveGuardCount += data.Value;
                        });

                        UtilsGameLog.AddGameLog($"Guard", UtilsName.GetPlayerColor(attemptTarget) + ":  " + string.Format(Translator.GetString("GuardMaster.Guard"), UtilsName.GetPlayerColor(attemptKiller, true)));
                        Logger.Info($"{attemptTarget.GetNameWithRole().RemoveHtmlTags()} ガード残り : {HaveGuardCount}", "Guarding");
                        break;
                    case 3://Role
                        break;
                    default:
                        break;
                }
                attemptKiller.RpcProtectedMurderPlayer(attemptTarget);
                CheckMurderPatch.TimeSinceLastKill[attemptKiller.PlayerId] = 0f;
                UtilsNotifyRoles.NotifyRoles();
                killer.OnCheckMurderDontKill(info);
                return false;
            }
        }

        //キル可能だった場合のみMurderPlayerに進む
        if (info.CanKill && info.DoKill)//ノイメ対応
        {
            if ((appearanceKiller.GetCustomRole() is CustomRoles.Viper || (appearanceKiller.GetRoleClass()?.HaveAddRole() is CustomRoles.Viper)) && !info.IsFakeSuicide && !info.IsSuicide)//DesyncImp役職だと死体が溶けないので一瞬だけViperにする。
            {
                if (AmongUsClient.Instance.AmHost)
                    foreach (var pc in PlayerCatch.AllPlayerControls)
                    {
                        appearanceKiller.RpcSetRoleDesync(RoleTypes.Viper, pc.GetClientId());
                    }
            }
            if (info.DontRoleAbility is false)
            {
                if (appearanceTarget.GetCustomRole().GetRoleInfo()?.BaseRoleType.Invoke() == RoleTypes.Noisemaker)
                {
                    if (AmongUsClient.Instance.AmHost)
                        foreach (var pc in PlayerCatch.AllPlayerControls)
                        {
                            if (pc == PlayerControl.LocalPlayer)
                                appearanceTarget.StartCoroutine(appearanceTarget.CoSetRole(RoleTypes.Noisemaker, true));
                            else
                                appearanceTarget.RpcSetRoleDesync(RoleTypes.Noisemaker, pc.GetClientId());
                        }
                }
            }

            if (info.DontRoleAbility is false)
                Psychic.CanAbility(appearanceTarget);

            //MurderPlayer用にinfoを保存
            CheckMurderInfos[appearanceKiller.PlayerId] = info;
            appearanceKiller.RpcMurderPlayer(appearanceTarget);

            if (info.AppearanceKiller.GetCustomRole() is CustomRoles.Viper)
            {
                foreach (var pc in PlayerCatch.AllPlayerControls)
                {
                    if (pc.IsModClient()) continue;
                    if (pc.PlayerId == info.AppearanceKiller.PlayerId || (pc.GetCustomRole().IsImpostor() && !pc.Is(CustomRoles.OneWolf)) || pc.GetCustomRole() is CustomRoles.Egoist) continue;
                    _ = new LateTask(() => info.AppearanceKiller.RpcSetRoleDesync(RoleTypes.Crewmate, pc.GetClientId()), 0.5f, "SetCrew", true); ;
                }
            }
            return true;
        }
        else
        {
            if (!info.CanKill) Logger.Info($"{appearanceTarget.GetNameWithRole().RemoveHtmlTags()}をキル出来ない。", "CheckMurder");
            if (!info.DoKill) Logger.Info($"{appearanceKiller.GetNameWithRole().RemoveHtmlTags()}はキルしない。", "CheckMurder");
            return false;
        }
    }
    /// <summary>
    /// MurderPlayer実行後の各役職処理
    /// </summary>
    /// <param name="appearanceKiller">見た目上でキルを行うプレイヤー 可変</param>
    /// <param name="appearanceTarget">見た目上でキルされるプレイヤー 可変</param>
    public static void OnMurderPlayer(PlayerControl appearanceKiller, PlayerControl appearanceTarget)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        //MurderInfoの取得
        if (CheckMurderInfos.TryGetValue(appearanceKiller.PlayerId, out var info))
        {
            //参照出来たら削除
            CheckMurderInfos.Remove(appearanceKiller.PlayerId);
        }
        else
        {
            //CheckMurderを経由していない場合はappearanceで処理
            info = new MurderInfo(appearanceKiller, appearanceTarget, appearanceKiller, appearanceTarget);
        }
        var killerstate = appearanceKiller.GetPlayerState();
        killerstate.LastKillPosition = info.killerpos;
        killerstate.Killcount++;

        (var attemptKiller, var attemptTarget) = info.AttemptTuple;

        var roleability = info.DontRoleAbility;

        Logger.Info($"Real Killer={attemptKiller.GetNameWithRole().RemoveHtmlTags()}", "MurderPlayer");

        //キラーの処理
        var killerrole = attemptKiller.GetRoleClass();
        if (roleability is false or null)
        {
                (killerrole as IKiller)?.OnMurderPlayerAsKiller(info);
        }

        //ターゲットの処理
        var targetRole = attemptTarget.GetRoleClass();

        if (roleability is false)
        {
                if (targetRole != null)
                    targetRole.OnMurderPlayerAsTarget(info);
        }

        //その他視点の処理があれば実行
        foreach (var onMurderPlayer in OnMurderPlayerOthers.ToArray())
        {
            onMurderPlayer(info);
        }


        //以降共通処理
        var targetState = PlayerState.GetByPlayerId(attemptTarget.PlayerId);
        if (targetState.DeathReason == CustomDeathReason.etc)
        {
            //死因が設定されていない場合は死亡判定
            targetState.DeathReason = info?.DeathReason ?? CustomDeathReason.Kill;
        }
        //あっ!死ぬ前にどこにいたかだけ教えてね!
        var roomName = attemptTarget.GetShipRoomName();
        targetState.KillRoom = roomName;

        targetState.SetDead();
        attemptTarget.SetRealKiller(attemptKiller, true);
        killerstate.Is10secKillButton = false;


        PlayerCatch.CountAlivePlayers(true);

        Utils.TargetDies(info);
        AllActiveRoles.Do(role => role.Value.OnDead(attemptTarget));

        //サドンデスの初手キル処理なので、バウハン等の影響受けないように限定にしておく
        if (SuddenDeathMode.NowSuddenDeathMode && appearanceKiller.GetPlayerState().Is10secKillButton)
            appearanceKiller.ResetKillCooldown();
        UtilsOption.SyncAllSettings();
        UtilsNotifyRoles.NotifyRoles();
        //サブロールは表示めんどいしながいから省略★
        if (PlayerState.GetByPlayerId(appearanceTarget.PlayerId).DeathReason != CustomDeathReason.Guess && !GameStates.CalledMeeting)
        {
            UtilsGameLog.AddGameLog($"Kill", $"{UtilsName.GetPlayerColor(appearanceTarget, true)}({UtilsRoleText.GetTrueRoleName(appearanceTarget.PlayerId, false).RemoveSizeTags()}) [{Utils.GetVitalText(appearanceTarget.PlayerId, true)}]〔{roomName}〕");
            if (appearanceKiller != appearanceTarget) UtilsGameLog.AddGameLogsub($"\n\t⇐ {UtilsName.GetPlayerColor(appearanceKiller, true)}({UtilsRoleText.GetTrueRoleName(appearanceKiller.PlayerId, false)})");
        }

        if (Options.CurrentGameMode is CustomGameMode.HideAndSeek && targetState.MainRole is CustomRoles.HASTroll)
        {
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.HASTroll);
            CustomWinnerHolder.WinnerIds.Add(appearanceTarget.PlayerId);
        }

        if (info.IsFakeSuicide || info.IsSuicide) return;

        //if (info.AppearanceKiller.PlayerId == info.AttemptKiller.PlayerId)
        if (killerrole is IUsePhantomButton usePhantomButton)
        {
            usePhantomButton.Init(appearanceKiller);
            if (usePhantomButton.IsresetAfterKill) appearanceKiller.RpcResetAbilityCooldown();
        }
        var roleinfo = appearanceKiller.GetCustomRole().GetRoleInfo();

    }
    /// <summary>
    /// その他視点からのMurderPlayer処理
    /// 初期化時にOnMurderPlayerOthers+=で登録
    /// </summary>
    public static HashSet<Action<MurderInfo>> OnMurderPlayerOthers = new();
    public static void OnFixedUpdate(PlayerControl player)
    {
        if (GameStates.IsInTask && !GameStates.CalledMeeting && (!Options.firstturnmeeting || !MeetingStates.FirstMeeting))
        {
            player.GetRoleClass()?.OnFixedUpdate(player);
            //その他視点処理があれば実行
            foreach (var onFixedUpdate in OnFixedUpdateOthers)
            {
                onFixedUpdate(player);
            }
        }
    }
    /// <summary>
    /// タスクターンに常時呼ばれる関数
    /// 他役職への干渉用
    /// Host以外も呼ばれるので注意
    /// 初期化時にOnFixedUpdateOthers+=で登録
    /// </summary>
    public static HashSet<Action<PlayerControl>> OnFixedUpdateOthers = new();

    public static bool OnSabotage(PlayerControl player, SystemTypes systemType)
    {
        bool cancel = false;
        foreach (var roleClass in AllActiveRoles.Values)
        {
            if (!roleClass.OnSabotage(player, systemType))
            {
                cancel = true;
            }
        }
        return !cancel;
    }
    // ==初期化関連処理 ==
    public static void Initialize()
    {
        //InitでMarkが呼ばれたりするので直接配置
        AllRolesInfo.Do(kvp => kvp.Value.IsEnable = kvp.Key.IsEnable());
        AllActiveRoles.Clear();
        MarkOthers.Clear();
        LowerOthers.Clear();
        SuffixOthers.Clear();
        OnEnterVentOthers.Clear();
        CheckMurderInfos.Clear();
        OnMurderPlayerOthers.Clear();
        OnFixedUpdateOthers.Clear();
        OnCompleteTaskOthers.Clear();
        SubRoleRPCSender.RoleHandlers.Clear();
    }
    public static void CreateInstance()
    {
        foreach (var pc in PlayerCatch.AllPlayerControls)
        {
            CreateInstance(pc.GetCustomRole(), pc);
        }
    }
    public static void CreateInstance(CustomRoles role, PlayerControl player, bool IsGame = false)
    {
        if (AllRolesInfo.TryGetValue(role, out var roleInfo))
        {
            var roleClass = roleInfo.CreateInstance(player);
            roleClass.Add();
            if (IsGame) roleClass.ChengeRoleAdd();
            if (roleClass is ISelfVoter selfVoter) selfVoter.AddSelfVoter(player);
            if (roleClass is IRoomTasker roomTasker) roomTasker.AddRoomTaker(player.PlayerId);
            //インポスター置き換えのクルー・ニュートラルには汎用の部屋タスクを割り当てる
            else RoomTaskSettings.AddTasker(player, role);
        }
        else
        {
            OtherRolesAdd(player);
        }
        var roleclass = player.GetRoleClass();
        if (player.Data.Role.Role == RoleTypes.Shapeshifter || role.GetRoleInfo()?.BaseRoleType?.Invoke() == RoleTypes.Shapeshifter)
        {
            Main.CheckShapeshift.TryAdd(player.PlayerId, false);
        }
        if (player.Data.Role.Role == RoleTypes.Phantom || role.GetRoleTypes() == RoleTypes.Phantom)
        {
            (roleclass as IUsePhantomButton)?.Init(player);
        }
    }

    public static void OtherRolesAdd(PlayerControl pc, CustomRoles role = CustomRoles.NotAssigned)
    {
        //属性と幽霊役職は削除済みなので、登録するものはない
    }
    /// <summary>
    /// 受信したRPCから送信先を読み取ってRoleClassに配信する
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="rpcType"></param>
    public static void DispatchRpc(MessageReader reader)
    {
        var playerId = reader.ReadByte();
        GetByPlayerId(playerId)?.ReceiveRPC(reader);
    }
    //NameSystem
    public static HashSet<Func<PlayerControl, PlayerControl, bool, string>> MarkOthers = new();
    public static HashSet<Func<PlayerControl, PlayerControl, bool, bool, string>> LowerOthers = new();
    public static HashSet<Func<PlayerControl, PlayerControl, bool, string>> SuffixOthers = new();
    //Vent
    public static HashSet<Func<PlayerPhysics, int, bool>> OnEnterVentOthers = new();
    public static HashSet<Action<PlayerControl, bool>> OnCompleteTaskOthers = new();
    /// <summary>
    /// seer,seenが役職であるかに関わらず発動するMark
    /// 登録されたすべてを結合する。
    /// </summary>
    /// <param name="seer">見る側</param>
    /// <param name="seen">見られる側</param>
    /// <param name="isForMeeting">会議中フラグ</param>
    /// <returns>結合したMark</returns>
    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        var sb = new StringBuilder(100);
        foreach (var marker in MarkOthers)
        {
            sb.Append(marker(seer, seen, isForMeeting));
        }
        return sb.ToString();
    }
    /// <summary>
    /// seer,seenが役職であるかに関わらず発動するLowerText
    /// 登録されたすべてを結合する。
    /// </summary>
    /// <param name="seer">見る側</param>
    /// <param name="seen">見られる側</param>
    /// <param name="isForMeeting">会議中フラグ</param>
    /// <param name="isForHud">ModでHudとして表示する場合</param>
    /// <returns>結合したLowerText</returns>
    public static string GetLowerTextOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        var sb = new StringBuilder(100);
        foreach (var lower in LowerOthers)
        {
            sb.Append(lower(seer, seen, isForMeeting, isForHud));
        }
        return sb.ToString();
    }
    /// <summary>
    /// seer,seenが役職であるかに関わらず発動するSuffix
    /// 登録されたすべてを結合する。
    /// </summary>
    /// <param name="seer">見る側</param>
    /// <param name="seen">見られる側</param>
    /// <param name="isForMeeting">会議中フラグ</param>
    /// <returns>結合したSuffix</returns>
    public static string GetSuffixOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        var sb = new StringBuilder(100);
        foreach (var suffix in SuffixOthers)
        {
            sb.Append(suffix(seer, seen, isForMeeting));
        }
        return sb.ToString();
    }
    /// <summary>
    /// ベントに入ることが確定した後に呼ばれる
    /// </summary>
    public static void OnEnterVent(PlayerPhysics physics, int ventId)
    {
        foreach (var vent in OnEnterVentOthers)
        {
            vent(physics, ventId);
        }
    }
    /// <summary>
    /// OnCompleateTask時に呼ばれる
    /// </summary>
    public static void OnTaskCompleteOthers(PlayerControl player, bool ret)
    {
        foreach (var cmptsk in OnCompleteTaskOthers)
            cmptsk(player, ret);
    }
    /// <summary>
    /// オブジェクトの破棄
    /// </summary>
    public static void Dispose()
    {
        Logger.Info($"Dispose ActiveRoles", "CustomRoleManager");
        MarkOthers.Clear();
        LowerOthers.Clear();
        SuffixOthers.Clear();
        OnEnterVentOthers.Clear();
        CheckMurderInfos.Clear();
        OnMurderPlayerOthers.Clear();
        OnFixedUpdateOthers.Clear();
        OnCompleteTaskOthers.Clear();
        SubRoleRPCSender.RoleHandlers.Clear();

        AllActiveRoles.Values.ToArray().Do(roleClass => roleClass.Dispose());
    }
}
public class MurderInfo
{
    /// <summary>実際にキルを行ったプレイヤー 不変</summary>
    public PlayerControl AttemptKiller { get; }
    /// <summary>Killerが実際にキルを行おうとしたプレイヤー 不変</summary>
    public PlayerControl AttemptTarget { get; }
    /// <summary>見た目上でキルを行うプレイヤー 可変</summary>
    public PlayerControl AppearanceKiller { get; set; }
    /// <summary>見た目上でキルされるプレイヤー 可変</summary>
    public PlayerControl AppearanceTarget { get; set; }

    /// <summary>
    /// targetをキル出来るか
    /// </summary>
    public bool CanKill = true;
    /// <summary>
    /// Killerが実際にキルするか
    /// </summary>
    public bool DoKill = true;
    /// <summary>
    /// ガーディングが発生しているか
    /// </summary>
    public bool IsGuard = false;

    public int KillPower = 1;
    public int GuardPower = 0;
    /// <summary>
    /// キル後、役職処理を行うか
    /// falseで通常処理
    /// trueでキラー,ターゲット共に行わない
    /// nullでターゲットのみ行わない
    /// </summary>
    public bool? DontRoleAbility = false;
    /// <summary>
    ///転落死など事故の場合(キラー不在)
    /// </summary>
    public bool IsAccident = false;
    public Vector2 killerpos;

    // 分解用 (killer, target) = info.AttemptTuple; のような記述でkillerとtargetをまとめて取り出せる
    public (PlayerControl killer, PlayerControl target) AttemptTuple => (AttemptKiller, AttemptTarget);
    public (PlayerControl killer, PlayerControl target) AppearanceTuple => (AppearanceKiller, AppearanceTarget);
    /// <summary>
    /// 本来の自殺
    /// </summary>
    public bool IsSuicide => AttemptKiller.PlayerId == AttemptTarget.PlayerId;
    /// <summary>
    /// 遠距離キル代わりの疑似自殺
    /// </summary>
    public bool IsFakeSuicide => AppearanceKiller.PlayerId == AppearanceTarget.PlayerId;
    /// <summary>
    /// キルができる状態か
    /// </summary>
    public bool IsCanKilling => !CheckHasGuard() && !IsSuicide && !IsFakeSuicide && DoKill && CanKill && !IsAccident;
    public CustomDeathReason DeathReason;
    public MurderInfo(PlayerControl attemptKiller, PlayerControl attemptTarget, PlayerControl appearanceKiller, PlayerControl appearancetarget, bool? DontRoleAbility = false, int Killpower = 1, int guardpower = 0, CustomDeathReason deathReason = CustomDeathReason.Kill)
    {
        AttemptKiller = attemptKiller;
        AttemptTarget = attemptTarget;
        AppearanceKiller = appearanceKiller;
        AppearanceTarget = appearancetarget;
        this.DontRoleAbility = DontRoleAbility;
        killerpos = appearanceKiller.transform.position;
        KillPower = Killpower;
        GuardPower = guardpower;
        DeathReason = deathReason;
    }
    public bool CheckHasGuard() => KillPower <= GuardPower;
}

/// <summary>
/// RoleBaseがない役職専用のRPC送信クラス
/// </summary>
public class SubRoleRPCSender : IDisposable
{
    public MessageWriter Writer;
    public SubRoleRPCSender(CustomRoles role, byte playerId)
    {
        Writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.CustomSubRoleSync, SendOption.None, -1);
        Writer.Write(playerId);
        Writer.Write((int)role);
    }
    public void Dispose()
    {
        if (!PlayerCatch.AnyModClient())
        {
            Writer.Recycle();
            return;
        }
        AmongUsClient.Instance.FinishRpcImmediately(Writer);
    }

    public static void DispatchRpc(MessageReader reader)
    {
        var playerId = reader.ReadByte();
        var roleId = (CustomRoles)reader.ReadInt32();
        if (!RoleHandlers.TryGetValue(roleId, out var actions)) return;
        foreach (var action in actions) action.Invoke(reader, playerId);
    }

    public static void AddHandler(CustomRoles role, Action<MessageReader, byte> action)
    {
        if (!RoleHandlers.TryGetValue(role, out var set))
            RoleHandlers[role] = set = new();
        set.Add(action);
    }
    public static Dictionary<CustomRoles, HashSet<Action<MessageReader, byte>>> RoleHandlers = new();
}

public enum CustomRoles
{//Default
    Crewmate = 0,
    //Impostor(Vanilla)
    Impostor,
    Shapeshifter,
    Phantom,
    Viper,
    //Impostor
    BountyHunter,
    FireWorks,
    Mafia,
    SerialKiller,
    ShapeMaster,
    Sniper,
    Vampire,
    Witch,
    Warlock,
    Mare,
    Penguin,
    Puppeteer,
    TimeThief,
    EvilTracker,
    Stealth,
    NekoKabocha,
    EvilHacker,
    Insider,
    //TOH-k
    Bomber,
    TeleportKiller,
    AntiReporter,
    Tairou,
    Evilgambler,
    Notifier,
    Magician,
    Decrescendo,
    Curser,
    Alien,
    AlienHijack,
    SpeedStar,
    EvilTeller,
    Limiter,
    ProgressKiller,
    Mole,
    EvilAddoer,
    Reloader,
    Jumper,
    EarnestWolf,
    Amnesiac,
    Camouflager,
    ConnectSaver,
    EvilSatellite,
    ProBowler,
    EvilMaker,
    Eraser,
    QuickKiller,
    CharismaStar,
    Ballooner,
    BorderKiller,
    ShapeKiller,
    Archer,
    Assassin,
    EvilBlender,
    UnFortuner,
    Bloodlust,
    EvilButtoner,
    //DEBUG only Impostor
    //Madmate
    MadGuardian,
    Madmate,
    MadSnitch,
    MadAvenger,
    SKMadmate,
    //TOH-k
    MadJester,
    MadTeller,
    MadBait,
    MadReduced,
    MadWorker,
    MadTracker,
    MadChanger,
    MadSuicide,
    MadBetrayer,
    Nue,
    MadHacker,
    MadWare,
    //DEBUG only Madmate
    //Crewmate(Vanilla)
    Engineer,
    Scientist,
    Tracker,
    Noisemaker,
    Detective,
    Judge,
    //Crewmate
    Bait,
    Lighter,
    Mayor,
    SabotageMaster,
    Sheriff,
    Snitch,
    SpeedBooster,
    Trapper,
    Dictator,
    Doctor,
    Seer,
    TimeManager,
    //TOH-K
    Gasp,
    VentMaster,
    ToiletFan,
    Bakery,
    FortuneTeller,
    TaskStar,
    PonkotuTeller,
    UltraStar,
    MeetingSheriff,
    GuardMaster,
    Shyboy,
    Balancer,
    ShrineMaiden,
    Comebacker,
    WhiteHacker,
    WolfBoy,
    NiceAddoer,
    InSender,
    Staff,
    Efficient,
    Psychic,
    SwitchSheriff,
    NiceLogger,
    Android,
    King,
    AmateurTeller,
    Cakeshop,
    Snowman,
    Stolener,
    VentOpener,
    VentHunter,
    Walker,
    CandleLighter,
    Express,
    Inspector,
    Fortuner,
    AllArounder,
    Observer,
    Satellite,
    Merlin,
    //DEBUG only Crewmate
    Analyzer,
    //Neutral
    Arsonist,
    Egoist,
    Jester,
    Opportunist,
    PlagueDoctor,
    SchrodingerCat,
    Terrorist,
    Executioner,
    Jackal,
    //TOHk
    Remotekiller,
    Chef,
    JackalMafia,
    CountKiller,
    GrimReaper,
    Madonna,
    Jackaldoll,
    Workaholic,
    Monochromer,
    DoppelGanger,
    MassMedia,
    Chameleon,
    Banker,
    BakeCat,
    Emptiness,
    JackalAlien,
    CurseMaker,
    PhantomThief,
    Fox,
    Turncoat,
    Vulture,
    SantaClaus,
    Missioneer,
    Strawdoll,
    Fool,
    JackalWolf,
    //ジャッカル〇〇(インポスター役職の能力を持つジャッカル)
    JackalAntiReporter,
    JackalArcher,
    JackalBomber,
    JackalBorderKiller,
    JackalBountyHunter,
    JackalCharismaStar,
    JackalDecrescendo,
    JackalEarnestWolf,
    JackalEraser,
    JackalEvilButtoner,
    JackalEvilTeller,
    JackalEvilgambler,
    JackalInsider,
    JackalMagician,
    JackalMare,
    JackalNekoKabocha,
    JackalNotifier,
    JackalPenguin,
    JackalProgressKiller,
    JackalPuppeteer,
    JackalQuickKiller,
    JackalReloader,
    JackalSerialKiller,
    JackalSniper,
    JackalSpeedStar,
    JackalStealth,
    JackalTairou,
    JackalTimeThief,
    JackalVampire,
    JackalWarlock,
    JackalWitch,
    TaskPlayerB,
    //DEBUG only Neutral.
    //HideAndSeek
    HASFox,
    HASTroll,
    MMArcher,
    //GM
    GM,
    //Combination
    Driver,
    Braid,
    Vega,
    Altair,
    // Sub-roll after 500
    NotAssigned = 500,
    LastImpostor,
    LastNeutral,
    Workhorse,
    Twins,
    OneWolf,
    //第三属性
    Lovers, RedLovers, YellowLovers, BlueLovers, GreenLovers, WhiteLovers, PurpleLovers,
    MadonnaLovers, OneLove, Amanojaku, Faction,
    //AddMadmate,
    //バフ
    Guesser,
    Serial,
    Connecting,
    Watching,
    PlusVote,
    Tiebreaker,
    Autopsy,
    Revenger,
    Speeding,
    Management,
    Opener,
    Powerful,
    Stack,
    AntiTeleporter,
    Seeing,
    Lighting,
    Moon,
    Guarding,
    MagicHand,
    Absorb,
    //デバフ
    Amnesia,
    Notvoter,
    Elector,
    NonReport,
    Transparent,
    Water,
    Clumsy,
    Slacker,
    SlowStarter,
    InfoPoor,
    News,
    Notice,
    Sunglasses,

    //GhostRoles

    //MadmateGhost

    DemonicCrusher,
    DemonicTracker,
    DemonicVenter,
    DemonicSupporter,
    //CrewMateGhost
    Ghostbuttoner,
    GhostNoiseSender,
    GhostReseter,
    GuardianAngel,
    SpiritGuide,
    GhostRumour,
    //NeutralGhost
    AsistingAngel,
}
public enum CustomRoleTypes
{
    Crewmate,
    Impostor,
    Neutral,
    Madmate
}
public enum HasTask
{
    True,
    False,
    ForRecompute
}

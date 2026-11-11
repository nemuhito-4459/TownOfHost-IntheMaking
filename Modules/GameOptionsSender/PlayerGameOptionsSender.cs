using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Linq;
using InnerNet;
using Mathf = UnityEngine.Mathf;

using TownOfHost.Roles.Core;
using static TownOfHost.Options;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Vanilla;

namespace TownOfHost.Modules
{
    public class PlayerGameOptionsSender : GameOptionsSender
    {
        public static void SetDirty(PlayerControl player) => SetDirty(player.PlayerId);
        public static void SetDirty(byte playerId) =>
            AllSenders.OfType<PlayerGameOptionsSender>()
            .Where(sender => sender.player.PlayerId == playerId)
            .ToList().ForEach(sender => sender.SetDirty());
        public static void SetDirtyToAll() =>
            AllSenders.OfType<PlayerGameOptionsSender>()
            .ToList().ForEach(sender => sender.SetDirty());

        public IGameOptions cachedGameOptions = null;
        public override IGameOptions BasedGameOptions =>
            Main.RealOptionsData.Restore(cachedGameOptions ?? (cachedGameOptions = new NormalGameOptionsV12(new UnityLogger().Cast<ILogger>()).Cast<IGameOptions>()));
        public override bool IsDirty { get; protected set; }

        public PlayerControl player;
        public string OldOptionstext;

        public PlayerGameOptionsSender(PlayerControl player)
        {
            this.player = player;
            this.OldOptionstext = "";
        }
        public void SetDirty() => IsDirty = true;

        public override void SendGameOptions()
        {
            //ローカルのテストで乗り移っている間は、元の自分の設定は使わない(乗り移った先の設定を自分の画面に使う)
            if (LocalTestManager.IsHostBodyWhilePossessed(player)) return;
            var opt = BuildGameOptions();
            if (player.AmOwner || player == LocalTestManager.Possessed)
            {
                foreach (var com in GameManager.Instance.LogicComponents)
                {
                    if (com.TryCast<LogicOptions>(out var lo))
                        lo.SetGameOptions(opt);
                }
                GameOptionsManager.Instance.CurrentGameOptions = opt;
            }
            else
            {
                var IsAlive = player.IsAlive();
                var killCooldown = IsAlive ? opt.GetFloat(FloatOptionNames.KillCooldown) : 0f;
                var killDistance = IsAlive ? opt.GetInt(Int32OptionNames.KillDistance) : 0;
                var impostorLight = IsAlive ? opt.GetFloat(FloatOptionNames.ImpostorLightMod) : 0f;
                var crewLight = IsAlive ? opt.GetFloat(FloatOptionNames.CrewLightMod) : 0f;
                var playerSpeed = opt.GetFloat(FloatOptionNames.PlayerSpeedMod);
                var numEmergency = IsAlive ? opt.GetInt(Int32OptionNames.NumEmergencyMeetings) : 0;
                var emergencyCooldown = IsAlive ? opt.GetInt(Int32OptionNames.EmergencyCooldown) : 0;
                var discussionTime = opt.GetInt(Int32OptionNames.DiscussionTime);
                var votingTime = opt.GetInt(Int32OptionNames.VotingTime);
                var anonymousVotes = opt.GetBool(BoolOptionNames.AnonymousVotes);
                var numCommonTasks = opt.GetInt(Int32OptionNames.NumCommonTasks);
                var numLongTasks = opt.GetInt(Int32OptionNames.NumLongTasks);
                var numShortTasks = opt.GetInt(Int32OptionNames.NumShortTasks);
                var visualTasks = IsAlive ? opt.GetBool(BoolOptionNames.VisualTasks) : false;
                var taskBarMode = IsAlive ? opt.GetInt(Int32OptionNames.TaskBarMode) : 0;
                var confirmImpostor = opt.GetBool(BoolOptionNames.ConfirmImpostor);
                var engcooldown = IsAlive ? opt.GetFloat(FloatOptionNames.EngineerCooldown) : 0;
                var engmaxtime = IsAlive ? opt.GetFloat(FloatOptionNames.EngineerInVentMaxTime) : 0;
                var scicooldown = IsAlive ? opt.GetFloat(FloatOptionNames.ScientistCooldown) : 0;
                var scibattery = IsAlive ? opt.GetFloat(FloatOptionNames.ScientistBatteryCharge) : 0;
                var trackercool = IsAlive ? opt.GetFloat(FloatOptionNames.TrackerCooldown) : 0;
                var trackerdelay = IsAlive ? opt.GetFloat(FloatOptionNames.TrackerDelay) : 0;
                var tarckduration = IsAlive ? opt.GetFloat(FloatOptionNames.TrackerDuration) : 0;
                var noisealert = opt.GetFloat(FloatOptionNames.NoisemakerAlertDuration);
                var noiseimp = opt.GetBool(BoolOptionNames.NoisemakerImpostorAlert);
                var shapecool = IsAlive ? opt.GetFloat(FloatOptionNames.ShapeshifterCooldown) : 0;
                var ShapeshifterDuration = opt.GetFloat(FloatOptionNames.ShapeshifterDuration);
                var shapeskin = IsAlive ? opt.GetBool(BoolOptionNames.ShapeshifterLeaveSkin) : false;
                var phantom = IsAlive ? opt.GetFloat(FloatOptionNames.PhantomCooldown) : 0;
                var detective = IsAlive ? opt.GetFloat(FloatOptionNames.DetectiveSuspectLimit) : 0;
                var guardancool = opt.GetFloat(FloatOptionNames.GuardianAngelCooldown);
                var vip = opt.GetFloat(FloatOptionNames.ViperDissolveTime);
                var jud = opt.GetFloat(FloatOptionNames.JudgeTaskRequirementPercentage);
                var spgcd = opt.GetFloat(FloatOptionNames.SpiritGuideCooldownSeconds);

                string NowOption = $"{killCooldown},{killDistance},{impostorLight},{crewLight},{playerSpeed},{numEmergency},{emergencyCooldown},{discussionTime},{votingTime},{anonymousVotes},{numCommonTasks},{numLongTasks},{numShortTasks},{visualTasks},{taskBarMode},{confirmImpostor}";
                NowOption += $"{engcooldown},{engmaxtime},{scicooldown},{scibattery},{trackercool},{trackerdelay},{tarckduration},{noisealert},{noiseimp},{shapecool},{ShapeshifterDuration},{shapeskin},{phantom},{detective},{vip},{guardancool},{jud},{spgcd}";
                if (OldOptionstext == NowOption)//再度送信するならキャンセル
                {
                    return;
                }
                OldOptionstext = GameStates.CalledMeeting ? "" : NowOption;
                base.SendGameOptions();
            }
        }

        public override void SendOptionsArray(Il2CppStructArray<byte> optionArray)
        {
            for (byte i = 0; i < GameManager.Instance.LogicComponents.Count; i++)
            {
                if (GameManager.Instance.LogicComponents[i].TryCast<LogicOptions>(out _))
                {
                    SendOptionsArray(optionArray, i, player.GetClientId());
                }
            }
        }
        public static void RemoveSender(PlayerControl player)
        {
            var sender = AllSenders.OfType<PlayerGameOptionsSender>()
            .FirstOrDefault(sender => sender.player.PlayerId == player.PlayerId);
            if (sender == null) return;
            sender.player = null;
            AllSenders.Remove(sender);
        }
        public override IGameOptions BuildGameOptions()
        {
            if (Main.RealOptionsData == null)
            {
                if (GameOptionsManager.Instance.CurrentGameOptions == null)
                {
                    Logger.Error($"CurrentGameOptionsがnullだ", "Pl.BuildGameOptions");
                    return BasedGameOptions;
                }
                Main.RealOptionsData = new OptionBackupData(GameOptionsManager.Instance.CurrentGameOptions);
            }

            var opt = BasedGameOptions;
            if (BasedGameOptions == null)
            {
                Logger.Error($"BasedGameOptionsがnullだ", "Pl.BuildGameOptions");
                return opt;
            }

            AURoleOptions.SetOpt(opt);

            AURoleOptions.ShapeshifterLeaveSkin = false;
            AURoleOptions.NoisemakerImpostorAlert = true;
            AURoleOptions.NoisemakerAlertDuration = Noisemaker.NoisemakerAlertDuration.GetFloat();
            AURoleOptions.ViperDissolveTime = Viper.ViperDissolveTime;

            if (player == null)
            {
                Logger.Error($"playerがnullだ", "Pl.BuildGameOptions");
                return opt;
            }
            var state = PlayerState.GetByPlayerId(player.PlayerId);
            if (state == null)
            {
                Logger.Error($"stateがnullやで", "Pl.BuildGameOptions");
                return opt;
            }
            //ジャッジ方式の会議能力: その人だけジャッジのタスク条件を0%にする(すぐ判決を使える)
            if (JudgeAbilityManager.IsJudge(player.PlayerId)) AURoleOptions.JudgeTaskRequirementPercentage = 0f;
            if (CurrentGameMode == CustomGameMode.TaskBattle)
            {
                opt.SetFloat(FloatOptionNames.CrewLightMod, 5f);
                opt.SetFloat(FloatOptionNames.EngineerCooldown, TaskBattle.TaskBattleVentCooldown.GetFloat());
                AURoleOptions.EngineerInVentMaxTime = 0;
                return opt;
            }

            CustomRoles role = player.GetCustomRole();

            if (player.IsAlive())
            {
                opt.SetFloat(FloatOptionNames.EngineerCooldown, DefaultEngineerCooldown.GetFloat());
                opt.SetFloat(FloatOptionNames.EngineerInVentMaxTime, DefaultEngineerInVentMaxTime.GetFloat());
                opt.SetInt(Int32OptionNames.NumEmergencyMeetings, (int)state.NumberOfRemainingButtons);

                //属性は削除済み。ライティング/ムーンはマッドメイトの設定からだけ付く
                var HasLithing = false;
                var HasMoon = false;
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, Main.DefaultImpostorVision);
                opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultCrewmateVision);

                switch (role.GetCustomRoleTypes())
                {
                    case CustomRoleTypes.Impostor:
                        AURoleOptions.ShapeshifterCooldown = DefaultShapeshiftCooldown.GetFloat();
                        AURoleOptions.ShapeshifterDuration = DefaultShapeshiftDuration.GetFloat();
                        break;
                    case CustomRoleTypes.Madmate:
                        AURoleOptions.EngineerCooldown = MadmateVentCooldown.GetFloat();
                        AURoleOptions.EngineerInVentMaxTime = MadmateVentMaxTime.GetFloat();
                        HasLithing |= MadmateHasLighting.GetBool();
                        HasMoon |= MadmateHasMoon.GetBool();
                        if (MadmateCanSeeOtherVotes.GetBool())
                            opt.SetBool(BoolOptionNames.AnonymousVotes, false);
                        break;
                }
                if (role is CustomRoles.Egoist)
                {
                    AURoleOptions.ShapeshifterCooldown = DefaultShapeshiftCooldown.GetFloat();
                    AURoleOptions.ShapeshifterDuration = DefaultShapeshiftDuration.GetFloat();
                }
                var roleClass = player.GetRoleClass();
                if (roleClass == null)
                {
                    Logger.Error($"roleClassがnullだ", "Pl.BuildGameOptions");
                    //    return opt;
                }

                //キルレンジ
                if (OverrideKilldistance.AllData.TryGetValue(role, out var killdistance))
                    opt.SetInt(Int32OptionNames.KillDistance, killdistance.Killdistance.GetInt());

                    roleClass?.ApplyGameOptions(opt);

                var isElectrical = Utils.IsActive(SystemTypes.Electrical);

                //Moon
                if (HasMoon)
                    if (isElectrical)
                    {
                        opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultCrewmateVision * AURoleOptions.ElectricalCrewVision);
                        opt.SetFloat(FloatOptionNames.ImpostorLightMod, Main.DefaultImpostorVision);
                    }

                //ホストだとまだﾋﾟｯｶｰﾝしちゃうのどうにかしたい。
                //Lighting
                if (HasLithing)
                {
                    if (isElectrical && HasMoon)
                    {
                        opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultImpostorVision * (role.GetRoleTypes().IsCrewmate() ? AURoleOptions.ElectricalCrewVision : 5f));
                        opt.SetFloat(FloatOptionNames.ImpostorLightMod, Main.DefaultImpostorVision);
                    }
                    else//停電時はクルー視界
                        if (isElectrical)
                        {
                            opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultCrewmateVision);
                            opt.SetFloat(FloatOptionNames.ImpostorLightMod, Main.DefaultCrewmateVision);
                        }
                        else
                        {
                            opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultImpostorVision);
                            opt.SetFloat(FloatOptionNames.ImpostorLightMod, Main.DefaultImpostorVision);
                        }
                }

                //キルクール0に設定+修正する設定をONにした時だけ呼び出す。
                if (Main.AllPlayerKillCooldown.TryGetValue(player.PlayerId, out var killCooldown))
                {
                    //キルボタンが使用可能の時は最小0.000...1　設定無効 or キルボタンが使用不可なら最小0
                    AURoleOptions.KillCooldown = Mathf.Max(((roleClass as IKiller)?.CanUseKillButton() == true) ? 0.00000000000000000000000000000000000000000001f : 0f, killCooldown);
                }

                state.taskState.hasTasks = UtilsTask.HasTasks(player.Data, false);

                if (AdditionalEmergencyCooldown.GetBool() && AdditionalEmergencyCooldownThreshold.GetInt() <= PlayerCatch.AllAlivePlayersCount)
                {
                    opt.SetInt(Int32OptionNames.EmergencyCooldown, AdditionalEmergencyCooldownTime.GetInt());
                }
                if (SyncButtonMode.GetBool() && SyncedButtonCount.GetValue() <= UsedButtonCount)
                {
                    opt.SetInt(Int32OptionNames.EmergencyCooldown, 3600);
                    opt.SetInt(Int32OptionNames.NumEmergencyMeetings, 0);
                }

                AURoleOptions.EngineerCooldown = Mathf.Max(1.2f, AURoleOptions.EngineerCooldown);
                opt.BlackOut(state.IsBlackOut);
            }
            else
            {
                bool HaveWatching = false;

                if (GhostCanSeeOtherVotes.GetBool() || !GhostOptions.GetBool())
                    HaveWatching |= true;
                if (HaveWatching is false)
                {
                    if (role.IsMadmate() && MadmateCanSeeOtherVotes.GetBool()) HaveWatching = true;
                }

                if (HaveWatching) opt.SetBool(BoolOptionNames.AnonymousVotes, false);

            }

            if (Main.AllPlayerSpeed.TryGetValue(player.PlayerId, out var speed))
            {
                if (state.CanMove is false) speed = Main.MinSpeed;
                AURoleOptions.PlayerSpeedMod = Mathf.Clamp(speed, Main.MinSpeed, Utils.IsRestriction() ? 3f : 10f);
            }

            //あっ、鬼さんは開始前見ちゃだめですよ?
            if ((CurrentGameMode == CustomGameMode.HideAndSeek || IsStandardHAS) && HideAndSeekKillDelayTimer > 0)
            {
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0f);
                if (player.Is(CountTypes.Impostor))
                {
                    AURoleOptions.PlayerSpeedMod = Main.MinSpeed;
                }
            }

            MeetingTimeManager.ApplyGameOptions(opt);

            AURoleOptions.ShapeshifterCooldown = Mathf.Max(1f, AURoleOptions.ShapeshifterCooldown);
            AURoleOptions.PhantomCooldown = Mathf.Max(1f, AURoleOptions.PhantomCooldown);
            AURoleOptions.ProtectionDurationSeconds = 0f;
            AURoleOptions.ImpostorsCanSeeProtect = false;

            return opt;

            float CoolDown(float cool) => Mathf.Max(1f, cool);
        }

        public override bool AmValid()
        {
            try
            {
                //キルクとか反映されないから～
                return base.AmValid() && player is not null && !SelectRolesPatch.Disconnected.Contains(player.PlayerId) && Main.RealOptionsData != null;
            }
            catch
            {
                Logger.Error($"{player?.Data?.GetLogPlayerName() ?? "???"} - Error", "PlayerGameOptionsSender.AmValid");
                return false;
            }
        }
    }
}
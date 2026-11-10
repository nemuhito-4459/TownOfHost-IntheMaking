using System.Collections.Generic;
using System.Linq;
using TownOfHost.Roles.Core;

namespace TownOfHost
{
    /// <summary>
    /// インポスター役職ごとのキルクール設定。<br/>
    /// 範囲は 0～180秒(0.5刻み)、0 は「規定値」(インポスタータブの共通キルクール)。<br/>
    /// 役職が自前で「KillCooldown」の設定を持っていればそれを規定値対応に変え、無ければ自動で追加する。
    /// </summary>
    public static class ImpostorKillCooldown
    {
        /// <summary>自動で追加したキルクール設定(自前の設定を持たない役職のみ)</summary>
        static readonly Dictionary<CustomRoles, FloatOptionItem> AddedOptions = new();
        /// <summary>自動追加分のID(+ConfigId)。他と重ならない帯</summary>
        const int IdBase = 3_000_000;

        /// <summary>役職の設定を作り終えた直後に呼ぶ(インポスター役職のみ)</summary>
        public static void Setup(SimpleRoleInfo info)
        {
            //役職確率の設定を作らない役職(ファントムなど)は対象外
            if (!Options.CustomRoleSpawnChances.ContainsKey(info.RoleName)) return;
            var own = OptionItem.AllOptions
                .Where(o => o.ParentRole == info.RoleName && o.Name == "KillCooldown")
                .OfType<FloatOptionItem>()
                .ToArray();
            if (own.Length > 0)
            {
                foreach (var option in own) option.UseDefaultKillCooldownAtZero();
                return;
            }

            var added = FloatOptionItem.Create(IdBase + info.ConfigId, "KillCooldown", new(0f, 180f, 0.5f), 0f, info.Tab, false);
            added.SetParent(info.RoleOption).SetParentRole(info.RoleName).SetValueFormat(OptionFormat.Seconds);
            added.UseDefaultKillCooldownAtZero();
            AddedOptions[info.RoleName] = added;
        }

        /// <summary>
        /// 自動追加したキルクール設定の値(0なら規定値)。自前の設定を持つ役職・インポスター以外は規定値を返す
        /// </summary>
        public static float Get(CustomRoles role)
            => AddedOptions.TryGetValue(role, out var option) ? option.GetFloat() : DefaultValue;

        /// <summary>規定値(インポスタータブの共通キルクール)</summary>
        public static float DefaultValue => FactionCommonOptions.KillCooldown?.GetFloat() ?? Options.DefaultKillCooldown;
    }
}

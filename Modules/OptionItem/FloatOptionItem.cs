using System;
using TownOfHost.Roles.Core;

namespace TownOfHost
{
    public class FloatOptionItem : OptionItem
    {
        // 必須情報
        public FloatValueRule Rule;

        // コンストラクタ
        public FloatOptionItem(int id, string name, float defaultValue, TabGroup tab, bool isSingleValue, FloatValueRule rule)
        : base(id, name, rule.GetNearestIndex(defaultValue), tab, isSingleValue)
        {
            Rule = rule;
        }
        public static FloatOptionItem Create(
            int id, string name, FloatValueRule rule, float defaultValue, TabGroup tab, bool isSingleValue
        )
        {
            return new FloatOptionItem(
                id, name, defaultValue, tab, isSingleValue, rule
            );
        }
        public static FloatOptionItem Create(
            int id, Enum name, FloatValueRule rule, float defaultValue, TabGroup tab, bool isSingleValue
        )
        {
            return new FloatOptionItem(
                id, name.ToString(), defaultValue, tab, isSingleValue, rule
            );
        }
        public static FloatOptionItem Create(
            SimpleRoleInfo roleInfo, int idOffset, Enum name, FloatValueRule rule, float defaultValue, bool isSingleValue, OptionItem parent = null
        )
        {
            var opt = new FloatOptionItem(
                roleInfo.ConfigId + idOffset, name.ToString(), defaultValue, roleInfo.Tab, isSingleValue, rule
            );
            opt.SetParent(parent ?? roleInfo.RoleOption);
            opt.SetParentRole(roleInfo.RoleName);
            return opt;
        }
        public static FloatOptionItem Create(
            SimpleRoleInfo roleInfo, int idOffset, string name, FloatValueRule rule, float defaultValue, bool isSingleValue, OptionItem parent = null
        )
        {
            var opt = new FloatOptionItem(
                roleInfo.ConfigId + idOffset, name.ToString(), defaultValue, roleInfo.Tab, isSingleValue, rule
            );
            opt.SetParent(parent ?? roleInfo.RoleOption);
            opt.SetParentRole(roleInfo.RoleName);
            return opt;
        }

        // Getter
        public override int GetInt() => (int)Rule.GetValueByIndex(CurrentValue);
        public override float GetFloat()
        {
            var value = Rule.GetValueByIndex(CurrentValue);
            //0が「規定値」の設定(インポスターのキルクール)は、インポスタータブの共通キルクールを返す
            if (value == 0f && ZeroNotation is OptionZeroNotation.Default) return ImpostorKillCooldown.DefaultValue;
            return value;
        }
        public override string GetString()
        {
            return ApplyFormat(Rule.GetValueByIndex(CurrentValue).ToString());
        }
        public override string GetValueString(bool coloroff)
        {
            return ApplyFormat(Rule.GetValueByIndex(CurrentValue).ToString(), coloroff);
        }
        public override int GetValue()
            => Rule.RepeatIndex(base.GetValue());

        // Setter
        public override void SetValue(int value, bool doSync = true)
        {
            base.SetValue(Rule.RepeatIndex(value), doSync);
        }

        /// <summary>
        /// 役職の秒数設定を「0.5秒刻み・0.5〜180秒」にそろえる。
        /// 0にオフ/無制限の意味がある設定は0を残して「∞」表記にする。
        /// メイン設定は対象外。最大値が180秒を超えるものは、役職の設定だけ対象にする
        /// (役職に属さない共通設定と、リミッターは元の範囲のまま)。
        /// </summary>
        public void NormalizeRoleSeconds()
        {
            if (ValueFormat != OptionFormat.Seconds) return;
            if (ZeroNotation is OptionZeroNotation.Default) return;//キルクール(規定値)は UseDefaultKillCooldownAtZero で設定済み
            if (Tab is TabGroup.MainSettings) return;
            if (Rule.MaxValue > 180f && ParentRole is CustomRoles.NotAssigned or CustomRoles.Limiter) return;

            var keepZero = ZeroNotation is not OptionZeroNotation.None && Rule.MinValue <= 0f;
            var defaultSeconds = Rule.GetValueByIndex(DefaultValue);
            var newRule = new FloatValueRule(keepZero ? 0f : 0.5f, 180f, 0.5f);
            if (!keepZero && defaultSeconds < 0.5f) defaultSeconds = 0.5f;

            Rule = newRule;
            if (keepZero) SetZeroNotation(OptionZeroNotation.Infinity);
            ResetDefaultValue(newRule.GetNearestIndex(Math.Min(defaultSeconds, 180f)));
        }

        /// <summary>
        /// インポスターのキルクール用: 0～180秒(0.5刻み)、0は「規定値」(共通キルクール)、初期値は規定値
        /// </summary>
        public void UseDefaultKillCooldownAtZero()
        {
            Rule = new FloatValueRule(0f, 180f, 0.5f);
            SetValueFormat(OptionFormat.Seconds);
            SetZeroNotation(OptionZeroNotation.Default);
            ResetDefaultValue(0);
        }
    }
}
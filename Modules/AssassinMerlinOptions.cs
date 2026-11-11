using System.Linq;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Crewmate;
using TownOfHost.Roles.Impostor;

namespace TownOfHost
{
    /// <summary>
    /// コンビネーションタブの「アサシンとマーリン」<br/>
    /// ・元の確率の行は ON/OFF の切り替えだけにする(0% / 100%)<br/>
    /// ・ONのときだけ「アサシン」「マーリン」の行を出し、それぞれの設定ページ・説明(？)を開けるようにする<br/>
    /// ・マーリンの設定(アサシンの設定の中で作っている)はマーリンのページへ移す。IDは変えないので保存値はそのまま
    /// </summary>
    public static class AssassinMerlinOptions
    {
        /// <summary>行だけのダミー設定のID(+ConfigId)</summary>
        const int IdBase = 3_100_000;
        public static OptionItem AssassinRow, MerlinRow;
        /// <summary>アサシン・マーリンの行か(役職の設定ページに移さないようにする)</summary>
        public static bool IsRow(OptionItem option) => option != null && (option == AssassinRow || option == MerlinRow);

        public static void Setup()
        {
            if (!Options.CustomRoleSpawnChances.TryGetValue(CustomRoles.Assassin, out var toggle)) return;

            //確率の行 → ON/OFF の行(役職の行ではなく普通の設定の行にする)
            toggle.Rule = new(0, 100, 100);
            toggle.ShowOnOff = true;
            toggle.SetCustomRole(CustomRoles.NotAssigned);

            AssassinRow = CreateRow(Assassin.RoleInfo, toggle);
            MerlinRow = CreateRow(Merlin.RoleInfo, toggle);

            //アサシンの設定はアサシンの行の下へ、マーリンの設定はマーリンの行の下(マーリンのページ)へ
            foreach (var option in OptionItem.AllOptions.ToArray())
            {
                if (option == toggle || option == AssassinRow || option == MerlinRow) continue;
                if (option.ParentRole != CustomRoles.Assassin) continue;
                var isMerlin = Assassin.MerlinOptions.Contains(option);
                if (isMerlin) option.SetParentRole(CustomRoles.Merlin);
                if (option.Parent == toggle) option.ReParent(isMerlin ? MerlinRow : AssassinRow);
            }

            //行は設定より後に作っているので、設定ページでは一番下に出てしまう。
            //普通の役職と同じように、それぞれの設定の一番上(直前)に並べ替える(ページの中の行のボタンで元の画面に戻れる)
            var firstAssassin = OptionItem.AllOptions.FirstOrDefault(o => o.ParentRole == CustomRoles.Assassin && !IsRow(o));
            var firstMerlin = OptionItem.AllOptions.FirstOrDefault(o => o.ParentRole == CustomRoles.Merlin && !IsRow(o));
            OptionItem.MoveBefore(AssassinRow, firstAssassin);
            OptionItem.MoveBefore(MerlinRow, firstMerlin);
        }

        /// <summary>設定ボタンと「？」ボタンだけの役職の行(値は表示しない)。ONのときだけ出る</summary>
        static OptionItem CreateRow(SimpleRoleInfo info, OptionItem toggle)
        {
            var row = IntegerOptionItem.Create(IdBase + info.ConfigId, info.RoleName.ToString(), new(0, 0, 1), 0, TabGroup.Combinations, false, HideValue: true)
                .SetCustomRole(info.RoleName)
                .SetColorcode(info.RoleColorCode)
                .SetColor(info.RoleColor)
                .SetEnabled(() => toggle.GetBool());
            row.HideValueText = true;//値(0)は出さない
            return row;
        }
    }
}

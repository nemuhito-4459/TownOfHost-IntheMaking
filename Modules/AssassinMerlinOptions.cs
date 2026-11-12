using System.Collections.Generic;
using System.Linq;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Crewmate;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;

namespace TownOfHost
{
    /// <summary>
    /// コンビネーションタブの「アサシンとマーリン」「リクルーターとニート」<br/>
    /// ・元の確率の行は ON/OFF の切り替えだけにする(0% / 100%)<br/>
    /// ・ONのときだけ「アサシン」「マーリン」の行を出し、それぞれの設定ページ・説明(？)を開けるようにする<br/>
    /// ・マーリンの設定(アサシンの設定の中で作っている)はマーリンのページへ移す。IDは変えないので保存値はそのまま
    /// </summary>
    public static class AssassinMerlinOptions
    {
        /// <summary>行だけのダミー設定のID(+ConfigId)</summary>
        const int IdBase = 3_100_000;
        public static OptionItem AssassinRow, MerlinRow;
        /// <summary>コンビネーション役職の行(役職の設定ページに移さないようにする)</summary>
        static readonly HashSet<OptionItem> Rows = new();
        /// <summary>コンビネーション役職の行か</summary>
        public static bool IsRow(OptionItem option) => option != null && Rows.Contains(option);

        public static void Setup()
        {
            //アサシンとマーリン
            var rows = SetupPair(CustomRoles.Assassin, Assassin.RoleInfo, Merlin.RoleInfo, Assassin.MerlinOptions);
            if (rows != null) (AssassinRow, MerlinRow) = rows.Value;
            //リクルーターとニート
            SetupPair(CustomRoles.Recruiter, Recruiter.RoleInfo, Neet.RoleInfo, Recruiter.NeetOptions);
        }

        /// <summary>
        /// コンビの「確率の行」を ON/OFF にして、それぞれの役職の行を作り、相方の設定を相方の行の下へ移す
        /// </summary>
        static (OptionItem main, OptionItem sub)? SetupPair(CustomRoles mainRole, SimpleRoleInfo mainInfo, SimpleRoleInfo subInfo, List<OptionItem> subOptions)
        {
            if (!Options.CustomRoleSpawnChances.TryGetValue(mainRole, out var toggle)) return null;

            //確率の行 → ON/OFF の行(役職の行ではなく普通の設定の行にする)
            toggle.Rule = new(0, 100, 100);
            toggle.ShowOnOff = true;
            toggle.SetCustomRole(CustomRoles.NotAssigned);

            var mainRow = CreateRow(mainInfo, toggle);
            var subRow = CreateRow(subInfo, toggle);
            Rows.Add(mainRow);
            Rows.Add(subRow);

            //本体の設定は本体の行の下へ、相方の設定は相方の行の下(相方のページ)へ
            foreach (var option in OptionItem.AllOptions.ToArray())
            {
                if (option == toggle || IsRow(option)) continue;
                if (option.ParentRole != mainRole) continue;
                var isSub = subOptions.Contains(option);
                if (isSub) option.SetParentRole(subInfo.RoleName);
                if (option.Parent == toggle) option.ReParent(isSub ? subRow : mainRow);
            }

            //行は設定より後に作っているので、設定ページでは一番下に出てしまう。
            //普通の役職と同じように、それぞれの設定の一番上(直前)に並べ替える(ページの中の行のボタンで元の画面に戻れる)
            var firstMain = OptionItem.AllOptions.FirstOrDefault(o => o.ParentRole == mainRole && !IsRow(o));
            var firstSub = OptionItem.AllOptions.FirstOrDefault(o => o.ParentRole == subInfo.RoleName && !IsRow(o));
            OptionItem.MoveBefore(mainRow, firstMain);
            OptionItem.MoveBefore(subRow, firstSub);
            return (mainRow, subRow);
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

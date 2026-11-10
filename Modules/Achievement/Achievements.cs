using TownOfHost.Roles.Core;

namespace TownOfHost;

// 実績(アチーブメント)機能は削除済み。
// 役職ファイルの中に実績の登録・達成の呼び出しが約300か所残っているので、
// それらを受け止めて何もしない「空の受け口」だけをここに残している。
// 役職ファイルから呼び出しを消し終わったら、このファイルごと消してよい。
class Achievements
{
    /// <summary>何もしない(実績機能は削除済み)</summary>
    public static void RpcCompleteAchievement(byte playerid, int flug, Achievement achievement, int addstate = 1) { }
}

public class Achievement
{
    /// <summary>何もしない(実績機能は削除済み)</summary>
    public Achievement(SimpleRoleInfo roleinfo, int id, int step, int states, int difficulty, bool IsHidden = false) { }
    /// <summary>何もしない(実績機能は削除済み)</summary>
    public Achievement(CustomRoles role, int id, int step, int states, int difficulty, bool IsHidden = false) { }
}

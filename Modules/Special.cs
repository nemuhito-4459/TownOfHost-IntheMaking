using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Attributes;
using TownOfHost.Modules;
using TownOfHost.Roles;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost;

static class Event
{
    public static bool IsChristmas = DateTime.Now.Month == 12 && DateTime.Now.Day is 24 or 25;
    public static bool White = DateTime.Now.Month == 3 && DateTime.Now.Day is 14;
    public static bool IsInitialRelease = DateTime.Now.Month == 10 && DateTime.Now.Day is 31;
    public static bool IsHalloween = (DateTime.Now.Month == 10 && DateTime.Now.Day is 31) || (DateTime.Now.Month == 11 && DateTime.Now.Day is 1 or 2 or 3 or 4 or 5 or 6 or 7);
    public static bool GoldenWeek = DateTime.Now.Month == 5 && DateTime.Now.Day is 3 or 4 or 5;
    public static bool April = DateTime.Now.Month == 4 && DateTime.Now.Day is 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8;
    public static bool Tanabata = DateTime.Now.Month == 7 && DateTime.Now.Day is > 6 and < 15;
    public static bool IsEventDay => IsChristmas || White || IsInitialRelease || IsHalloween || GoldenWeek || April;
    public static bool Special = false;
    public static bool NowRoleEvent => false;
    public static List<string> OptionLoad = new();

    /// <summary>
    /// 期間限定ロールが使用可能かをチェックします<br/>
    /// 通常ロールはtrueを返します
    /// </summary>
    /// <returns>ロールが使用可能ならtrueを返します</returns>
    public static bool CheckRole(CustomRoles role, bool useApiData = true)
    {
        //イベント役職以外はtrueを返す!!
        if (!EventRoles.TryGetValue(role, out var check)) return true;

        //キャッシュ済みならそっちを使う
        if (cachedEventFlags.TryGetValue((role, useApiData), out bool cachedData))
            return cachedData;

        bool result = check.Invoke();
        //元のTOHKのサーバーからイベント情報を取る機能は削除したので、常にローカルの状態を使用
        cachedEventFlags[(role, useApiData)] = result;
        return result;
    }
    public static Dictionary<CustomRoles, Func<bool>> EventRoles = new()
    {
        //期間限定役職は全部削除したので空。今後追加するときはここに登録する
    };

    public static Dictionary<(CustomRoles role, bool isApiData), bool> cachedEventFlags = new(EventRoles.Count);
}
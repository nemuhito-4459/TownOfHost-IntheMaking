using System.Collections.Generic;
using System.Text;
using TownOfHost.Roles.Core;

namespace TownOfHost
{
    /// <summary>
    /// 役職を日本語名の五十音順(あ～ん)に並べるための読みを作る。<br/>
    /// カタカナ・ひらがなの名前は自動でひらがなに直す。<br/>
    /// 漢字を含む名前は自動で読めないので、<see cref="Readings"/> に読みを登録すること(役職を追加したときも同様)。
    /// </summary>
    public static class RoleSortHelper
    {
        /// <summary>漢字を含む役職名などの読み(ひらがな)</summary>
        public static readonly Dictionary<CustomRoles, string> Readings = new()
        {
            //バニラ
            { CustomRoles.Scientist, "かがくしゃ" },
            { CustomRoles.Detective, "たんてい" },
            //インポスター
            { CustomRoles.Amnesiac, "きおくそうしつしゃ" },
            { CustomRoles.Bomber, "ばくだんま" },
            { CustomRoles.EarnestWolf, "いちずなおおかみ" },
            { CustomRoles.FireWorks, "はなびしょくにん" },
            { CustomRoles.Tairou, "たいろう" },
            //クルー
            { CustomRoles.Bakery, "ぱんや" },
            { CustomRoles.Balancer, "てんびん" },
            { CustomRoles.FortuneTeller, "うらないし" },
            { CustomRoles.King, "くんりんしゃ" },
            { CustomRoles.MMArcher, "ほあんかん" },
            { CustomRoles.PonkotuTeller, "ぽんこつうらないし" },
            { CustomRoles.WolfBoy, "おおかみしょうねん" },
            //ジャッカル〇〇で、元の役職名から読みを作れないもの
            { CustomRoles.JackalEvilButtoner, "じゃっかるぼたなー" },
            { CustomRoles.JackalEvilTeller, "じゃっかるてらー" },
            { CustomRoles.JackalEvilgambler, "じゃっかるぎゃんぶらー" },
            //ニュートラル
            { CustomRoles.Fox, "ようこ" },
            { CustomRoles.PlagueDoctor, "ぺすといし" },
            { CustomRoles.SchrodingerCat, "しゅれでぃんがーのねこ" },
        };

        static readonly Dictionary<CustomRoles, string> cache = new();

        /// <summary>並べ替え用のキー(読みを正規化したもの)を返す</summary>
        public static string GetSortKey(CustomRoles role)
        {
            if (cache.TryGetValue(role, out var key)) return key;
            var reading = GetReading(role);
            key = Normalize(reading);
            cache[role] = key;
            return key;
        }

        /// <summary>読み(ひらがな・カタカナ)</summary>
        static string GetReading(CustomRoles role)
        {
            if (Readings.TryGetValue(role, out var reading)) return reading;
            //ジャッカル〇〇も表示されている名前(カタカナ)の順に並べる
            return Translator.GetString(role.ToString(), SupportedLangs.Japanese);
        }

        /// <summary>
        /// 1文字目から順に「清音・大きい文字」で比べ、同じなら濁点・小さい文字を含めた元の読みで比べるためのキー<br/>
        /// (例: は → ば → ぱ の順、ー は直前の母音として扱う)
        /// </summary>
        static string Normalize(string text)
        {
            var hira = new StringBuilder();
            foreach (var c in text ?? "")
            {
                //カタカナ → ひらがな
                if (c >= 'ァ' && c <= 'ヶ') hira.Append((char)(c - 0x60));
                else if (c == 'ー' && hira.Length > 0) hira.Append(VowelOf(hira[^1]));
                else if (c >= 'ぁ' && c <= 'ゖ') hira.Append(c);
                //記号・空白・タグなどは無視、それ以外(英字・漢字)は後ろに回るようそのまま
                else if (char.IsLetterOrDigit(c)) hira.Append(c);
            }
            var full = hira.ToString();
            var primary = new StringBuilder();
            foreach (var c in full) primary.Append(ToSeion(c));
            return primary + "\u0001" + full;
        }

        const string Small = "ぁぃぅぇぉっゃゅょゎゕゖ";
        const string Large = "あいうえおつやゆよわかけ";
        const string Daku = "がぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽゔ";
        const string Sei = "かきくけこさしすせそたちつてとはひふへほはひふへほう";

        static char ToSeion(char c)
        {
            var i = Small.IndexOf(c);
            if (i >= 0) return Large[i];
            i = Daku.IndexOf(c);
            if (i >= 0) return Sei[i];
            return c;
        }

        static char VowelOf(char c)
        {
            c = ToSeion(c);
            const string a = "あかさたなはまやらわ", i = "いきしちにひみり", u = "うくすつぬふむゆる", e = "えけせてねへめれ", o = "おこそとのほもよろを";
            if (a.IndexOf(c) >= 0) return 'あ';
            if (i.IndexOf(c) >= 0) return 'い';
            if (u.IndexOf(c) >= 0) return 'う';
            if (e.IndexOf(c) >= 0) return 'え';
            if (o.IndexOf(c) >= 0) return 'お';
            return 'ー';
        }
    }
}

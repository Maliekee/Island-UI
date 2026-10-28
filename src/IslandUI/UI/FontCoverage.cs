using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace IslandUI.UI
{
    // WHICH LANGUAGES OUR TYPE CAN SET (Forgejo #19). For the harness:
    //   game.ps1 invoke IslandUI.UI.FontCoverage Report
    // lists every TMP font loaded, how it gets its glyphs (Static: only what its atlas
    // was baked with; Dynamic: anything in its source font), its fallbacks, and for each
    // shipped language the characters it cannot set even through those fallbacks.
    internal static class FontCoverage
    {
        // each language's alphabet and the punctuation a translation reaches for; for
        // Chinese and Japanese, the most frequent characters and the words of our UI
        private static readonly string[][] Samples =
        {
            new[] { "ru", "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя№«»—…" },
            new[] { "es", "ÁÉÍÓÚÑÜáéíóúñü¿¡«»—…" },
            new[] { "de", "ÄÖÜäöüßẞ„“‚‘—…" },
            new[] { "ptbr", "ÁÂÃÀÇÉÊÍÓÔÕÚáâãàçéêíóôõú“”—…" },
            new[] { "cn", "的一是不了人我在有他这为之大来以个中上们到说国和地也子时道出而要于就下得可你年生自会那后能对着事其里所去行过家十用发天如然作方成者多日都三小军二无同么经法当起与好看学进种将还分此心前面又定见只主没公从知"
                + "设置筛选制作光标选择你岛上使用的指针更改立即生效完成名册附近在家角色物品社交命令生命食物水分眩晕工具腰带菜单"
                + "爱情心情睡觉房屋区域距离背包装备武器材料配方数量最大取消确认关闭打开搜索没有匹配饥饿口渴体力攻击防御速度等级经验"
                + "，。、：；？！（）《》“”…—·" },
            new[] { "jp", "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽゃゅょっ"
                + "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポャュョッー"
                + "設定絞込作成名簿近在宅持物交流命令体力食料水分気絶道具帯選択島使用変更即反映完了愛情気分睡眠家範囲距離装備武器材料数量最大取消確認閉開検索"
                + "、。・「」『』（）！？…ー～" },
        };

        internal static string Report()
        {
            var sb = new StringBuilder();
            var seen = new HashSet<TMP_FontAsset>();
            foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (font == null || !seen.Add(font))
                {
                    continue;
                }
                sb.Append(font.name).Append(" [").Append(font.atlasPopulationMode)
                    .Append(font.sourceFontFile != null ? ", " + font.sourceFontFile.name : ", no source")
                    .Append(", ").Append(font.characterTable != null ? font.characterTable.Count : 0).Append(" chars]");
                if (font.fallbackFontAssetTable != null && font.fallbackFontAssetTable.Count > 0)
                {
                    sb.Append(" -> ");
                    for (int i = 0; i < font.fallbackFontAssetTable.Count; i++)
                    {
                        TMP_FontAsset f = font.fallbackFontAssetTable[i];
                        sb.Append(i > 0 ? ", " : "").Append(f != null ? f.name : "null");
                    }
                }
                sb.Append('\n');
                foreach (string[] sample in Samples)
                {
                    font.HasCharacters(sample[1], out uint[] missing, true, true);
                    if (missing != null && missing.Length > 0)
                    {
                        sb.Append("    ").Append(sample[0]).Append(" lacks ").Append(missing.Length).Append(": ");
                        for (int i = 0; i < missing.Length && i < 40; i++)
                        {
                            sb.Append((char)missing[i]);
                        }
                        sb.Append('\n');
                    }
                }
            }
            // the texts set in a face that cannot reach the game's chain
            foreach (TMP_Text text in Object.FindObjectsOfType<TMP_Text>(true))
            {
                if (text != null && text.font != null && text.font.fallbackFontAssetTable.Count == 0
                    && text.font.atlasPopulationMode == AtlasPopulationMode.Static)
                {
                    sb.Append("no fallback: ").Append(text.font.name).Append(" on ").Append(PathOf(text.transform)).Append('\n');
                }
            }
            return sb.ToString();
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent)
            {
                path = p.name + "/" + path;
            }
            return path;
        }
    }
}

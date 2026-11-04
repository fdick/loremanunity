using System;
using System.Collections.Generic;

namespace Loreman.Dialogues.Editor
{
    /// <summary>One language of a build as the window shows it: its Loreman code, the Unity locale it goes to, and how much of it is translated.</summary>
    public sealed class LanguageRow
    {
        public string LoremanCode { get; set; }

        public string UnityCode { get; set; }

        /// <summary>The Unity locale comes from the settings (a mapping), not from the code itself.</summary>
        public bool IsMapped { get; set; }

        /// <summary>The project has a Unity locale with this code. When it has not, the sync makes one (or fails, by the setting).</summary>
        public bool LocaleExists { get; set; }

        public bool IsBase { get; set; }

        public int Total { get; set; }

        public int Translated { get; set; }

        public int Stale { get; set; }

        /// <summary>The share of the texts that have a translation, 0 to 1 (1 for the language of the project).</summary>
        public float Progress => Total == 0 ? 1f : (float)Translated / Total;
    }

    public static class LanguageRows
    {
        /// <param name="existingUnityCodes">The codes of the Unity locales the project has now.</param>
        public static List<LanguageRow> Build(
            IReadOnlyList<ManifestLanguage> languages, string baseLanguage, IReadOnlyDictionary<string, string> mapping, ISet<string> existingUnityCodes)
        {
            var rows = new List<LanguageRow>(languages.Count);
            foreach (var l in languages)
            {
                var mapped = mapping != null && mapping.TryGetValue(l.code, out var chosen) && !string.IsNullOrWhiteSpace(chosen);
                var unity = LocaleCodes.ToUnity(l.code, mapping);
                rows.Add(new LanguageRow
                {
                    LoremanCode = l.code,
                    UnityCode = unity,
                    IsMapped = mapped,
                    LocaleExists = existingUnityCodes != null && ContainsIgnoreCase(existingUnityCodes, unity),
                    IsBase = string.Equals(l.code, baseLanguage, StringComparison.OrdinalIgnoreCase),
                    Total = l.total,
                    Translated = l.translated,
                    Stale = l.stale,
                });
            }
            rows.Sort((a, b) => a.IsBase != b.IsBase ? (a.IsBase ? -1 : 1) : string.CompareOrdinal(a.LoremanCode, b.LoremanCode));
            return rows;
        }

        private static bool ContainsIgnoreCase(ISet<string> set, string code)
        {
            foreach (var c in set)
                if (string.Equals(c, code, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}

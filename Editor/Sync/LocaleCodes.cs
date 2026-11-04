using System.Collections.Generic;
using System.Globalization;

namespace Loreman.Dialogues.Editor
{
    /// <summary>
    /// Loreman writes a language as a lower-case code (<c>pt-br</c>); Unity Localization names a locale <c>pt-BR</c>. This turns one into the other,
    /// unless the settings say otherwise.
    /// </summary>
    public static class LocaleCodes
    {
        public static string ToUnity(string loremanCode, IReadOnlyDictionary<string, string> overrides = null)
        {
            if (overrides != null && overrides.TryGetValue(loremanCode, out var chosen) && !string.IsNullOrWhiteSpace(chosen)) return chosen.Trim();
            try
            {
                // the culture knows its own spelling (pt-br → pt-BR); an unknown code is kept as it is
                var name = CultureInfo.GetCultureInfo(loremanCode).Name;
                return string.IsNullOrEmpty(name) ? loremanCode : name;
            }
            catch (CultureNotFoundException)
            {
                return loremanCode;
            }
        }
    }
}

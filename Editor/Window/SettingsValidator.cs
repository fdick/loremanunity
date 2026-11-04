using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Loreman.Dialogues.Editor
{
    public enum IssueSeverity
    {
        Warning,
        Error,
    }

    /// <summary>A problem with the settings: which field, and what to do about it.</summary>
    public readonly struct SettingsIssue
    {
        public SettingsIssue(IssueSeverity severity, string field, string message)
        {
            Severity = severity;
            Field = field;
            Message = message;
        }

        public IssueSeverity Severity { get; }

        /// <summary>The name of the field in <see cref="LoremanSettings"/>.</summary>
        public string Field { get; }

        public string Message { get; }
    }

    /// <summary>
    /// Checks <see cref="LoremanSettings"/> before a sync uses them and while the window edits them. An error stops the sync; a warning is only said.
    /// Pure: it looks at the settings and nothing else, so it is tested without the editor.
    /// </summary>
    public static class SettingsValidator
    {
        private static readonly char[] BadPathChars = { ':', '*', '?', '"', '<', '>', '|', '\\' };

        public static IReadOnlyList<SettingsIssue> Validate(LoremanSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var issues = new List<SettingsIssue>();
            CheckFolder(settings.outputFolder, issues);

            if (string.IsNullOrWhiteSpace(settings.addressablesGroup))
                issues.Add(new SettingsIssue(IssueSeverity.Error, nameof(LoremanSettings.addressablesGroup), "The Addressables group needs a name."));

            CheckOverrides(settings.localeOverrides, issues);
            return issues;
        }

        public static bool HasErrors(IEnumerable<SettingsIssue> issues) => issues.Any(i => i.Severity == IssueSeverity.Error);

        private static void CheckFolder(string folder, List<SettingsIssue> issues)
        {
            const string field = nameof(LoremanSettings.outputFolder);
            var path = (folder ?? string.Empty).Trim().TrimEnd('/');
            if (path.Length == 0)
            {
                issues.Add(new SettingsIssue(IssueSeverity.Error, field, "Choose the folder where the importer puts what it makes."));
                return;
            }
            if (path != "Assets" && !path.StartsWith("Assets/", StringComparison.Ordinal))
                issues.Add(new SettingsIssue(IssueSeverity.Error, field, "The folder must be inside the Assets folder (Assets/Loreman/Generated)."));
            else if (path.IndexOfAny(BadPathChars) >= 0 || path.Split('/').Any(part => part == ".." || part == "." || part.Length == 0))
                issues.Add(new SettingsIssue(IssueSeverity.Error, field, "The folder name has characters Unity cannot use (\\ : * ? \" < > | .. or an empty part)."));
            else if (path == "Assets")
                issues.Add(new SettingsIssue(IssueSeverity.Warning, field, "The importer makes and deletes files in this folder: give it a folder of its own, not Assets itself."));
        }

        private static void CheckOverrides(List<LocaleOverride> overrides, List<SettingsIssue> issues)
        {
            const string field = nameof(LoremanSettings.localeOverrides);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in overrides)
            {
                var from = o.loremanCode?.Trim();
                var to = o.unityCode?.Trim();
                if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
                {
                    issues.Add(new SettingsIssue(IssueSeverity.Error, field, "A language mapping needs both codes: the one Loreman uses and the Unity locale."));
                    continue;
                }
                if (!seen.Add(from))
                    issues.Add(new SettingsIssue(IssueSeverity.Error, field, $"The language \"{from}\" is mapped twice."));
                if (!IsKnownCulture(to))
                    issues.Add(new SettingsIssue(IssueSeverity.Warning, field, $"\"{to}\" is not a language code Unity knows; the locale will be made with this code as it is."));
            }
        }

        private static bool IsKnownCulture(string code)
        {
            try
            {
                CultureInfo.GetCultureInfo(code);
                return true;
            }
            catch (CultureNotFoundException)
            {
                return false;
            }
        }
    }
}

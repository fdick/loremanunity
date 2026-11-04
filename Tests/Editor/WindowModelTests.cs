using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Loreman.Dialogues.Editor;

namespace Loreman.Dialogues.Tests
{
    /// <summary>The parts of the Loreman window that decide things (the window itself only draws them).</summary>
    public class WindowModelTests
    {
        private LoremanSettings _settings;

        [SetUp]
        public void SetUp() => _settings = ScriptableObject.CreateInstance<LoremanSettings>();

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_settings);

        private static IEnumerable<SettingsIssue> Errors(IEnumerable<SettingsIssue> issues) => issues.Where(i => i.Severity == IssueSeverity.Error);

        // ── the settings ──

        [Test]
        public void The_default_settings_have_no_problems()
        {
            Assert.IsEmpty(SettingsValidator.Validate(_settings));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Packages/Loreman")]
        [TestCase("Library/Loreman")]
        [TestCase("Assets/../Outside")]
        [TestCase("Assets//Twice")]
        [TestCase("Assets/Bad:Name")]
        [TestCase("Assets\\Back\\Slash")]
        public void A_folder_outside_assets_or_with_bad_characters_is_an_error(string folder)
        {
            _settings.outputFolder = folder;
            var errors = Errors(SettingsValidator.Validate(_settings)).ToList();
            Assert.AreEqual(1, errors.Count, folder);
            Assert.AreEqual(nameof(LoremanSettings.outputFolder), errors[0].Field);
        }

        [Test]
        public void A_folder_may_end_with_a_slash_and_assets_itself_is_only_a_warning()
        {
            _settings.outputFolder = "Assets/Loreman/Generated/";
            Assert.IsEmpty(SettingsValidator.Validate(_settings));
            _settings.outputFolder = "Assets";
            var issues = SettingsValidator.Validate(_settings);
            Assert.IsFalse(SettingsValidator.HasErrors(issues));
            Assert.AreEqual(IssueSeverity.Warning, issues.Single().Severity);
        }

        [Test]
        public void The_addressables_group_needs_a_name()
        {
            _settings.addressablesGroup = " ";
            Assert.AreEqual(nameof(LoremanSettings.addressablesGroup), Errors(SettingsValidator.Validate(_settings)).Single().Field);
        }

        [Test]
        public void A_language_mapping_needs_both_codes_and_a_language_is_mapped_once()
        {
            _settings.localeOverrides.Add(new LocaleOverride { loremanCode = "pt-br", unityCode = "" });
            Assert.AreEqual(1, Errors(SettingsValidator.Validate(_settings)).Count());

            _settings.localeOverrides.Clear();
            _settings.localeOverrides.Add(new LocaleOverride { loremanCode = "pt-br", unityCode = "pt" });
            _settings.localeOverrides.Add(new LocaleOverride { loremanCode = "PT-BR", unityCode = "pt-PT" });
            var errors = Errors(SettingsValidator.Validate(_settings)).ToList();
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("twice", errors[0].Message);
        }

        [Test]
        public void A_locale_code_unity_does_not_know_is_a_warning_not_an_error()
        {
            _settings.localeOverrides.Add(new LocaleOverride { loremanCode = "tlh", unityCode = "not-a-real-code-1234" });
            var issues = SettingsValidator.Validate(_settings);
            Assert.IsFalse(SettingsValidator.HasErrors(issues));
            Assert.AreEqual(IssueSeverity.Warning, issues.Single().Severity);
        }

        // ── the languages ──

        private static List<ManifestLanguage> Languages() => new List<ManifestLanguage>
        {
            new ManifestLanguage { code = "ru", total = 10, translated = 4, stale = 1 },
            new ManifestLanguage { code = "en", total = 10, translated = 10 },
            new ManifestLanguage { code = "pt-br", total = 10, translated = 0 },
        };

        [Test]
        public void The_base_language_comes_first_and_every_language_shows_the_unity_locale_it_goes_to()
        {
            var rows = LanguageRows.Build(Languages(), "en", null, new HashSet<string> { "en", "ru" });
            CollectionAssert.AreEqual(new[] { "en", "pt-br", "ru" }, rows.Select(r => r.LoremanCode));
            Assert.IsTrue(rows[0].IsBase);
            Assert.AreEqual("pt-BR", rows[1].UnityCode);
            Assert.IsFalse(rows[1].LocaleExists); // the sync makes it
            Assert.IsTrue(rows[2].LocaleExists);
            Assert.AreEqual(0.4f, rows[2].Progress, 0.001f);
            Assert.AreEqual(1, rows[2].Stale);
        }

        [Test]
        public void A_mapping_from_the_settings_changes_the_locale_and_is_marked()
        {
            var rows = LanguageRows.Build(Languages(), "en", new Dictionary<string, string> { ["pt-br"] = "pt" }, new HashSet<string> { "PT" });
            var pt = rows.Single(r => r.LoremanCode == "pt-br");
            Assert.AreEqual("pt", pt.UnityCode);
            Assert.IsTrue(pt.IsMapped);
            Assert.IsTrue(pt.LocaleExists, "codes are compared without regard to case");
            Assert.IsFalse(rows.Single(r => r.LoremanCode == "ru").IsMapped);
        }

        [Test]
        public void A_language_with_nothing_to_translate_counts_as_done()
        {
            var rows = LanguageRows.Build(new List<ManifestLanguage> { new ManifestLanguage { code = "ru" } }, "en", null, new HashSet<string>());
            Assert.AreEqual(1f, rows[0].Progress);
        }

        // ── the builds ──

        [Test]
        public void A_build_is_read_for_the_window_and_is_synced_only_when_the_catalog_came_from_it()
        {
            var path = SampleBuild.Path;
            string hash;
            Guid project;
            using (var file = SampleBuild.Open())
            {
                hash = file.Manifest.contentHash;
                project = Guid.Parse(file.Manifest.projectId);
            }
            IReadOnlyDictionary<Guid, string> Synced(string h, Guid p) => new Dictionary<Guid, string> { [p] = h };

            var fresh = BuildListing.Read(path, "Assets/x/sample.lorebuild", null);
            Assert.AreEqual(project, fresh.ProjectId);
            Assert.IsNull(fresh.Error);
            Assert.AreEqual("My Game!", fresh.ProjectName);
            Assert.AreEqual("sample.lorebuild", fresh.FileName);
            Assert.AreEqual(1, fresh.Dialogues);
            Assert.AreEqual(3, fresh.Languages.Count);
            Assert.IsFalse(fresh.IsSynced);

            Assert.IsTrue(BuildListing.Read(path, "Assets/x/sample.lorebuild", Synced(hash, project)).IsSynced);
            Assert.IsFalse(BuildListing.Read(path, "Assets/x/sample.lorebuild", Synced("an older build", project)).IsSynced);
            Assert.IsFalse(BuildListing.Read(path, "Assets/x/sample.lorebuild", Synced(hash, Guid.NewGuid())).IsSynced, "another project's catalog does not make this build synced");
        }

        private static readonly Guid ProjectA = Guid.NewGuid();
        private static readonly Guid ProjectB = Guid.NewGuid();

        private static BuildInfo B(string built, bool synced = false, string error = null, Guid? project = null) =>
            new BuildInfo { AssetPath = "Assets/" + built + ".lorebuild", BuiltAt = built, IsSynced = synced, Error = error, ProjectId = project ?? ProjectA };

        [Test]
        public void The_project_is_brought_to_the_newest_readable_build_unless_it_already_has_one_of_them()
        {
            Assert.IsEmpty(BuildListing.PickToSync(new BuildInfo[0]));
            Assert.AreEqual("2026-11-05", BuildListing.PickToSync(new[] { B("2026-11-01"), B("2026-11-05"), B("2026-11-09", error: "damaged") }).Single().BuiltAt);
            Assert.IsEmpty(BuildListing.PickToSync(new[] { B("2026-11-01", synced: true), B("2026-11-05") }), "an older build is the project's own choice: leave it");
            Assert.IsEmpty(BuildListing.PickToSync(new[] { B("x", error: "damaged") }));
        }

        [Test]
        public void Every_project_gets_its_own_newest_build_so_builds_of_different_projects_do_not_compete()
        {
            var picked = BuildListing.PickToSync(new[]
            {
                B("2026-11-01", project: ProjectA), B("2026-11-09", project: ProjectA),
                B("b-only", project: ProjectB),
            });
            CollectionAssert.AreEquivalent(new[] { "2026-11-09", "b-only" }, picked.Select(b => b.BuiltAt));

            // project B is already synced, project A is not
            var onlyA = BuildListing.PickToSync(new[] { B("2026-11-09", project: ProjectA), B("b-only", synced: true, project: ProjectB) });
            CollectionAssert.AreEqual(new[] { "2026-11-09" }, onlyA.Select(b => b.BuiltAt));
        }

        [Test]
        public void A_file_that_is_not_a_build_is_listed_with_the_reason_instead_of_failing()
        {
            var bad = System.IO.Path.GetTempFileName();
            try
            {
                System.IO.File.WriteAllBytes(bad, new byte[] { 1, 2, 3 });
                var info = BuildListing.Read(bad, "Assets/bad.lorebuild", null);
                Assert.IsNotNull(info.Error);
                Assert.IsFalse(info.IsSynced);
            }
            finally
            {
                System.IO.File.Delete(bad);
            }
        }

        // ── the window ──

        [Test]
        public void The_window_builds_its_screen_and_refreshes_without_errors()
        {
            var window = ScriptableObject.CreateInstance<LoremanWindow>();
            try
            {
                window.CreateGUI();
                Assert.Greater(window.rootVisualElement.childCount, 0);
                window.Refresh();
                Assert.IsNotNull(window.rootVisualElement.Q<UnityEngine.UIElements.Label>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }
    }
}

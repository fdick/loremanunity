using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Loreman.Dialogues.Editor
{
    /// <summary>
    /// The window of the package (Tools → Loreman → Window): the builds in the project and whether they are synced, the settings of the importer
    /// (with problems said at once), the languages and the Unity locales they go to, and the dialogues of the catalog.
    /// The window only shows and asks; what is decided and done lives in <see cref="SettingsValidator"/>, <see cref="LanguageRows"/>,
    /// <see cref="BuildListing"/> and <see cref="LoreBuildSync"/>, which are tested without it.
    /// </summary>
    public sealed class LoremanWindow : EditorWindow
    {
        private const string BuildsFolder = "Assets/Loreman/Builds";

        private LoremanSettings _settings;
        private SerializedObject _settingsObject;
        private VisualElement _builds;
        private VisualElement _issues;
        private VisualElement _languages;
        private VisualElement _dialogues;
        private Label _status;

        [MenuItem("Tools/Loreman/Window")]
        public static void Open()
        {
            var window = GetWindow<LoremanWindow>();
            window.titleContent = new GUIContent("Loreman");
            window.minSize = new Vector2(420, 360);
        }

        private void OnEnable() => EditorApplication.projectChanged += Refresh;

        private void OnDisable() => EditorApplication.projectChanged -= Refresh;

        private void OnFocus() => Refresh();

        public void CreateGUI()
        {
            _settings = LoremanSettings.GetOrCreate();
            _settingsObject = new SerializedObject(_settings);

            var scroll = new ScrollView();
            rootVisualElement.Add(scroll);
            var root = scroll.contentContainer;
            root.style.paddingLeft = root.style.paddingRight = 10;
            root.style.paddingTop = root.style.paddingBottom = 8;

            root.Add(Title("Loreman Dialogues"));
            root.Add(Section("Builds"));
            _builds = new VisualElement();
            root.Add(_builds);
            var importRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            importRow.Add(new Button(ImportBuild) { text = "Add a .lorebuild file…", tooltip = "Copies a build into Assets/Loreman/Builds. It is synced as soon as Unity imports it." });
            importRow.Add(new Button(SyncAll) { text = "Sync all" });
            root.Add(importRow);
            _status = new Label { style = { marginTop = 4, opacity = 0.8f, whiteSpace = WhiteSpace.Normal } };
            root.Add(_status);

            root.Add(Section("Settings"));
            _issues = new VisualElement();
            root.Add(_issues);
            root.Add(SettingsFields());

            root.Add(Section("Languages"));
            _languages = new VisualElement();
            root.Add(_languages);

            root.Add(Section("Dialogues in the project"));
            _dialogues = new VisualElement();
            root.Add(_dialogues);

            root.Bind(_settingsObject);
            root.TrackSerializedObjectValue(_settingsObject, _ =>
            {
                AssetDatabase.SaveAssetIfDirty(_settings);
                ShowIssues();
                ShowLanguages();
            });
            Refresh();
        }

        // ── the settings ──

        private VisualElement SettingsFields()
        {
            var box = new VisualElement();
            var folder = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var folderField = new PropertyField(_settingsObject.FindProperty(nameof(LoremanSettings.outputFolder)));
            folderField.style.flexGrow = 1;
            folder.Add(folderField);
            folder.Add(new Button(ChooseFolder) { text = "Choose…", style = { height = 20, alignSelf = Align.FlexEnd } });
            box.Add(folder);
            box.Add(new PropertyField(_settingsObject.FindProperty(nameof(LoremanSettings.addressablesGroup))));
            box.Add(new PropertyField(_settingsObject.FindProperty(nameof(LoremanSettings.packDialoguesSeparately))));
            box.Add(new PropertyField(_settingsObject.FindProperty(nameof(LoremanSettings.createMissingLocales))));
            box.Add(new PropertyField(_settingsObject.FindProperty(nameof(LoremanSettings.localeOverrides))));
            return box;
        }

        private void ChooseFolder()
        {
            var picked = EditorUtility.OpenFolderPanel("Where the importer puts what it makes", Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(picked)) return;
            var project = Path.GetFullPath(Application.dataPath + "/..").Replace('\\', '/');
            picked = picked.Replace('\\', '/');
            if (!picked.StartsWith(project + "/"))
            {
                EditorUtility.DisplayDialog("Loreman", "Choose a folder inside this Unity project (under Assets).", "OK");
                return;
            }
            _settingsObject.Update();
            _settingsObject.FindProperty(nameof(LoremanSettings.outputFolder)).stringValue = picked.Substring(project.Length + 1);
            _settingsObject.ApplyModifiedProperties();
        }

        private void ShowIssues()
        {
            _issues.Clear();
            foreach (var issue in SettingsValidator.Validate(_settings))
                _issues.Add(new HelpBox(issue.Message, issue.Severity == IssueSeverity.Error ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning));
        }

        // ── what the project has ──

        public void Refresh()
        {
            if (_builds == null || _settings == null) return;
            var catalogs = CatalogLocator.FindAll();
            var builds = BuildListing.FindAll(CatalogLocator.HashesByProject());

            _builds.Clear();
            if (builds.Count == 0)
                _builds.Add(new HelpBox("No .lorebuild file in the project. Build the project in Loreman (the hammer on its card), then add the file here.", HelpBoxMessageType.Info));
            foreach (var build in builds) _builds.Add(BuildCard(build));

            ShowIssues();
            ShowLanguages(builds);
            ShowDialogues(catalogs);
        }

        private VisualElement BuildCard(BuildInfo build)
        {
            var card = Card();
            var head = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
            head.Add(new Label(string.IsNullOrEmpty(build.ProjectName) ? build.FileName : build.ProjectName) { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            if (build.Error == null)
                head.Add(new Label(build.IsSynced ? "● synced" : "○ not synced") { style = { color = build.IsSynced ? new Color(0.35f, 0.75f, 0.4f) : new Color(0.9f, 0.65f, 0.2f) } });
            card.Add(head);

            if (build.Error != null)
            {
                card.Add(new HelpBox(build.Error, HelpBoxMessageType.Error));
            }
            else
            {
                card.Add(new Label($"{build.AssetPath}\nBuilt {build.BuiltAt}\n{build.Dialogues} dialogue(s) · {build.Speakers} speaker(s) · {build.Languages.Count} language(s)")
                    { style = { opacity = 0.75f, whiteSpace = WhiteSpace.Normal } });
            }

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            var path = build.AssetPath;
            row.Add(new Button(() => Sync(path, false)) { text = build.IsSynced ? "Sync again" : "Sync" });
            row.Add(new Button(() => Sync(path, true)) { text = "Force sync", tooltip = "Remakes every dialogue, even the ones that did not change." });
            row.Add(new Button(() => EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path))) { text = "Show" });
            card.Add(row);
            return card;
        }

        private void ShowLanguages(IReadOnlyList<BuildInfo> builds)
        {
            if (_languages == null) return;
            _languages.Clear();
            var readable = builds.Where(b => b.Error == null).ToList();
            if (readable.Count == 0)
            {
                _languages.Add(new Label("The languages appear when there is a build.") { style = { opacity = 0.7f } });
                return;
            }

            var existing = LocalizationEditorSettings.GetLocales().Select(l => l.Identifier.Code).ToHashSet();
            foreach (var build in readable)
            {
                // with several builds each one has its own list, under its name
                if (readable.Count > 1)
                    _languages.Add(new Label(string.IsNullOrEmpty(build.ProjectName) ? build.FileName : build.ProjectName) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 4 } });
                ShowLanguageRows(build, existing);
            }
        }

        private void ShowLanguageRows(BuildInfo build, HashSet<string> existing)
        {
            foreach (var row in LanguageRows.Build(build.Languages, build.BaseLanguage, _settings.LocaleMap(), existing))
            {
                var line = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };
                line.Add(new Label(row.LoremanCode + (row.IsBase ? "  (base)" : string.Empty)) { style = { width = 110 } });
                line.Add(new Label("→ " + row.UnityCode + (row.IsMapped ? "  (mapped)" : string.Empty)) { style = { width = 130 } });
                line.Add(new Label(row.LocaleExists ? "locale ready" : "locale will be made") { style = { width = 130, opacity = 0.75f } });
                var bar = new ProgressBar { value = row.Progress * 100f, title = $"{row.Translated}/{row.Total}" + (row.Stale > 0 ? $" · {row.Stale} out of date" : string.Empty) };
                bar.style.flexGrow = 1;
                line.Add(bar);
                _languages.Add(line);
            }
        }

        private void ShowLanguages() => ShowLanguages(BuildListing.FindAll(CatalogLocator.HashesByProject()));

        private void ShowDialogues(IReadOnlyList<DialogueCatalog> catalogs)
        {
            _dialogues.Clear();
            if (catalogs.Count == 0)
            {
                _dialogues.Add(new Label("Nothing is synced yet.") { style = { opacity = 0.7f } });
                return;
            }
            var all = DialogueSearch.Entries(catalogs);
            var shown = all;
            var summary = new Label { style = { marginBottom = 2, opacity = 0.75f } };
            var list = new ListView(shown, 20, () => new Label(), (element, i) =>
            {
                var d = shown[i];
                ((Label)element).text = $"{d.FullName}   ({d.Nodes} nodes)";
            })
            {
                selectionType = SelectionType.None,
                showBorder = true,
            };

            void Show(string query)
            {
                shown = DialogueSearch.Filter(all, query);
                list.itemsSource = shown;
                list.style.height = Mathf.Clamp(shown.Count * 20 + 4, 44, 220);
                list.Rebuild();
                summary.text = string.IsNullOrWhiteSpace(query)
                    ? $"{all.Count} dialogue(s) in {catalogs.Count} build(s)"
                    : $"{shown.Count} of {all.Count} dialogue(s)";
            }

            var search = new ToolbarSearchField { style = { width = Length.Auto(), marginBottom = 4 } };
            search.RegisterValueChangedCallback(e => Show(e.newValue));
            _dialogues.Add(search);
            _dialogues.Add(summary);
            _dialogues.Add(list);
            Show(string.Empty);
        }

        // ── actions ──

        private void Sync(string assetPath, bool force)
        {
            var report = LoreBuildSync.SyncAsset(assetPath, force);
            _status.text = report == null ? "The sync did not finish: see the Console." : report.ToString();
            Refresh();
        }

        private void SyncAll()
        {
            var builds = LoreBuildSync.FindBuilds();
            if (builds.Length == 0)
            {
                _status.text = "No .lorebuild file in the project.";
                return;
            }
            foreach (var path in builds) Sync(path, false);
        }

        private void ImportBuild()
        {
            var picked = EditorUtility.OpenFilePanel("Choose a .lorebuild file made by Loreman", string.Empty, "lorebuild");
            if (string.IsNullOrEmpty(picked)) return;
            LoremanSettings.EnsureFolder(BuildsFolder);
            var target = BuildsFolder + "/" + Path.GetFileName(picked);
            File.Copy(picked, target, true);
            AssetDatabase.ImportAsset(target); // the postprocessor syncs it when the import is over
            _status.text = "Added " + target + ". It is synced now.";
        }

        // ── small pieces ──

        private static Label Title(string text) => new Label(text) { style = { fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 } };

        private static Label Section(string text) => new Label(text) { style = { marginTop = 12, marginBottom = 4, unityFontStyleAndWeight = FontStyle.Bold, borderBottomWidth = 1, borderBottomColor = new Color(0.5f, 0.5f, 0.5f, 0.4f) } };

        private static VisualElement Card()
        {
            var card = new VisualElement();
            card.style.marginBottom = 4;
            card.style.paddingLeft = card.style.paddingRight = card.style.paddingTop = card.style.paddingBottom = 6;
            card.style.borderLeftWidth = card.style.borderRightWidth = card.style.borderTopWidth = card.style.borderBottomWidth = 1;
            card.style.borderLeftColor = card.style.borderRightColor = card.style.borderTopColor = card.style.borderBottomColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 4;
            return card;
        }
    }
}

# Changelog

## 0.4.0

- **`DialoguePlayer`** component: owns the service, plays a dialogue and its voice-overs, raises `Ready` / `Presented` / `Refreshed` / `Finished` / `Failed`; `Play`, `Advance`, `Choose`, `Stop`. A screen only listens to it.
- Samples: **Canvas Dialogue UI (uGUI)** and **UI Toolkit Dialogue UI** (UXML + USS + Panel Settings), each self-contained with a scene and the demo build. All four sample scenes are verified in Play Mode, clicking their buttons.
- The searchable dialogue picker (0.3.1) is part of this release.
- The import tests refuse to run in a project that already has Loreman data (they would overwrite it).

## 0.3.1

- The dialogue field (`DialogueReference`) opens a **searchable picker**: the search box is focused, every word must match the group or the name, ↑ ↓ Enter Esc work, best matches first, × clears. The search lives in `DialogueSearch` (tested without the editor).
- The dialogue list in the Loreman window has the same search.

## 0.3.0

- **Loreman window** (*Tools → Loreman → Window*): builds and their sync state, add a build file, settings with validation, languages ↔ Unity locales, the dialogues of the catalog.
- **Self-contained samples**: each brings its scene, its script and a demo build (`CityGate.lorebuild`); import, open the scene, press Play. Both are verified in Play Mode.
- The project is synced again after a recompile when it is not at a build (a build imported together with scripts, e.g. a sample, is no longer missed).
- The sync makes the *Localization Settings* asset when the project has none, and notices when a dialogue or the catalog lost its Addressables entry.
- `LoremanCli.SyncAll` / `ForceSyncAll` for build machines; settings are validated before a sync.
- The importer reads the recordings of a dialogue one by one (the matching change in Loreman streams the build).

## 0.2.0

- Voice-overs: imported as audio clips into one Localization asset table per dialogue (a clip per language); `DialogueView.HasAudio`, `DialogueConversation.LoadAudioAsync`, `IDialogueAudioSource` / `LocalizationAudioSource`. Needs a build of format version 2 (graph version 1 files are still read).
- `DialogueReference`: pick a dialogue in the inspector from a list.
- Samples: Basic Dialogue, Preload and Resume.

## 0.1.0

- `.lorebuild` importer: Addressables graphs, one Unity Localization table per dialogue, speakers table, locales, incremental sync.
- Runtime: `DialogueService`, `DialogueConversation`, `DialogueLibrary` (on-demand loading, reference counting), `LocalizationTextSource` (current language with fallback to the base language, live language switch).

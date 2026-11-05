# Changelog

## 0.7.0

- **A dialogue clip is as long as its dialogue** when the dialogue goes on by itself. Picking the dialogue or changing *Auto Advance* / *Seconds* sets the length of the clip (and the **Fit Clip To Dialogue** button in the inspector does it on demand). The inspector shows how long the dialogue takes: lines count their time (*After Voice Over*: the longest recording of the line among the languages, plus the pause), a choice counts only its longest answer, a loop is counted once round. The clip gets half a second of slack.
- `DialogueLengthEstimator` (pure, tested) and `DialogueLengthMeasure` (reads the imported dialogue and its audio tables) are public, in case a game wants the length of a dialogue.

## 0.6.1

- **Timeline sample showed magenta objects in URP/HDRP projects** (the primitives carried the Built-in pipeline's material). The sample now paints its objects at run time with the default material of the render pipeline in use, so it looks right in Built-in, URP and HDRP.

## 0.6.0

- **Unity Timeline integration.** A *Loreman/Dialogue Track* takes *Dialogue Clips*: when a clip starts, the `DialoguePlayer` bound to the track plays the dialogue and the timeline waits there until it is over (or goes on and stops the dialogue at the end of the clip). The dialogue is picked from the searchable list and the clip shows its name. Optional: the assembly exists only where `com.unity.timeline` 1.6 or newer is installed.
- **Automatic advance** for cutscenes: `DialoguePlayer.SetAutoAdvance(Manual | AfterSeconds | AfterVoiceOver, seconds)`. A choice always waits for the player; with `AfterVoiceOver` a line goes on when its recording has been said.
- New sample **Timeline Dialogue** (scene, timeline asset, animation clips), played in Play Mode by the tests: the timeline is held while the dialogue runs and goes on after it.

## 0.5.3

- The package is named **Loreman** in the Package Manager (it was "Loreman Dialogues") and the author is **Ousiron**. Samples are imported to `Assets/Samples/Loreman/<version>/…` now. The package id `com.loreman.unity` and the code namespaces are unchanged.

## 0.5.2

- **UI Toolkit sample drew no text.** Its Panel Settings pointed at the default theme of the project that made the sample, a file that does not exist anywhere else (Unity warned: *No Theme Style Sheet set to PanelSettings DialoguePanelSettings, UI will not render properly*). The sample now ships its own theme, `DialogueTheme.tss` (it imports Unity's default runtime theme), and the Panel Settings use it.
- The UI Toolkit sample makes an `EventSystem` with the Input System's UI module when the scene has none (found by name, so it compiles without the Unity UI / Input System packages).
- The sample scenes are now also played in a project that uses **only the new Input System**, clicking the Talk buttons with a virtual mouse (Canvas and UI Toolkit), and the test fails on the theme warning.

## 0.5.1

- **UI Toolkit sample showed nothing** in projects where Unity does not make default Panel Settings for you: the scene was saved without its Panel Settings reference (the generator loaded the asset before `NewScene`, which unloaded it). The scene references `DialoguePanelSettings` now, and the screen warns and falls back to defaults if the reference is ever missing. (It worked in the clean test projects only because of that default.)
- The automatic sync after a recompile now **waits** until the editor is free instead of giving up when it is still compiling or importing (after a package update nothing was synced until you ran Sync by hand).

## 0.5.0

- **Builds of different Loreman projects live side by side.** Each build has a catalog of its own (`Catalogs/{project}.asset`, label `loreman.catalog`) and a speakers table of its own; a sync reads, writes and removes only its own project's dialogues. Before, one catalog served the whole Unity project and syncing a second build (for example a sample's demo build) **removed the dialogues of the first**. The first sync after the update removes the old catalog and speakers table and re-syncs each build into its own (dialogue assets are adopted, nothing is lost).
- API: `DialogueService.Catalog` is now `Catalogs` (all of them) and `Dialogues` (all dialogues); `IAssetLoader.FindAddressesAsync`, `IDialogueTextSource.InitializeAsync(catalogs)` and `GetSpeakerText(projectId, key)`; the dialogue picker, the window and the auto-sync work over all catalogs.
- **Voice-overs are imported as game audio** (Vorbis 50%, mono, streamed when long): on two real 4–5 minute recordings 8.2 MB → 3.0 MB in the build and about 75 MB less RAM. Settings in the window; applied once per recording.
- Samples choose their own dialogue (the scene stores its id), so they work in a project that has other builds. Fixed the Canvas sample not compiling in projects that use the new Input System (a method named `Type` hid `System.Type`).
- Long streamed clips are played as the clip of the source (`DialoguePlayer`, Basic Dialogue), not with `PlayOneShot`.

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

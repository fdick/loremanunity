# Loreman Dialogues

Plays the dialogues of a [Loreman](https://github.com/fdick/Loreman) project in Unity 6.3+. Loreman packs a project into one `.lorebuild` file; this package
turns it into **Addressables** and **Unity Localization** tables, so a dialogue and its words are loaded only when the game plays it, in the language being played.

## Install

Package Manager → Add package from git URL (or *from disk*). It depends on `com.unity.addressables` and `com.unity.localization`, which Unity installs with it.

## Quick look (no Loreman project needed)

*Package Manager → Loreman Dialogues → Samples → import **Basic Dialogue*** → open its scene → Play. The sample brings a demo build and everything else; see [Samples](#samples).

## The Loreman window

*Tools → Loreman → Window*: the builds in the project and whether each is synced (Sync / Force sync / Show), **Add a .lorebuild file…** (copies a build into `Assets/Loreman/Builds`, synced at once), the settings of the importer with problems said as you type (output folder, Addressables group, one bundle per dialogue, creating missing locales, language mapping), each language with the Unity locale it goes to and how much of it is translated, and the dialogues of the catalog. On a build machine: `Unity -batchmode -quit -projectPath … -executeMethod Loreman.Dialogues.Editor.LoremanCli.SyncAll` (exits with 1 when a sync fails).

## Workflow

1. In Loreman, on the project list, press the hammer on the project (or right click → *Build for the game*). A `my-game.lorebuild` file is saved.
2. Put it anywhere under `Assets/` of the Unity project (or use the window above). Replacing it later is enough: **it is synced when it is imported**, and again after a recompile if the project is not at a build yet (or use *Tools → Loreman → Sync All Builds*). The first sync also makes the *Localization Settings* asset when the project has none.
3. The importer makes, in `Assets/Loreman/Generated` (settings: *Tools → Loreman → Settings*), only what changed:
   - `Catalog.asset` — the small list of dialogues, speakers and languages (Addressable `loreman/catalog`);
   - `Dialogues/{id}.asset` — one graph per dialogue (Addressable `loreman/dialogues/{id}`, a bundle of its own);
   - one Unity Localization **string table per dialogue** (`LoremanDialogue_{id}`) and one for the speakers (`LoremanSpeakers`), with the Unity locales the project lacks;
   - the **voice-overs**: the recordings are imported as `AudioClip`s under `Audio/{dialogue}/{language}/` and put in one Localization **asset table per dialogue** (`LoremanAudio_{id}`), a clip per language.
4. Build Addressables as you do for the rest of the game (*Build Addressables on Player Build* is on by default in Addressables 2.x).

Do not edit the generated assets: they are remade from the build. Commit them with the rest of the project (or generate them in CI with *Sync All Builds*).

## Play a dialogue

```csharp
using Loreman.Dialogues;

var dialogues = DialogueService.CreateDefault();   // Addressables + Unity Localization
await dialogues.InitializeAsync();                 // loads the catalog and the speakers' names, once

using var talk = await dialogues.StartAsync(dialogueId);   // loads this dialogue's graph and words
talk.Presented += view =>                                   // a line, or a choice
{
    ShowName(view.Speaker.Name, view.Speaker.Color);
    ShowText(view.Text);
    foreach (var option in view.Options) ShowButton(option.Index, option.Text);
};
talk.Refreshed += view => Redraw(view);            // the player changed the language: same line, new words
talk.Finished += Close;

talk.Start();
// later: talk.Advance();  or  talk.Choose(option.Index);
```

Voice-overs are loaded when you ask, not with the dialogue:

```csharp
talk.Presented += async view =>
{
    Show(view);
    if (!view.HasAudio) return;
    var clip = await talk.LoadAudioAsync(view);          // for an answer: talk.LoadAudioAsync(view, option.Index)
    if (clip != null) audioSource.PlayOneShot(clip);     // null: nothing recorded in this language
};
```

In the inspector, a field of type `DialogueReference` shows the dialogues of your build as a list, so you do not type ids:

```csharp
[SerializeField] private DialogueReference greeting;    // ... await dialogues.StartAsync(greeting);
```

`dialogueId` is the id of the dialogue in Loreman (`catalog.Dialogues[i].Guid`; the ids never change). Disposing the conversation lets the graph and the texts go.
`dialogues.PreloadAsync(id)` loads a dialogue ahead of time and keeps it until the handle is disposed. `view.Note` is the designer's note exactly as typed in
Loreman (conditions, effects…): the package does not read it, your game may. `view.NodeId` can be saved and passed to `talk.JumpTo(id)` to carry on later.

## Samples

*Package Manager → Loreman Dialogues → Samples → Import.* Each sample is **self-contained**: it brings a scene you open and play at once, its script, and a small demo build (`CityGate.lorebuild`, English and Russian, a few voice-overs) that is synced when it is imported.

- **Basic Dialogue** — start, advance, choose, play the voice-over, switch the language while it runs.
- **Preload and Resume** — preload a dialogue, save the place in it and resume, read the designer's note of a line.

Each has a README next to its script. Both draw with `OnGUI` so that they need no UI package; do the same calls from your own UI. (Both scenes are tested by playing them in Play Mode, from a project that has nothing but the package.)

## How it is built

| Assembly | Knows | Holds |
|---|---|---|
| `Loreman.Dialogues.Core` | no Unity at all | the binary readers, the graph model, `DialogueSession` (the state machine), `LoremanNames` |
| `Loreman.Dialogues.Runtime` | Unity, Addressables, Localization | `DialogueLibrary` (loading, reference counting), `IAssetLoader`/`IDialogueTextSource`/`IDialogueAudioSource` and their Addressables/Localization implementations, `DialogueConversation`, `DialogueService`, `DialogueReference` |
| `Loreman.Dialogues.Editor` | the editor | `.lorebuild` reader, `SyncPlan`, `BuildSynchronizer` (the rules), `UnitySyncTarget` (the writes), importer, auto-sync, `LoremanCli`, the window (`SettingsValidator`, `LanguageRows`, `BuildListing` decide; `LoremanWindow` draws) |

Everything the game touches in the outside world goes through a small interface (`IAssetLoader`, `IDialogueTextSource`, `IDialogueAudioSource`, `ISyncTarget`), so each part is tested with
a fake. The file format is described in [Documentation~/BUILD_FORMAT.md](Documentation~/BUILD_FORMAT.md). Tests: *Window → General → Test Runner → EditMode*
(they read `Tests/Fixtures/sample.lorebuild`, made by Loreman's own tests).

## Limits of the first version

- Dialogues only; items come next (the file format already leaves room for them).
- A voice-over has no fallback to another language: a line without a recording in the current language is silent (`LoadAudioAsync` gives `null`).
- Recordings go into the `.lorebuild` as they were uploaded, so a project with a lot of audio gives a big file (each recording is at most 10 MB in Loreman). Loreman streams it while building; the importer reads one recording at a time too.
- All string tables of a locale sit in the group Localization makes for it; if you want every dialogue's words in a bundle of its own, set that group's *Bundle Mode* to *Pack Separately*.

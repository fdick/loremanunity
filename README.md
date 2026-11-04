# Loreman Dialogues

Plays the dialogues of a [Loreman](https://github.com/fdick/Loreman) project in Unity 6.3+. Loreman packs a project into one `.lorebuild` file; this package
turns it into **Addressables** and **Unity Localization** tables, so a dialogue and its words are loaded only when the game plays it, in the language being played.

## Install

Package Manager → Add package from git URL (or *from disk*). It depends on `com.unity.addressables` and `com.unity.localization`, which Unity installs with it.

## Workflow

1. In Loreman, on the project list, press the hammer on the project (or right click → *Build for the game*). A `my-game.lorebuild` file is saved.
2. Put it anywhere under `Assets/` of the Unity project. Replacing it later is enough: **it is synced when it is imported** (or use *Tools → Loreman → Sync All Builds*).
3. The importer makes, in `Assets/Loreman/Generated` (settings: *Tools → Loreman → Settings*), only what changed:
   - `Catalog.asset` — the small list of dialogues, speakers and languages (Addressable `loreman/catalog`);
   - `Dialogues/{id}.asset` — one graph per dialogue (Addressable `loreman/dialogues/{id}`, a bundle of its own);
   - one Unity Localization **string table per dialogue** (`LoremanDialogue_{id}`) and one for the speakers (`LoremanSpeakers`), with the Unity locales the project lacks.
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

`dialogueId` is the id of the dialogue in Loreman (`catalog.Dialogues[i].Guid`; the ids never change). Disposing the conversation lets the graph and the texts go.
`dialogues.PreloadAsync(id)` loads a dialogue ahead of time and keeps it until the handle is disposed. `view.Note` is the designer's note exactly as typed in
Loreman (conditions, effects…): the package does not read it, your game may. `view.NodeId` can be saved and passed to `talk.JumpTo(id)` to carry on later.

## How it is built

| Assembly | Knows | Holds |
|---|---|---|
| `Loreman.Dialogues.Core` | no Unity at all | the binary readers, the graph model, `DialogueSession` (the state machine), `LoremanNames` |
| `Loreman.Dialogues.Runtime` | Unity, Addressables, Localization | `DialogueLibrary` (loading, reference counting), `IAssetLoader`/`IDialogueTextSource` and their Addressables/Localization implementations, `DialogueConversation`, `DialogueService` |
| `Loreman.Dialogues.Editor` | the editor | `.lorebuild` reader, `SyncPlan`, `BuildSynchronizer` (the rules), `UnitySyncTarget` (the writes), importer, menu |

Everything the game touches in the outside world goes through a small interface (`IAssetLoader`, `IDialogueTextSource`, `ISyncTarget`), so each part is tested with
a fake. The file format is described in [Documentation~/BUILD_FORMAT.md](Documentation~/BUILD_FORMAT.md). Tests: *Window → General → Test Runner → EditMode*
(they read `Tests/Fixtures/sample.lorebuild`, made by Loreman's own tests).

## Limits of the first version

- Dialogues only; items come next (the file format already leaves room for them). Voice-overs are not in the build yet.
- All string tables of a locale sit in the group Localization makes for it; if you want every dialogue's words in a bundle of its own, set that group's *Bundle Mode* to *Pack Separately*.

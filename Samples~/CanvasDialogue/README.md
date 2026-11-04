# Canvas Dialogue UI (uGUI)

A custom dialogue screen made with **uGUI**. **Self-contained**: it brings its scene (a Canvas with the whole screen in it), its scripts and the small demo build `CityGate.lorebuild`, so there is nothing to prepare. It needs the **Unity UI** package (`com.unity.ugui`, in new projects by default); without it the sample's assembly is simply not compiled.

1. *Package Manager → Loreman Dialogues → Samples → Canvas Dialogue UI → Import.* The demo build is synced by itself (see the Console).
2. Open the scene **CanvasDialogue** and press **Play**, then **Talk**.
3. The line is typed out: **Next** shows it whole, **Next** again goes on. At a choice the answers are buttons. The language buttons (top right) switch the Unity Localization language: the open line is shown again in the new words, without typing it again. A `♪` marks a line or an answer that has a voice-over.

How it is built:

- `DialoguePlayer` (a component of the package, on the object *Dialogue Player*) loads and plays the dialogue and the voice-over. It knows nothing about the screen.
- `CanvasDialogueScreen` is the whole screen logic, about 150 lines: it listens to `player.Presented` / `Refreshed` / `Finished`, fills the labels, makes a button per answer from a template, and calls `player.Advance()` / `player.Choose(i)`. Everything you see is in the scene, so restyle it there.
- It makes an `EventSystem` if the scene has none, with the input module that fits your project (the new Input System or the old Input Manager).
- The labels are the legacy `Text`, so the sample needs nothing but uGUI. With TextMeshPro change the fields to `TMP_Text`; the rest stays.

To use your own dialogue: put your `.lorebuild` into `Assets` and pick the dialogue on the *Dialogue Player* (empty = the first dialogue of the catalog).

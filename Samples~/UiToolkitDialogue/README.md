# UI Toolkit Dialogue UI

A custom dialogue screen made with **UI Toolkit**. **Self-contained**: it brings its scene, the UXML and USS of the screen, a Panel Settings asset, its script and the small demo build `CityGate.lorebuild`, so there is nothing to prepare.

1. *Package Manager → Loreman → Samples → UI Toolkit Dialogue UI → Import.* The demo build is synced by itself (see the Console).
2. Open the scene **UiToolkitDialogue** and press **Play**, then **Talk**.
3. The line is typed out: **Next** shows it whole, **Next** again goes on. At a choice the answers are buttons. The language buttons (top right) switch the Unity Localization language: the open line is shown again in the new words. A `♪` marks a line or an answer that has a voice-over.

How it is built:

- `DialoguePlayer` (a component of the package, on the object *Dialogue Player*) loads and plays the dialogue and the voice-over. It knows nothing about the screen.
- `DialogueScreen.uxml` is the structure (panel, speaker, line, choices, Next, Talk, languages, status) and `DialogueScreen.uss` the look. **Restyle in the USS**; if you move elements in the UXML keep their names, the script finds them by name.
- `UiToolkitDialogueScreen` fills the elements: it listens to `player.Presented` / `Refreshed` / `Finished`, makes a `Button` per answer, types the line out with the element scheduler, and calls `player.Advance()` / `player.Choose(i)`.
- `DialoguePanelSettings` scales the screen with the window (reference 1920×1080) and uses the theme `DialogueTheme.tss` of this folder (it imports Unity's default runtime theme; without a theme UI Toolkit draws no text).
- The script makes an `EventSystem` with the Input System's UI module if the scene has none, so clicks work in projects that use only the new Input System.

To use your own dialogue: put your `.lorebuild` into `Assets` and pick the dialogue on the *Dialogue Player* (the sample plays its own *City Gate*, whatever other builds the project has; clear the field to play the first dialogue of the project).

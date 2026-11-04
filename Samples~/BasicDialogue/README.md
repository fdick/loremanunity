# Basic Dialogue

The smallest complete use of the package. **It brings everything it needs**: a scene, a script and a small demo build (`CityGate.lorebuild`, a guard at a city gate, in English and Russian, with a few voice-overs), so there is nothing to prepare.

1. *Package Manager → Loreman Dialogues → Samples → Basic Dialogue → Import.*
2. Unity imports `CityGate.lorebuild` and syncs it by itself (the Console says *Loreman: dialogues synced*; the first time it also makes the Localization Settings and the `en`/`ru` locales). If it does not, use *Tools → Loreman → Window → Sync*.
3. Open the scene **BasicDialogue** and press **Play**, then **Talk**. Click the language buttons while the guard is talking: the same line is shown again in the other language, and `♪` marks a line with a voice-over (in Russian the second recording is missing on purpose: that line is silent).

To use your own dialogue instead, put your `.lorebuild` into `Assets` and pick the dialogue in the **Dialogue** field of the component (empty = the first dialogue of the catalog).

What the script shows (the numbered comments in `BasicDialogueExample.cs`):

1. one `DialogueService` for the game, `InitializeAsync` once;
2. `StartAsync` loads one dialogue (its graph and its words in the current language);
3. `LoadAudioAsync` loads a voice-over when the line appears; it can be `null` in a language that has no recording;
4. `Advance` after a line, `Choose` after a choice;
5. the language buttons set `LocalizationSettings.SelectedLocale`; the open dialogue raises `Refreshed` and says the same line in the new language.

The screen is drawn with `OnGUI` only so that the sample needs no UI package. Do the same calls from your own UI.

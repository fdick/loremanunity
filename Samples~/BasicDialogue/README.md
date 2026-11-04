# Basic Dialogue

The smallest complete use of the package.

1. Import a `.lorebuild` made by Loreman into `Assets/` (see the package README). Wait for the sync to finish.
2. Make an empty GameObject in a scene and add **BasicDialogueExample**.
3. Pick the dialogue in the **Dialogue** field (a list of the dialogues of your build, grouped like in Loreman).
4. Press Play, then **Talk**.

What it shows (the numbered comments in `BasicDialogueExample.cs`):

1. one `DialogueService` for the game, `InitializeAsync` once;
2. `StartAsync` loads one dialogue (its graph and its words in the current language);
3. `LoadAudioAsync` loads a voice-over when the line appears, and it can be `null` in a language that has no recording;
4. `Advance` after a line, `Choose` after a choice;
5. the language buttons set `LocalizationSettings.SelectedLocale`; the open dialogue raises `Refreshed` and says the same line in the new language.

The screen is drawn with `OnGUI` only so that the sample needs no UI package. Do the same calls from your own UI.

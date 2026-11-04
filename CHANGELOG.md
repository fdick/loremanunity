# Changelog

## 0.2.0

- Voice-overs: imported as audio clips into one Localization asset table per dialogue (a clip per language); `DialogueView.HasAudio`, `DialogueConversation.LoadAudioAsync`, `IDialogueAudioSource` / `LocalizationAudioSource`. Needs a build of format version 2 (graph version 1 files are still read).
- `DialogueReference`: pick a dialogue in the inspector from a list.
- Samples: Basic Dialogue, Preload and Resume.

## 0.1.0

- `.lorebuild` importer: Addressables graphs, one Unity Localization table per dialogue, speakers table, locales, incremental sync.
- Runtime: `DialogueService`, `DialogueConversation`, `DialogueLibrary` (on-demand loading, reference counting), `LocalizationTextSource` (current language with fallback to the base language, live language switch).

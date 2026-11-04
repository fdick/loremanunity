# Changelog

## 0.1.0

- `.lorebuild` importer: Addressables graphs, one Unity Localization table per dialogue, speakers table, locales, incremental sync.
- Runtime: `DialogueService`, `DialogueConversation`, `DialogueLibrary` (on-demand loading, reference counting), `LocalizationTextSource` (current language with fallback to the base language, live language switch).

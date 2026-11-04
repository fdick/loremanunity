# The `.lorebuild` file

What a game needs from a Loreman project, packed once by the **Build** button (project list → the hammer on a project card, or right click →
“Build for the game”). The Unity package `com.loreman.dialogues` reads it. Server code: `src/Services/Maps/Application/Build/`, endpoint
`GET /api/build?projectId=…&name=…` (and `/api/build/manifest` for the manifest alone).

Iteration 1 holds **dialogues** (graphs, texts in every language, speakers). Items will join the same file as another part, without changing what is here.

## Principles

- **A ZIP of small parts**, so a person can open it and look. The parts the game reads are binary and compact.
- **Texts are apart from graphs.** A graph holds only the *numbers* of its texts; the words are in string tables, one per dialogue and language.
  That is what lets the game load a dialogue and its words only when it needs them, and only in the language being played.
- **Deterministic.** The same project gives the same bytes (entries are sorted, the ZIP time is fixed). `contentHash` and every dialogue's `hash`
  tell the Unity importer what changed, so it remakes only that.
- **Only what the game can show.** A line that cannot be reached from Start is left out, with its texts. Notes are kept as they were typed. Positions on the canvas,
  voice-overs and photos are not in the file (voice-overs come later with items).

## Layout

```
manifest.json                      what is inside (JSON, read first)
dialogues/{id}.bin                 the graph of one dialogue (id: 32 hex digits, no dashes)
texts/{language}/{id}.bin          the texts of one dialogue in one language (only when it has any)
texts/{language}/speakers.bin      the names and descriptions of the speakers in one language
```

### manifest.json

camelCase, indented.

| field | meaning |
|---|---|
| `format`, `formatVersion` | `"loreman.build"`, `1`. A reader refuses a newer `formatVersion`; adding a part does not raise it. |
| `projectId`, `projectName`, `builtAt` | where it came from; `builtAt` is **not** part of the hash |
| `contentHash` | 32 hex digits: the SHA-256 (cut) of the manifest without time and hash, then every part in path order (path, bytes) |
| `baseLanguage`, `languages[]` | `{code, total, translated, stale}` for the base language and every target language. `stale` = translated from an original that has changed since |
| `speakers[]` | `{id, color, nameKey, noteKey}`; the speakers who talk in the build, sorted by id. Nodes refer to them by index. `noteKey` 0 = no description |
| `groups[]` | `{id, name}` of the dialogue groups |
| `dialogues[]` | `{id, name, groupId?, graph, hash, nodes, texts}`; `hash` covers the graph and all its tables |

Language codes are Loreman's (`en`, `pt-br`); the Unity importer turns them into locale codes (`pt-BR`).

### Text keys

Every text has a Loreman key (`node:{guid}`, `option:{guid}`, `link:{guid}`, `speaker:{guid}:name`, `speaker:{guid}:note`; guids lower case with dashes) and a
**number**: `KeyId(key)` = the first 8 bytes of SHA-256(UTF-8 key) as a little-endian `int64`, with the sign bit cleared, and 1 instead of 0.
It never changes, so tables keep their ids between builds. `0` in a graph means “no text”.

### `dialogues/{id}.bin`

Little-endian. `varint` = 7 bits per byte, high bit = another byte follows. A *ref* is an index plus one (0 = nothing). `guid` = 16 bytes
(.NET `Guid.ToByteArray()`). `string` = varint length + UTF-8.

```
"LDLG" · u8 version=1 · guid dialogueId · varint entry(ref) · varint nodeCount · nodeCount × node

node  = u8 kind (0 line, 1 choice) · guid id · varint speaker(ref into manifest.speakers) · i64 text · string note · then
  line   : varint next(ref) · i64 nextLabel
  choice : varint answerSpeaker(ref) · varint optionCount · optionCount × ( guid id · i64 text · varint next(ref) · i64 label )
```

Nodes are in reading order (breadth first from Start); `entry` is where Start leads. A line has one way on; every answer of a choice has its own.
`nextLabel` / `label` are the words on the arrow, if any.

### `texts/…/*.bin` (string tables)

```
"LSTR" · u8 version=1 · varint count · count × ( i64 id · string key · string text )
```

Sorted by key. A target language holds only the texts that are translated; the game falls back to the base language for the rest.

## Compatibility test

`LoreBuildTests.The_sample_build_for_the_unity_package_can_be_written` writes `sample.lorebuild` when `LOREMAN_FIXTURE_DIR` is set. The package keeps a copy in
`Tests/Fixtures/`; its tests read it with the package's own readers. When the format changes, regenerate the file and copy it, so both sides are tested
against the same bytes:

```bash
LOREMAN_FIXTURE_DIR=/path/to/fixture dotnet run --project tests/Loreman.Tests -- --treenode-filter "/*/*/LoreBuildTests/*"
```

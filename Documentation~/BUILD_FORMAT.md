# The `.lorebuild` file

What a game needs from a Loreman project, packed once by the **Build** button (project list → the hammer on a project card, or right click →
“Build for the game”). The Unity package `com.loreman.unity` reads it. Server code: `src/Services/Maps/Application/Build/`, endpoint
`GET /api/build?projectId=…&name=…` (and `/api/build/manifest` for the manifest alone).

The file holds **dialogues** (graphs, texts in every language, speakers, **voice-overs**) and the **items** of the project (names, descriptions, numbers, text parameters, pictures, folders).

## Principles

- **A ZIP of small parts**, so a person can open it and look. The parts the game reads are binary and compact.
- **Texts are apart from graphs.** A graph holds only the *numbers* of its texts; the words are in string tables, one per dialogue and language.
  That is what lets the game load a dialogue and its words only when it needs them, and only in the language being played.
- **Deterministic.** The same project gives the same bytes (entries are sorted, the ZIP time is fixed). `contentHash` and every dialogue's `hash`
  tell the Unity importer what changed, so it remakes only that.
- **Only what the game can show.** A line that cannot be reached from Start is left out, with its texts and voice-over. Notes are kept as they were typed. Positions on the canvas
  and photos of speakers are not in the file.

## Layout

```
manifest.json                      what is inside (JSON, read first)
dialogues/{id}.bin                 the graph of one dialogue (id: 32 hex digits, no dashes)
texts/{language}/{id}.bin          the texts of one dialogue in one language (only when it has any)
texts/{language}/speakers.bin      the names and descriptions of the speakers in one language
items/items.bin                    the catalog of the items: folders, items, number parameters (only when the project has items)
items/icons/{hash}.{png|jpg}       the pictures of the items (identical pictures are one file)
texts/{language}/items.bin         the names, descriptions and text parameters of all the items in one language
audio/{language}/{dialogueId}/{id}.{ext}   the voice-over of a line or an answer in one language (id: 32 hex digits; ext: mp3, wav, ogg, m4a)
```

### manifest.json

camelCase, indented.

| field | meaning |
|---|---|
| `format`, `formatVersion` | `"loreman.build"`, `2` (version 2 added voice-overs and the `audio` fields of the graph). A reader refuses a newer `formatVersion`; adding a part does not raise it. |
| `projectId`, `projectName`, `builtAt` | where it came from; `builtAt` is **not** part of the hash |
| `contentHash` | 32 hex digits: the SHA-256 (cut) of the manifest without time and hash, then every part in path order (path, bytes; for a voice-over: path, size, upload time) |
| `baseLanguage`, `languages[]` | `{code, total, translated, stale}` for the base language and every target language. `stale` = translated from an original that has changed since |
| `speakers[]` | `{id, color, nameKey, noteKey}`; the speakers who talk in the build, sorted by id. Nodes refer to them by index. `noteKey` 0 = no description |
| `groups[]` | `{id, name}` of the dialogue groups |
| `items` | absent when the project has no items (or the build leaves them out). `{catalog, hash, count, folders, texts, icons[], skippedIcons}`: `hash` covers the catalog, all the item texts and all the pictures; `icons[]` the paths of the pictures; `skippedIcons` pictures that are not in the file because Unity cannot import their format (webp; the web client only makes png and jpeg) |
| `dialogues[]` | `{id, name, groupId?, graph, hash, nodes, texts, audio[]}`; `hash` covers the graph, all its tables and all its voice-overs (a recording by its path, size and the time it was uploaded: the file is streamed, so the bytes are not read for the hash). `audio[]` is `{id, key, language, path}`: `id` the line or answer, `key` the key id of its voice-over (`audio:{guid}`, the number the graph holds in its `audio` field) |

Language codes are Loreman's (`en`, `pt-br`); the Unity importer turns them into locale codes (`pt-BR`).

### Text keys

Every text has a Loreman key (`node:{guid}`, `option:{guid}`, `link:{guid}`, `speaker:{guid}:name`, `speaker:{guid}:note`; guids lower case with dashes) and a
**number**: `KeyId(key)` = the first 8 bytes of SHA-256(UTF-8 key) as a little-endian `int64`, with the sign bit cleared, and 1 instead of 0.
It never changes, so tables keep their ids between builds. `0` in a graph means “no text”.

### `dialogues/{id}.bin`

Little-endian. `varint` = 7 bits per byte, high bit = another byte follows. A *ref* is an index plus one (0 = nothing). `guid` = 16 bytes
(.NET `Guid.ToByteArray()`). `string` = varint length + UTF-8.

```
"LDLG" · u8 version=2 · guid dialogueId · varint entry(ref) · varint nodeCount · nodeCount × node

node  = u8 kind (0 line, 1 choice) · guid id · varint speaker(ref into manifest.speakers) · i64 text · i64 audio · string note · then
  line   : varint next(ref) · i64 nextLabel
  choice : varint answerSpeaker(ref) · varint optionCount · optionCount × ( guid id · i64 text · i64 audio · varint next(ref) · i64 label )
```

Nodes are in reading order (breadth first from Start); `entry` is where Start leads. A line has one way on; every answer of a choice has its own.
`nextLabel` / `label` are the words on the arrow, if any. `audio` is the key id of the voice-over (0: none in any language); a reader of version 1 files treats it as 0.

### Voice-overs

A voice-over belongs to a line or to an answer and to a language; the one recorded for the language of the project is written under that language's code. A recording is put into
the file **as it was uploaded** (stored, not compressed again), except when the build asks to compress: then a **wav** recording is made into **Ogg Vorbis** (quality 3, channels and
sample rate as they were) and its path ends in `.ogg` (mp3, ogg and m4a are never re-encoded). A build can also leave the voice-overs out, and take only some of the project's languages (the language of the
project is always in). A recording is left out if its line is not reachable, its language is not one of the project's, or its format is
not mp3, wav, ogg or m4a. Two recordings of the same thing in the same language: the last one counts. The game gets a voice-over only in its own language: there is no fallback
to the base language (a translated line read in the original voice is wrong more often than it helps).

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


### Items

The items are small, and a game wants the name and picture of every one of them at hand (an inventory lists them), so unlike the dialogues they are **not split up**: one catalog, one table of
texts per language, and the pictures. Each item is the `Item` of the Items tool: a name, a description, a picture, **number parameters** and **text parameters**, in a folder or at the top level.

**`items/items.bin`** (little-endian, `varint` and `string` as in the graph, a *ref* is an index plus one, 0 = nothing):

```
"LITM" · u8 version=1
varint statCount   · statCount × ( string name · string unit )       the number parameters in use, each once (“Damage” “pt”); an item refers to one by its index
varint nameCount   · nameCount × string                              the names of the text parameters in use, each once (“Lore”)
varint folderCount · folderCount × ( guid id · varint parent(ref) · string name )     a parent comes before its children
varint iconCount   · iconCount × string path                         the pictures
varint itemCount   · itemCount × item

item = guid id · varint folder(ref) · i64 name · i64 description · varint icon(ref) · varint statCount · statCount × ( varint stat · number )
       · varint fieldCount · fieldCount × ( varint nameIndex · i64 text )
number = u8 0 · zigzag varint        a whole number: 15 is one byte, −30 is one byte
       | u8 1 · f64                  anything else
```

Items are sorted by id and folders by tree and name, so the same project gives the same bytes. `i64 name` and `i64 text` are key ids of `texts/{language}/items.bin`
(`item:{guid}:name`, `item:{guid}:description`, `item:{guid}:field:{name}`; `description` is 0 when the item has none, and a text parameter with no text is left out). The table of a
language other than the base holds only what is translated: the game falls back to the language of the project, as it does for dialogues.

A **number parameter** is one of the Progression tool, in the group named like the item (“Sword: Damage”). In the file it is its **name and unit** (“Damage”, “pt”), so the sword's and
the potion's Damage are one entry of `stat`. Names are not translated. The unit and the name are what a game shows next to the number; the name is also how code asks for it.

**Pictures** are the files the web client made (png, or jpeg when a png was too heavy; at most 192 px on the longest side). They are stored as they are in `items/icons/`, under a code of
their bytes, so two items with the same picture take one file. A webp picture (only an import can make one) is left out and counted in `skippedIcons`.

Size: 2000 items with three small numbers each are about 50 bytes apiece in the catalog; the pictures are the bulk, and the file keeps them once.

# Preload and Resume

What a game does with dialogues besides playing them once. **Self-contained**: it brings its scene, its script and the same small demo build as *Basic Dialogue* (`CityGate.lorebuild`; if you import both samples, the second one finds the dialogues already synced).

1. *Package Manager → Loreman → Samples → Preload and Resume → Import.* The demo build is synced by itself (see the Console).
2. Open the scene **PreloadAndResume** and press **Play**.
3. **Preload** loads the graph and the words ahead of time (`DialogueService.PreloadAsync`); the dialogue stays in memory until you press **Unload** (the handle is disposed). The status line counts the dialogues in memory.
4. **Talk from the start**, answer a few lines, press **Leave**: the id of the node (`DialogueView.NodeId`) was saved. **Resume from the saved place** carries on there with `DialogueConversation.JumpTo`.
5. The designer's note of a line (`DialogueView.Note`: *mood: stern*, *effect: give_pass*) is shown under the line and written to the Console. The package does not interpret it; your game may (conditions, effects).

Node ids come from Loreman and do not change between builds, so they are safe in a save file. If a later build removes the node, `JumpTo` returns `false` and you start from the beginning.

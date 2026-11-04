# Preload and Resume

What a game does with dialogues besides playing them once.

1. Add **PreloadAndResumeExample** to an empty GameObject, pick a dialogue, press Play.
2. **Preload** loads the graph and the words ahead of time (`DialogueService.PreloadAsync`); the dialogue stays in memory until you press **Unload** (the handle is disposed). Watch the number of dialogues in memory in the status line.
3. **Talk from the start**, advance a few lines, press **Leave**: the id of the node (`DialogueView.NodeId`) was saved. **Resume from the saved place** carries on there with `DialogueConversation.JumpTo`.
4. The designer's note of a line (`DialogueView.Note`) is written to the Console as it appears. The package does not interpret it; your game may (conditions, effects).

Node ids come from Loreman and do not change between builds, so they are safe in a save file. If a later build removes the node, `JumpTo` returns `false` and you start from the beginning.

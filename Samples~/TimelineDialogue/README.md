# Timeline Dialogue

A dialogue as a **clip on a Unity Timeline**. **Self-contained**: it brings its scene, the timeline asset, three small animation clips, a script that draws the dialogue and the demo build `CityGate.lorebuild`, so there is nothing to prepare. It needs the **Timeline** package (`com.unity.timeline` 1.6 or newer); Loreman's Timeline support exists only where that package is installed.

1. *Package Manager → Loreman → Samples → Timeline Dialogue → Import.* The demo build is synced by itself (see the Console).
2. Open the scene **TimelineDialogue** and press **Play**.
3. The hero walks to the gate. When the timeline reaches the **dialogue clip** (at 3 s) the walk stops and the guard talks: **Next** goes on, at a choice the answers are buttons, the language buttons (top right) change the language. The bar at the top says **waiting for the dialogue to end** while the timeline is held. When the dialogue is over the timeline goes on: the gate opens and the hero walks through. **Play again** restarts it.

Open *Window → Sequencing → Timeline* with the object **Timeline Director** selected to see how it is made:

- the **Dialogue** track (*Loreman/Dialogue Track*) is bound to the object **Dialogue Player** (a `DialoguePlayer`, the same component the other samples use);
- its clip is a **Dialogue Clip**: click it, and in the inspector **Dialogue** is the searchable list of the dialogues of your builds (the clip shows the dialogue's name), **Ending** is *Wait For Dialogue* (the timeline stops where the clip starts until the dialogue is over; the length of the clip does not matter) or *Stop At End Of Clip* (the timeline goes on and the dialogue is stopped when the clip ends), **Auto Advance** makes lines go on by themselves (a cutscene) instead of waiting for Next, and then the clip is made as long as the dialogue (try *After Seconds*: the clip stretches, and **Fit Clip To Dialogue** in its inspector sets the length again);
- the **Hero** and **Gate** tracks are ordinary animation tracks (they move the child objects *Body* and *Door*).

`TimelineDialogueScreen` only listens to the player's events and draws them with `OnGUI` (so the sample needs no UI package). It does not know about the timeline: the clip talks to the player by itself, exactly as `player.Play()` from your own code would. To use your own dialogue, put your `.lorebuild` into `Assets` and pick the dialogue in the clip.

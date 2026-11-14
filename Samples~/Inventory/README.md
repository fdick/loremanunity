# Inventory

A ready inventory with the items of a Loreman project. **Self-contained**: it brings its scene, a screen made with uGUI, and the demo build `CityGate.lorebuild` (the same one the other samples use), which has seven items with pictures: a coin, bread, a potion, a sword, a bow, a letter and a key, in English and Russian. Needs the Unity UI package (`com.unity.ugui`).

1. *Package Manager → Loreman → Samples → Inventory → Import.* The demo build is synced by itself (see the Console); the items are in it.
2. Open the scene **Inventory** and press **Play**.
3. Click an item to see what it is (name, folder, description, numbers, text parameters). Click another slot to **move** it there; the same kind of item merges up to its stack limit. **Add random** puts a few random items in, **Remove 1** takes one from the selected slot, **Sort** merges the stacks and orders them by name, **Save** / **Load** keep the inventory in `PlayerPrefs`. The language buttons (top right) change the language of every name and text.

How it is built:

- The object **Player** has an `InventoryComponent`: the number of slots and what it starts with are set in the inspector (*Starting Items* is a list of `ItemReference`: the picker has a search box and shows the pictures). It gets the items from the shared `ItemDatabase` (`LoremanItems`) by itself.
- `InventorySampleScreen` only shows the inventory: it listens to `inventory.Changed`, draws the slots from `inventory.Inventory[i]`, loads the pictures with `await item.LoadIconAsync()`, and calls `inventory.Add(...)`, `inventory.Inventory.Move(from, to)`, `.RemoveAt(slot)`, `.Sort(...)`, `inventory.SaveToJson()` / `LoadFromJson(...)`.
- **How many fit in a slot** is a number parameter of the item named **Max Stack** (also *Stack*, *Stack Size*); an item without it does not stack. Give your items one in Loreman (the bread has 10, the coin 999, the sword none).

To use your own items: put your `.lorebuild` (with the items included in the build settings) into `Assets` and set the *Starting Items* of the player. Restyle the screen in the scene; the slots are copies of the inactive *Slot Template*.

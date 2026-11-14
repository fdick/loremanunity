using System;
using System.Collections.Generic;
using System.Text;
using Loreman.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Loreman.Dialogues.Samples.ItemsExample
{
    /// <summary>
    /// An inventory screen made with uGUI: a grid of slots with the pictures and counts of the items, a panel with what the selected item is, and a few buttons that use the inventory.
    /// The inventory itself is an <see cref="InventoryComponent"/> (look at the object "Player"); this script only shows it and calls it:
    /// <list type="bullet">
    /// <item><c>inventory.Changed</c> → draw the slots again. <c>inventory.Add(item, n)</c>, <c>inventory.Inventory.RemoveAt(slot)</c>, <c>.Move(from, to)</c>, <c>.Sort()</c>.</item>
    /// <item><c>item.Name</c>, <c>item.Description</c>, <c>item.Stats</c>, <c>item.GetField("Lore")</c> and <c>await item.LoadIconAsync()</c> are what an item is.</item>
    /// <item><c>inventory.SaveToJson()</c> / <c>LoadFromJson()</c> keep it between runs (here in PlayerPrefs).</item>
    /// </list>
    /// Click an item to see it; click another slot to move it there (items of the same kind merge). Everything on the screen is in the scene, so restyle it there.
    /// </summary>
    public sealed class InventorySampleScreen : MonoBehaviour
    {
        private const string SaveKey = "Loreman.Sample.Inventory";

        [SerializeField] private InventoryComponent inventory;

        [Header("Slots")]
        [SerializeField] private RectTransform gridRoot;
        [Tooltip("An inactive button with two children: an Image called Icon and a Text called Count. One copy is made for every slot.")]
        [SerializeField] private Button slotTemplate;
        [SerializeField] private Color slotColor = new Color(1f, 1f, 1f, 0.10f);
        [SerializeField] private Color selectedColor = new Color(0.95f, 0.75f, 0.2f, 0.55f);

        [Header("Details and actions")]
        [SerializeField] private Text detailsLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button addButton;
        [SerializeField] private Button removeButton;
        [SerializeField] private Button sortButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;

        [Header("Language")]
        [SerializeField] private RectTransform languagesRoot;
        [Tooltip("An inactive button; one copy is made for every language of the project.")]
        [SerializeField] private Button languageTemplate;

        private sealed class SlotView
        {
            public Button Button;
            public Image Background;
            public Image Icon;
            public Text Count;
            public ItemDefinition Showing;
        }

        private readonly List<SlotView> _slots = new List<SlotView>();
        private ItemDatabase _items;
        private int _selected = -1;
        private bool _built;

        private void Awake()
        {
            EnsureEventSystem();
            slotTemplate.gameObject.SetActive(false);
            languageTemplate.gameObject.SetActive(false);
            detailsLabel.text = string.Empty;
        }

        private void OnEnable()
        {
            inventory.Ready += OnReady;
            inventory.Changed += OnChanged;
            addButton.onClick.AddListener(AddRandom);
            removeButton.onClick.AddListener(RemoveSelected);
            sortButton.onClick.AddListener(SortByName);
            saveButton.onClick.AddListener(Save);
            loadButton.onClick.AddListener(Load);
            if (inventory.IsReady) OnReady();
            else statusLabel.text = "Loading the items…";
        }

        private void OnDisable()
        {
            inventory.Ready -= OnReady;
            inventory.Changed -= OnChanged;
            if (_items != null) _items.TextsChanged -= Redraw;
            addButton.onClick.RemoveListener(AddRandom);
            removeButton.onClick.RemoveListener(RemoveSelected);
            sortButton.onClick.RemoveListener(SortByName);
            saveButton.onClick.RemoveListener(Save);
            loadButton.onClick.RemoveListener(Load);
        }

        /// <summary>uGUI needs an EventSystem to hear clicks. One is made if the scene has none, with the module that fits the project's input.</summary>
        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            // The Input System's own module, found by name so that the sample compiles without a reference to that package.
            var module = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (module != null)
            {
                go.AddComponent(module);
                return;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<StandaloneInputModule>();
#else
            Debug.LogWarning("Loreman sample: the project uses the Input System only, but the package com.unity.inputsystem was not found: the buttons will not react.");
#endif
        }

        // ── the inventory is ready ──

        private void OnReady()
        {
            _items = inventory.Database;
            _items.TextsChanged -= Redraw;
            _items.TextsChanged += Redraw; // the player changed the language: every name is in the new one
            if (!_built) BuildSlots();
            BuildLanguageButtons();
            statusLabel.text = _items.Count + " items in the project. Click an item, then another slot to move it.";
            Redraw();
        }

        private void BuildSlots()
        {
            _built = true;
            for (var i = 0; i < inventory.SlotCount; i++)
            {
                var button = Instantiate(slotTemplate, gridRoot);
                button.gameObject.SetActive(true);
                var view = new SlotView
                {
                    Button = button,
                    Background = button.GetComponent<Image>(),
                    Icon = button.transform.Find("Icon").GetComponent<Image>(),
                    Count = button.transform.Find("Count").GetComponent<Text>(),
                };
                var slot = i;
                button.onClick.AddListener(() => OnSlotClicked(slot));
                _slots.Add(view);
            }
        }

        private void BuildLanguageButtons()
        {
            if (languagesRoot.childCount > 1) return;
            foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
            {
                var button = Instantiate(languageTemplate, languagesRoot);
                button.gameObject.SetActive(true);
                button.GetComponentInChildren<Text>().text = locale.Identifier.Code.ToUpperInvariant();
                var chosen = locale;
                button.onClick.AddListener(() => LocalizationSettings.SelectedLocale = chosen);
            }
        }

        // ── drawing ──

        private void OnChanged(InventoryChange change) => Redraw();

        private void Redraw()
        {
            if (!inventory.IsReady || !_built) return;
            for (var i = 0; i < _slots.Count; i++)
            {
                var view = _slots[i];
                var stack = inventory.Inventory[i];
                view.Background.color = i == _selected ? selectedColor : slotColor;
                if (stack.IsEmpty)
                {
                    view.Showing = null;
                    view.Icon.enabled = false;
                    view.Count.text = string.Empty;
                    continue;
                }
                var item = _items.Get(stack.ItemId);
                view.Count.text = stack.Count > 1 ? stack.Count.ToString() : string.Empty;
                if (!ReferenceEquals(view.Showing, item))
                {
                    view.Showing = item;
                    ShowIcon(view, item);
                }
            }
            ShowDetails();
        }

        // The picture is an Addressable that is loaded when it is first needed (and kept by the database after that).
        private async void ShowIcon(SlotView view, ItemDefinition item)
        {
            view.Icon.enabled = false;
            try
            {
                var sprite = await item.LoadIconAsync();
                if (this == null || !ReferenceEquals(view.Showing, item)) return; // the slot shows something else by now
                view.Icon.sprite = sprite;
                view.Icon.enabled = sprite != null;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void ShowDetails()
        {
            if (_selected < 0 || inventory.Inventory[_selected].IsEmpty)
            {
                detailsLabel.text = "<i>Nothing selected.</i>";
                return;
            }
            var stack = inventory.Inventory[_selected];
            var item = _items.Get(stack.ItemId);
            var text = new StringBuilder();
            text.Append("<size=40><b>").Append(item.Name).Append("</b></size>\n");
            if (item.Folder != null) text.Append("<color=#9aa>").Append(item.Folder.Path).Append("</color>\n");
            if (item.Description.Length > 0) text.Append('\n').Append(item.Description).Append('\n');
            if (item.Stats.Count > 0) text.Append('\n');
            foreach (var stat in item.Stats)
            {
                if (ItemRules.Normalize(stat.Name) == "maxstack") continue; // what the inventory does with it is shown below
                text.Append(stat.Name).Append(": <b>").Append(stat).Append("</b>\n");
            }
            foreach (var field in item.Fields)
                if (field.Text.Length > 0) text.Append("\n<i>").Append(field.Name).Append("</i>\n").Append(field.Text).Append('\n');
            text.Append("\n<color=#9aa>").Append(stack.Count).Append(" of ").Append(item.MaxStack).Append(" in this slot</color>");
            detailsLabel.text = text.ToString();
        }

        // ── clicks ──

        private void OnSlotClicked(int slot)
        {
            if (_selected < 0)
            {
                if (!inventory.Inventory[slot].IsEmpty) _selected = slot;
            }
            else if (_selected == slot)
            {
                _selected = -1;
            }
            else
            {
                // Move puts the stack onto the other one when it is the same item (up to its limit), and swaps otherwise.
                inventory.Inventory.Move(_selected, slot);
                _selected = -1;
            }
            Redraw();
        }

        private void AddRandom()
        {
            var all = _items.All;
            if (all.Count == 0) return;
            var item = all[UnityEngine.Random.Range(0, all.Count)];
            var count = UnityEngine.Random.Range(1, 4);
            var left = inventory.Add(item, count); // what did not fit comes back
            statusLabel.text = left == 0 ? "Added " + count + " × " + item.Name : left == count ? "No room for " + item.Name + "." : "Added " + (count - left) + " × " + item.Name + "; the rest did not fit.";
        }

        private void RemoveSelected()
        {
            if (_selected < 0 || inventory.Inventory[_selected].IsEmpty) return;
            var taken = inventory.Inventory.RemoveAt(_selected, 1);
            statusLabel.text = "Removed 1 × " + _items.Get(taken.ItemId).Name;
        }

        private void SortByName()
        {
            _selected = -1;
            inventory.Inventory.Sort(new ByName(_items)); // merges the stacks and puts them in the order the comparer gives
            statusLabel.text = "Sorted by name.";
        }

        private void Save()
        {
            PlayerPrefs.SetString(SaveKey, inventory.SaveToJson());
            PlayerPrefs.Save();
            statusLabel.text = "Saved.";
        }

        private void Load()
        {
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                statusLabel.text = "Nothing saved yet: press Save first.";
                return;
            }
            _selected = -1;
            inventory.LoadFromJson(PlayerPrefs.GetString(SaveKey));
            statusLabel.text = "Loaded.";
        }

        private sealed class ByName : IComparer<Guid>
        {
            private readonly ItemDatabase _items;

            public ByName(ItemDatabase items)
            {
                _items = items;
            }

            public int Compare(Guid a, Guid b) => string.Compare(_items.Get(a).Name, _items.Get(b).Name, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}

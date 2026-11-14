using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Loreman.Dialogues.Editor
{
    /// <summary>
    /// The list that opens under an <see cref="Loreman.Items.ItemReference"/> field: a search box that has the focus at once, and the items below it, each with its picture.
    /// Type to narrow the list (every word must be in the name, the folder or the description), ↑ ↓ to move, Enter or a click to pick, Esc to close.
    /// What is listed and in which order is decided by <see cref="ItemSearch"/>; this class only draws it.
    /// </summary>
    public sealed class ItemPickerWindow : EditorWindow
    {
        private const float RowHeight = 28f;

        private IReadOnlyList<ItemEntry> _all;
        private List<ItemEntry> _shown = new List<ItemEntry>();
        private string _currentId;
        private Action<string> _picked;
        private ListView _list;
        private Label _count;
        private ToolbarSearchField _search;
        private bool _isOpen;

        /// <summary>Opens the list under <paramref name="fieldRect"/> (in the coordinates of the GUI that draws the field).</summary>
        public static void Show(Rect fieldRect, IReadOnlyList<ItemEntry> entries, string currentId, Action<string> picked)
        {
            var window = Create(entries, currentId, picked);
            window._isOpen = true;
            var screen = GUIUtility.GUIToScreenRect(fieldRect);
            window.ShowAsDropDown(screen, new Vector2(Mathf.Max(screen.width, 380f), 400f));
        }

        internal static ItemPickerWindow Create(IReadOnlyList<ItemEntry> entries, string currentId, Action<string> picked)
        {
            var window = CreateInstance<ItemPickerWindow>();
            window._all = entries;
            window._currentId = currentId;
            window._picked = picked;
            window.titleContent = new GUIContent("Item");
            return window;
        }

        internal IReadOnlyList<ItemEntry> Shown => _shown;

        public void CreateGUI()
        {
            if (_all == null) { CloseWindow(); return; } // reopened after a recompile: there is nothing to pick from any more
            var root = rootVisualElement;
            root.style.paddingLeft = root.style.paddingRight = 4;
            root.style.paddingTop = root.style.paddingBottom = 4;

            _search = new ToolbarSearchField { style = { width = Length.Auto(), marginBottom = 4 } };
            _search.RegisterValueChangedCallback(e => Refilter(e.newValue));
            _search.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            root.Add(_search);

            _list = new ListView(_shown, RowHeight, MakeRow, BindRow)
            {
                selectionType = SelectionType.Single,
                showBorder = true,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
            };
            _list.style.flexGrow = 1;
            root.Add(_list);

            _count = new Label { style = { opacity = 0.65f, marginTop = 3 } };
            root.Add(_count);

            Refilter(string.Empty);
            _search.schedule.Execute(() => _search.Focus()).ExecuteLater(10);
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 4, paddingRight = 4 } };
            row.Add(new Image { name = "icon", scaleMode = ScaleMode.ScaleToFit, style = { width = 22, height = 22, marginRight = 6 } });
            row.Add(new Label { name = "name", style = { flexGrow = 1, flexShrink = 1, overflow = Overflow.Hidden } });
            row.Add(new Label { name = "folder", style = { opacity = 0.6f, marginLeft = 6, maxWidth = 140, overflow = Overflow.Hidden } });
            row.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.button == 0 && row.userData is int index) Choose(index);
            });
            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            var entry = _shown[index];
            row.userData = index;
            var isCurrent = string.Equals(entry.Id, _currentId, StringComparison.OrdinalIgnoreCase);
            var name = row.Q<Label>("name");
            name.text = (isCurrent ? "✓ " : string.Empty) + entry.Name;
            name.style.unityFontStyleAndWeight = isCurrent ? FontStyle.Bold : FontStyle.Normal;
            row.Q<Label>("folder").text = entry.Folder;
            row.Q<Image>("icon").sprite = entry.IconPath.Length > 0 ? AssetDatabase.LoadAssetAtPath<Sprite>(entry.IconPath) : null;
        }

        internal void Refilter(string query)
        {
            _shown = ItemSearch.Filter(_all, query);
            _list.itemsSource = _shown;
            _list.Rebuild();
            if (_shown.Count > 0) _list.SetSelectionWithoutNotify(new[] { 0 });
            _count.text = string.IsNullOrWhiteSpace(query)
                ? $"{_all.Count} item(s)"
                : _shown.Count == 0 ? "Nothing matches." : $"{_shown.Count} of {_all.Count}";
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            switch (e.keyCode)
            {
                case KeyCode.DownArrow: Move(1); break;
                case KeyCode.UpArrow: Move(-1); break;
                case KeyCode.PageDown: Move(8); break;
                case KeyCode.PageUp: Move(-8); break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_shown.Count > 0) Choose(Mathf.Max(0, _list.selectedIndex));
                    break;
                case KeyCode.Escape:
                    CloseWindow();
                    break;
                default:
                    return;
            }
            e.StopPropagation();
        }

        private void Move(int step)
        {
            if (_shown.Count == 0) return;
            var index = Mathf.Clamp((_list.selectedIndex < 0 ? 0 : _list.selectedIndex) + step, 0, _shown.Count - 1);
            _list.SetSelectionWithoutNotify(new[] { index });
            _list.ScrollToItem(index);
        }

        internal void Choose(int index)
        {
            if (index < 0 || index >= _shown.Count) return;
            var id = _shown[index].Id;
            var picked = _picked;
            CloseWindow();
            picked?.Invoke(id);
        }

        private void OnLostFocus() => CloseWindow();

        private void CloseWindow()
        {
            if (!_isOpen) return;
            _isOpen = false;
            Close();
        }
    }
}

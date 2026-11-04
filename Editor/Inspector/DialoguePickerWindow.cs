using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Loreman.Dialogues.Editor
{
    /// <summary>
    /// The list that opens under a <see cref="DialogueReference"/> field: a search box that has the focus at once, and the dialogues below it. Type to narrow the list
    /// (every word must be in the group or the name), ↑ ↓ to move, Enter or a click to pick, Esc to close. It closes by itself when it loses the focus.
    /// What is listed and in which order is decided by <see cref="DialogueSearch"/>; this class only draws it.
    /// </summary>
    public sealed class DialoguePickerWindow : EditorWindow
    {
        private const float RowHeight = 22f;

        private IReadOnlyList<DialogueEntry> _all;
        private List<DialogueEntry> _shown = new List<DialogueEntry>();
        private string _currentId;
        private Action<string> _picked;
        private ListView _list;
        private Label _count;
        private ToolbarSearchField _search;
        private bool _isOpen;

        /// <summary>Opens the list under <paramref name="fieldRect"/> (in the coordinates of the GUI that draws the field).</summary>
        public static void Show(Rect fieldRect, IReadOnlyList<DialogueEntry> entries, string currentId, Action<string> picked)
        {
            var window = Create(entries, currentId, picked);
            window._isOpen = true;
            var screen = GUIUtility.GUIToScreenRect(fieldRect);
            window.ShowAsDropDown(screen, new Vector2(Mathf.Max(screen.width, 360f), 360f));
        }

        /// <summary>Makes the window without showing it (<see cref="Show"/> shows it; the tests look at it as it is).</summary>
        internal static DialoguePickerWindow Create(IReadOnlyList<DialogueEntry> entries, string currentId, Action<string> picked)
        {
            var window = CreateInstance<DialoguePickerWindow>();
            window._all = entries;
            window._currentId = currentId;
            window._picked = picked;
            window.titleContent = new GUIContent("Dialogue");
            return window;
        }

        /// <summary>What the list shows now.</summary>
        internal IReadOnlyList<DialogueEntry> Shown => _shown;

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
            // The search box gets the focus after the window is shown, not while it is built.
            _search.schedule.Execute(() => _search.Focus()).ExecuteLater(10);
        }

        // ── rows ──

        private VisualElement MakeRow()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 4, paddingRight = 4 } };
            row.Add(new Label { name = "name", style = { flexGrow = 1, flexShrink = 1, overflow = Overflow.Hidden } });
            row.Add(new Label { name = "group", style = { opacity = 0.6f, marginLeft = 6, maxWidth = 120, overflow = Overflow.Hidden } });
            row.Add(new Label { name = "nodes", style = { opacity = 0.45f, marginLeft = 6, width = 34, unityTextAlign = TextAnchor.MiddleRight } });
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
            row.Q<Label>("group").text = entry.Group;
            row.Q<Label>("nodes").text = entry.Nodes.ToString();
        }

        // ── search and keys ──

        internal void Refilter(string query)
        {
            _shown = DialogueSearch.Filter(_all, query);
            _list.itemsSource = _shown;
            _list.Rebuild();
            if (_shown.Count > 0) _list.SetSelectionWithoutNotify(new[] { 0 });
            _count.text = string.IsNullOrWhiteSpace(query)
                ? $"{_all.Count} dialogue(s)"
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

        // A window that was made but never shown (the tests do that) has nothing to close.
        private void CloseWindow()
        {
            if (!_isOpen) return;
            _isOpen = false;
            Close();
        }
    }
}

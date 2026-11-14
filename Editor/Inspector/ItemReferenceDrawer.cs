using System.Collections.Generic;
using Loreman.Items;
using UnityEditor;
using UnityEngine;

namespace Loreman.Dialogues.Editor
{
    /// <summary>
    /// Draws an <see cref="ItemReference"/> as a button with the picture and the name of the item (and its folder). A click opens <see cref="ItemPickerWindow"/>:
    /// type to search the items of the project, pick one. The × clears the field.
    /// </summary>
    [CustomPropertyDrawer(typeof(ItemReference))]
    public sealed class ItemReferenceDrawer : PropertyDrawer
    {
        private const float ClearWidth = 22f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var id = property.FindPropertyRelative("id");
            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, label);

            var entries = ItemLocator.FindAll();
            if (entries.Count == 0)
            {
                EditorGUI.HelpBox(position, "No items yet: add a .lorebuild file with items (Tools → Loreman → Window).", MessageType.Info);
                EditorGUI.EndProperty();
                return;
            }

            var text = ItemSearch.Label(entries, id.stringValue);
            var missing = text.StartsWith("(missing)");
            var hasValue = !string.IsNullOrEmpty(id.stringValue);

            var button = hasValue ? new Rect(position.x, position.y, position.width - ClearWidth - 2f, position.height) : position;
            var previousColor = GUI.color;
            if (missing) GUI.color = new Color(1f, 0.6f, 0.55f);
            var icon = hasValue && !missing ? IconOf(entries, id.stringValue) : null;
            var content = new GUIContent("  " + text, icon != null ? icon.texture : null, missing ? "The project has no such item any more." : text);
            var pressed = EditorGUI.DropdownButton(button, content, FocusType.Keyboard);
            GUI.color = previousColor;

            if (hasValue && GUI.Button(new Rect(button.xMax + 2f, position.y, ClearWidth, position.height), new GUIContent("×", "Clear"), EditorStyles.miniButton))
                Set(property.serializedObject, property, string.Empty);
            if (pressed) Open(button, property, entries, id.stringValue);
            EditorGUI.EndProperty();
        }

        private static Sprite IconOf(IReadOnlyList<ItemEntry> entries, string id)
        {
            foreach (var e in entries)
                if (string.Equals(e.Id, id, System.StringComparison.OrdinalIgnoreCase) && e.IconPath.Length > 0) return AssetDatabase.LoadAssetAtPath<Sprite>(e.IconPath);
            return null;
        }

        private static void Open(Rect button, SerializedProperty property, IReadOnlyList<ItemEntry> entries, string current)
        {
            // The list is another window: by the time something is picked this property is gone, so what is needed to find it again is kept.
            var targets = property.serializedObject.targetObjects;
            var path = property.propertyPath;
            ItemPickerWindow.Show(button, entries, current, picked =>
            {
                var so = new SerializedObject(targets);
                var field = so.FindProperty(path);
                if (field != null) Set(so, field, picked);
            });
        }

        private static void Set(SerializedObject so, SerializedProperty property, string id)
        {
            so.Update();
            property.FindPropertyRelative("id").stringValue = id;
            so.ApplyModifiedProperties();
        }
    }
}

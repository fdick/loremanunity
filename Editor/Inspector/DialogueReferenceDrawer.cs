using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Loreman.Dialogues.Editor
{
    /// <summary>
    /// Draws a <see cref="DialogueReference"/> as a button with the name of the dialogue (and its group). A click opens <see cref="DialoguePickerWindow"/>:
    /// type to search the dialogues of the catalog, pick one. The × clears the field.
    /// </summary>
    [CustomPropertyDrawer(typeof(DialogueReference))]
    public sealed class DialogueReferenceDrawer : PropertyDrawer
    {
        private const float ClearWidth = 22f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            CatalogLocator.FindAll().Count == 0 ? EditorGUIUtility.singleLineHeight * 2f : EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var id = property.FindPropertyRelative("id");
            var catalogs = CatalogLocator.FindAll();

            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, label);
            if (catalogs.Count == 0)
            {
                EditorGUI.HelpBox(position, "No catalog yet: add a .lorebuild file (Tools → Loreman → Window).", MessageType.Info);
                EditorGUI.EndProperty();
                return;
            }

            var entries = DialogueSearch.Entries(catalogs);
            var text = DialogueSearch.Label(entries, id.stringValue);
            var missing = text.StartsWith("(missing)");
            var hasValue = !string.IsNullOrEmpty(id.stringValue);

            var button = hasValue ? new Rect(position.x, position.y, position.width - ClearWidth - 2f, position.height) : position;
            var previousColor = GUI.color;
            if (missing) GUI.color = new Color(1f, 0.6f, 0.55f);
            var pressed = EditorGUI.DropdownButton(button, new GUIContent(text, missing ? "The catalog has no such dialogue any more." : text), FocusType.Keyboard);
            GUI.color = previousColor;

            if (hasValue && GUI.Button(new Rect(button.xMax + 2f, position.y, ClearWidth, position.height), new GUIContent("×", "Clear"), EditorStyles.miniButton))
                Set(property.serializedObject, property, string.Empty);
            if (pressed) Open(button, property, entries, id.stringValue);
            EditorGUI.EndProperty();
        }

        private static void Open(Rect button, SerializedProperty property, IReadOnlyList<DialogueEntry> entries, string current)
        {
            // The list is another window: by the time something is picked this property is gone, so what is needed to find it again is kept.
            var targets = property.serializedObject.targetObjects;
            var path = property.propertyPath;
            DialoguePickerWindow.Show(button, entries, current, picked =>
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

    /// <summary>Finds the catalogs the importer made: one for each Loreman build in the project.</summary>
    public static class CatalogLocator
    {
        public static IReadOnlyList<DialogueCatalog> FindAll()
        {
            var catalogs = new List<DialogueCatalog>();
            foreach (var guid in AssetDatabase.FindAssets("t:DialogueCatalog"))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<DialogueCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (catalog != null && catalog.ProjectGuid != System.Guid.Empty) catalogs.Add(catalog);
            }
            return catalogs;
        }

        /// <summary>For each Loreman project: the content hash of the build its catalog came from.</summary>
        public static Dictionary<System.Guid, string> HashesByProject()
        {
            var hashes = new Dictionary<System.Guid, string>();
            foreach (var catalog in FindAll()) hashes[catalog.ProjectGuid] = catalog.ContentHash;
            return hashes;
        }
    }
}

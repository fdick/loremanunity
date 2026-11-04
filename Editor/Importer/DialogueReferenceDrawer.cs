using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Loreman.Dialogues.Editor
{
    /// <summary>Shows a <see cref="DialogueReference"/> as a list of the dialogues of the catalog (grouped as they are in Loreman).</summary>
    [CustomPropertyDrawer(typeof(DialogueReference))]
    public sealed class DialogueReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var id = property.FindPropertyRelative("id");
            var catalog = FindCatalog();

            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, label);

            if (catalog == null)
            {
                EditorGUI.HelpBox(position, "No catalog yet: put a .lorebuild file into Assets.", MessageType.Info);
                EditorGUI.EndProperty();
                return;
            }

            var names = new List<string> { "(none)" };
            var ids = new List<string> { string.Empty };
            var groups = new Dictionary<string, string>();
            foreach (var g in catalog.Groups) groups[g.id] = g.name;
            var current = 0;
            foreach (var d in catalog.Dialogues)
            {
                var group = !string.IsNullOrEmpty(d.groupId) && groups.TryGetValue(d.groupId, out var gn) ? gn + "/" : string.Empty;
                if (d.id == id.stringValue) current = names.Count;
                names.Add(group + d.name);
                ids.Add(d.id);
            }
            if (current == 0 && !string.IsNullOrEmpty(id.stringValue))
            {
                // a dialogue the catalog no longer has: show it, so it is not lost silently
                current = names.Count;
                names.Add("(missing) " + id.stringValue);
                ids.Add(id.stringValue);
            }

            EditorGUI.BeginChangeCheck();
            var picked = EditorGUI.Popup(position, current, names.ToArray());
            if (EditorGUI.EndChangeCheck()) id.stringValue = ids[picked];
            EditorGUI.EndProperty();
        }

        private static DialogueCatalog FindCatalog()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:DialogueCatalog"))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<DialogueCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (catalog != null) return catalog;
            }
            return null;
        }
    }
}

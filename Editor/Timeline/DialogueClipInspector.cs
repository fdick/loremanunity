using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

namespace Loreman.Dialogues.Timeline.Editor
{
    /// <summary>The inspector of a dialogue clip: its settings, how long the dialogue takes, and a button that makes the clip that long.</summary>
    [CustomEditor(typeof(DialogueClip))]
    public sealed class DialogueClipInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var clip = (DialogueClip)target;
            EditorGUILayout.Space();

            if (clip.autoAdvance == DialogueAutoAdvance.Manual)
            {
                EditorGUILayout.HelpBox("The player goes on with Next, so the length of the dialogue is not known. Choose After Seconds or After Voice Over to have the clip fitted to the dialogue.", MessageType.None);
                return;
            }
            if (!clip.dialogue.IsSet) return;

            var length = DialogueClipFit.Measure(clip);
            if (length == null)
            {
                EditorGUILayout.HelpBox("This dialogue is not in the project (sync the build: Tools → Loreman → Window).", MessageType.Warning);
                return;
            }

            var l = length.Value;
            var text = $"The dialogue takes about {l.Seconds:0.#} s ({l.Lines} lines" + (clip.autoAdvance == DialogueAutoAdvance.AfterVoiceOver ? ", the longest voice-over of each line" : "") + ").";
            if (l.HasChoices) text += " It has a choice: the longest answer is counted, and the time the player thinks is not.";
            if (l.HasLoop) text += " An answer leads back: only the way up to the second time round is counted.";
            EditorGUILayout.HelpBox(text, MessageType.Info);

            if (GUILayout.Button("Fit Clip To Dialogue") && Fit(clip))
                TimelineEditor.Refresh(RefreshReason.ContentsModified);
        }

        private static bool Fit(DialogueClip asset)
        {
            var timelineClip = FindClip(asset);
            return timelineClip != null && DialogueClipFit.Fit(timelineClip);
        }

        private static TimelineClip FindClip(DialogueClip asset)
        {
            var selected = TimelineEditor.selectedClip;
            if (selected != null && ReferenceEquals(selected.asset, asset)) return selected;
            var timeline = TimelineEditor.inspectedAsset;
            return timeline == null ? null : timeline.GetOutputTracks().SelectMany(t => t.GetClips()).FirstOrDefault(c => ReferenceEquals(c.asset, asset));
        }
    }
}

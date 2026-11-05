using System;
using UnityEditor;
using UnityEngine.Timeline;

namespace Loreman.Dialogues.Timeline.Editor
{
    /// <summary>Makes a dialogue clip as long as its dialogue, when the dialogue goes on by itself (so the clip on the track shows how long the dialogue takes).</summary>
    public static class DialogueClipFit
    {
        /// <summary>The clip is a little longer than the dialogue: the loading of a recording is not counted in the estimate, and a dialogue must not be cut off.</summary>
        public const double Slack = 0.5;

        /// <summary>The length of the dialogue of the clip, or null when it cannot be told (nothing chosen, not synced, or the player goes on with Next).</summary>
        public static DialogueLength? Measure(DialogueClip clip)
        {
            if (clip == null || !clip.dialogue.IsSet || clip.autoAdvance == DialogueAutoAdvance.Manual) return null;
            return Loreman.Dialogues.Editor.DialogueLengthMeasure.Measure(clip.dialogue.Guid, clip.autoAdvance, clip.seconds);
        }

        /// <summary>The length the clip would get.</summary>
        public static double FittedDuration(DialogueLength length) => Math.Max(0.1d, length.Seconds + Slack);

        /// <summary>Sets the length of the clip to that of its dialogue. False when that cannot be told.</summary>
        public static bool Fit(TimelineClip clip)
        {
            if (clip == null || !(clip.asset is DialogueClip dialogueClip)) return false;
            var length = Measure(dialogueClip);
            if (length == null || !length.Value.IsKnown) return false;
            var track = clip.GetParentTrack();
            if (track != null) Undo.RecordObject(track, "Fit Dialogue Clip");
            clip.duration = FittedDuration(length.Value);
            dialogueClip.lengthFittedFor = Signature(dialogueClip);
            EditorUtility.SetDirty(dialogueClip);
            return true;
        }

        /// <summary>Fits the clip when the dialogue or the pace has changed since it was last fitted. A clip that is only moved or stretched by hand is left alone.</summary>
        public static void FitWhenChanged(TimelineClip clip)
        {
            if (clip == null || !(clip.asset is DialogueClip dialogueClip)) return;
            var signature = Signature(dialogueClip);
            if (dialogueClip.lengthFittedFor == signature) return;
            if (dialogueClip.autoAdvance == DialogueAutoAdvance.Manual || !dialogueClip.dialogue.IsSet)
            {
                dialogueClip.lengthFittedFor = signature; // nothing to fit: remember, so that changing the pace later fits the clip
                EditorUtility.SetDirty(dialogueClip);
                return;
            }
            // remembered first: setting the duration makes Timeline call the editor again
            dialogueClip.lengthFittedFor = signature;
            Fit(clip);
        }

        internal static string Signature(DialogueClip clip) => clip.dialogue + "|" + clip.autoAdvance + "|" + clip.seconds;
    }
}

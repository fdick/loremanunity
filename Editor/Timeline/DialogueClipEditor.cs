using UnityEditor.Timeline;
using UnityEngine.Timeline;

namespace Loreman.Dialogues.Timeline.Editor
{
    /// <summary>
    /// Names a dialogue clip after its dialogue (the clip shows "Gate Guard / City Gate", not "Dialogue Clip"), and says on the clip what it does. The name follows
    /// the dialogue when it is changed in the inspector.
    /// </summary>
    [CustomTimelineEditor(typeof(DialogueClip))]
    public sealed class DialogueClipEditor : ClipEditor
    {
        public override ClipDrawOptions GetClipOptions(TimelineClip clip)
        {
            var options = base.GetClipOptions(clip);
            if (clip.asset is DialogueClip dialogueClip)
                options.tooltip = Describe(dialogueClip);
            return options;
        }

        public override void OnCreate(TimelineClip clip, TrackAsset track, TimelineClip clonedFrom) => Rename(clip);

        public override void OnClipChanged(TimelineClip clip) => Rename(clip);

        private static void Rename(TimelineClip clip)
        {
            if (!(clip.asset is DialogueClip dialogueClip)) return;
            clip.displayName = Loreman.Dialogues.Editor.DialogueSearch.Label(Loreman.Dialogues.Editor.DialogueSearch.Entries(Loreman.Dialogues.Editor.CatalogLocator.FindAll()), dialogueClip.dialogue.IsSet ? dialogueClip.dialogue.ToString() : string.Empty);
        }

        /// <summary>The tooltip of a clip: what it plays, what the timeline does meanwhile, how the lines go on.</summary>
        public static string Describe(DialogueClip clip)
        {
            var wait = clip.ending == DialogueClipEnding.WaitForDialogue
                ? "The timeline waits here until the dialogue is over."
                : "The dialogue is stopped when the clip ends.";
            string advance;
            switch (clip.autoAdvance)
            {
                case DialogueAutoAdvance.AfterSeconds: advance = $"Each line goes on after {clip.seconds:0.#} s."; break;
                case DialogueAutoAdvance.AfterVoiceOver: advance = $"Each line goes on {clip.seconds:0.#} s after its voice-over."; break;
                default: advance = "The player goes on with Next."; break;
            }
            return wait + " " + advance;
        }
    }
}

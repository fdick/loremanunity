using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Loreman.Dialogues.Timeline
{
    /// <summary>
    /// A dialogue on a timeline: when the clip starts, the <see cref="DialoguePlayer"/> bound to its track plays the chosen dialogue. By default the timeline waits where
    /// the clip starts until the dialogue is over (the player presses Next, or the dialogue goes on by itself); the screen that shows the dialogue is whatever
    /// listens to that player.
    /// </summary>
    [Serializable]
    public sealed class DialogueClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("The dialogue to play. Pick it from the list of the dialogues of your builds.")]
        public DialogueReference dialogue;

        [Tooltip("Wait For Dialogue: the timeline stops where the clip starts until the dialogue is over. Stop At End Of Clip: the timeline goes on, and the dialogue is stopped when the clip ends.")]
        public DialogueClipEnding ending = DialogueClipEnding.WaitForDialogue;

        [Tooltip("Manual: the player presses Next. After Seconds: each line goes on by itself. After Voice Over: each line goes on when its voice-over is said, plus the pause below. A choice always waits for the player.")]
        public DialogueAutoAdvance autoAdvance = DialogueAutoAdvance.Manual;

        [Min(0f)]
        [Tooltip("After Seconds: how long each line stays. After Voice Over: the pause after the voice (and how long a line without a voice stays).")]
        public float seconds = 3f;

        /// <summary>Editor only: what the length of the clip was last fitted for, so it is fitted again only when the dialogue or its pace changes.</summary>
        [HideInInspector] public string lengthFittedFor;

        public ClipCaps clipCaps => ClipCaps.None;

        /// <summary>The length a new clip gets. With Wait For Dialogue the length does not matter.</summary>
        public override double duration => 2d;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<DialogueBehaviour>.Create(graph);
            playable.GetBehaviour().Configure(this, FindPlayer(owner));
            return playable;
        }

        /// <summary>The player bound to the track this clip is on, as the director that owns the timeline has it.</summary>
        private DialoguePlayer FindPlayer(GameObject owner)
        {
            var director = owner != null ? owner.GetComponent<PlayableDirector>() : null;
            if (director == null || !(director.playableAsset is TimelineAsset timeline)) return null;
            var track = timeline.GetOutputTracks().OfType<DialogueTrack>().FirstOrDefault(t => t.GetClips().Any(c => ReferenceEquals(c.asset, this)));
            return track == null ? null : director.GetGenericBinding(track) as DialoguePlayer;
        }
    }
}

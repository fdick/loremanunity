using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Timeline;

namespace Loreman.Dialogues.Timeline.Tests
{
    public class DialogueClipControllerTests
    {
        private sealed class FakeCue : IDialogueCue
        {
            public readonly List<Guid> Played = new List<Guid>();
            public int Stopped;
            public DialogueAutoAdvance Mode { get; private set; } = DialogueAutoAdvance.Manual;
            public float Seconds { get; private set; } = 1f;
            public event Action Finished;
            public event Action<Exception> Failed;
            public DialogueAutoAdvance AutoAdvance => Mode;
            public float AutoAdvanceSeconds => Seconds;
            public void SetAutoAdvance(DialogueAutoAdvance mode, float seconds) { Mode = mode; Seconds = seconds; }
            public void Play(Guid dialogueId) => Played.Add(dialogueId);
            public void Stop() { Stopped++; Finished?.Invoke(); } // the real player raises Finished when it is stopped
            public void Over() => Finished?.Invoke();
            public void Fail() => Failed?.Invoke(new InvalidOperationException("no such dialogue"));
        }

        private sealed class FakeHold : ITimeHold
        {
            public int Holds, Releases;
            public bool IsHeld => Holds > Releases;
            public void Hold() => Holds++;
            public void Release() => Releases++;
        }

        private static readonly Guid Dialogue = new Guid("11111111-2222-3333-4444-555555555555");

        private static (DialogueClipController, FakeCue, FakeHold) Make(
            DialogueClipEnding ending = DialogueClipEnding.WaitForDialogue, DialogueAutoAdvance advance = DialogueAutoAdvance.Manual, float seconds = 3f, Guid? dialogue = null)
        {
            var cue = new FakeCue();
            var hold = new FakeHold();
            return (new DialogueClipController(cue, hold, dialogue ?? Dialogue, ending, advance, seconds), cue, hold);
        }

        [Test]
        public void A_clip_that_waits_holds_the_timeline_and_plays_the_dialogue()
        {
            var (clip, cue, hold) = Make();
            clip.Begin();
            CollectionAssert.AreEqual(new[] { Dialogue }, cue.Played);
            Assert.IsTrue(hold.IsHeld);
            Assert.IsTrue(clip.IsHolding);
        }

        [Test]
        public void The_timeline_goes_on_when_the_dialogue_is_over()
        {
            var (clip, cue, hold) = Make();
            clip.Begin();
            cue.Over();
            Assert.IsFalse(hold.IsHeld);
            clip.End();
            Assert.AreEqual(0, cue.Stopped, "a dialogue that is over is not stopped again");
        }

        [Test]
        public void A_clip_that_does_not_wait_never_holds_and_stops_the_dialogue_when_the_clip_ends()
        {
            var (clip, cue, hold) = Make(DialogueClipEnding.StopAtEndOfClip);
            clip.Begin();
            Assert.AreEqual(0, hold.Holds);
            clip.End();
            Assert.AreEqual(1, cue.Stopped);
        }

        [Test]
        public void A_dialogue_that_ended_before_the_clip_does_is_not_stopped()
        {
            var (clip, cue, _) = Make(DialogueClipEnding.StopAtEndOfClip);
            clip.Begin();
            cue.Over();
            clip.End();
            Assert.AreEqual(0, cue.Stopped);
        }

        [Test]
        public void Leaving_the_clip_while_it_waits_releases_the_timeline_and_stops_the_dialogue()
        {
            var (clip, cue, hold) = Make();
            clip.Begin();
            clip.End(); // the timeline was stopped or left
            Assert.IsFalse(hold.IsHeld);
            Assert.AreEqual(1, cue.Stopped);
        }

        [Test]
        public void A_dialogue_that_cannot_be_played_does_not_freeze_the_timeline()
        {
            var (clip, cue, hold) = Make();
            clip.Begin();
            cue.Fail();
            Assert.IsFalse(hold.IsHeld);
        }

        [Test]
        public void Starting_twice_is_starting_once_and_ending_without_starting_is_nothing()
        {
            var (clip, cue, hold) = Make();
            clip.End();
            Assert.AreEqual(0, cue.Stopped);
            clip.Begin();
            clip.Begin();
            Assert.AreEqual(1, cue.Played.Count);
            Assert.AreEqual(1, hold.Holds);
        }

        [Test]
        public void A_clip_with_no_dialogue_chosen_does_nothing_and_holds_nothing()
        {
            var (clip, cue, hold) = Make(dialogue: Guid.Empty);
            clip.Begin();
            Assert.IsEmpty(cue.Played);
            Assert.AreEqual(0, hold.Holds);
            Assert.IsFalse(clip.IsRunning);
        }

        [Test]
        public void The_automatic_advance_of_the_clip_is_set_while_it_runs_and_the_old_one_comes_back()
        {
            var (clip, cue, _) = Make(advance: DialogueAutoAdvance.AfterVoiceOver, seconds: 1.5f);
            cue.SetAutoAdvance(DialogueAutoAdvance.AfterSeconds, 7f); // what the player had before
            clip.Begin();
            Assert.AreEqual(DialogueAutoAdvance.AfterVoiceOver, cue.Mode);
            Assert.AreEqual(1.5f, cue.Seconds);
            cue.Over();
            clip.End();
            Assert.AreEqual(DialogueAutoAdvance.AfterSeconds, cue.Mode);
            Assert.AreEqual(7f, cue.Seconds);
        }

        [Test]
        public void A_clip_can_run_again_after_it_ended()
        {
            var (clip, cue, hold) = Make();
            clip.Begin();
            cue.Over();
            clip.End();
            clip.Begin(); // scrubbed back, or looped
            Assert.AreEqual(2, cue.Played.Count);
            Assert.IsTrue(hold.IsHeld);
        }

        // ── the assets ──

        [Test]
        public void The_track_takes_dialogue_clips_and_a_new_clip_waits_for_its_dialogue_and_goes_on_by_hand()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            try
            {
                var track = timeline.CreateTrack<DialogueTrack>(null, "Dialogue");
                var clip = track.CreateClip<DialogueClip>();
                var asset = (DialogueClip)clip.asset;
                Assert.AreEqual(DialogueClipEnding.WaitForDialogue, asset.ending);
                Assert.AreEqual(DialogueAutoAdvance.Manual, asset.autoAdvance);
                Assert.IsFalse(asset.dialogue.IsSet);
                Assert.AreEqual(ClipCaps.None, asset.clipCaps);
                Assert.Greater(clip.duration, 0d);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(timeline);
            }
        }
    }
}

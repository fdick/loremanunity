using System;
using NUnit.Framework;

namespace Loreman.Dialogues.Tests
{
    public class DialogueLengthTests
    {
        private static DialogueGraph Gate()
        {
            using (var file = SampleBuild.Open()) return SampleBuild.GateGraph(file);
        }

        private static DialogueNode Line(int index, int next, long audio = 0) =>
            DialogueNode.CreateLine(index, Guid.NewGuid(), -1, new TextKey(100 + index), "", next, TextKey.None, new TextKey(audio));

        [Test]
        public void A_dialogue_the_player_goes_through_with_Next_has_no_known_length()
        {
            var length = DialogueLengthEstimator.Estimate(Gate(), DialogueAutoAdvance.Manual, 3f);
            Assert.IsFalse(length.IsKnown);
        }

        [Test]
        public void After_seconds_every_line_takes_the_same_and_a_choice_takes_no_time_of_its_own()
        {
            // line → choice → (answer) line : two lines and a question
            var length = DialogueLengthEstimator.Estimate(Gate(), DialogueAutoAdvance.AfterSeconds, 2f);
            Assert.IsTrue(length.IsKnown);
            Assert.IsTrue(length.HasChoices);
            Assert.IsFalse(length.HasLoop);
            Assert.AreEqual(4d, length.Seconds, 1e-9);
            Assert.AreEqual(3, length.Lines);
        }

        [Test]
        public void After_the_voice_over_a_line_takes_its_recording_plus_the_pause_and_a_silent_line_the_pause()
        {
            var graph = Gate();
            var first = graph.Nodes[graph.Entry];
            Assert.IsTrue(first.HasAudio);
            var length = DialogueLengthEstimator.Estimate(graph, DialogueAutoAdvance.AfterVoiceOver, 0.5f, key => key == first.Audio ? 3d : 0d);
            // (3 + 0.5) for the first line, nothing for the choice (it has no recording here), 0.5 for the silent last line
            Assert.AreEqual(4d, length.Seconds, 1e-9);
        }

        [Test]
        public void A_choice_counts_the_longest_answer()
        {
            // 0 → choice(1) → answer A: line 2 (ends)   answer B: line 3 → line 4 (ends)
            var options = new[]
            {
                new DialogueOption(Guid.NewGuid(), new TextKey(1), 2, TextKey.None),
                new DialogueOption(Guid.NewGuid(), new TextKey(2), 3, TextKey.None),
            };
            var nodes = new DialogueNode[]
            {
                Line(0, 1),
                DialogueNode.CreateChoice(1, Guid.NewGuid(), -1, new TextKey(101), "", -1, options),
                Line(2, -1),
                Line(3, 4),
                Line(4, -1),
            };
            var length = DialogueLengthEstimator.Estimate(new DialogueGraph(Guid.NewGuid(), 0, nodes), DialogueAutoAdvance.AfterSeconds, 1f);
            Assert.AreEqual(3d, length.Seconds, 1e-9, "line 0, then the way of answer B (two lines)");
            Assert.AreEqual(4, length.Lines);
            Assert.IsTrue(length.HasChoices);
        }

        [Test]
        public void A_dialogue_that_goes_round_in_a_circle_is_counted_once_round_and_reported()
        {
            var options = new[]
            {
                new DialogueOption(Guid.NewGuid(), new TextKey(1), 0, TextKey.None), // back to the start
                new DialogueOption(Guid.NewGuid(), new TextKey(2), -1, TextKey.None),
            };
            var nodes = new DialogueNode[]
            {
                Line(0, 1),
                DialogueNode.CreateChoice(1, Guid.NewGuid(), -1, new TextKey(101), "", -1, options),
            };
            var length = DialogueLengthEstimator.Estimate(new DialogueGraph(Guid.NewGuid(), 0, nodes), DialogueAutoAdvance.AfterSeconds, 2f);
            Assert.IsTrue(length.HasLoop);
            Assert.AreEqual(2d, length.Seconds, 1e-9);
        }

        [Test]
        public void An_empty_dialogue_takes_no_time_and_a_negative_pause_is_zero()
        {
            var empty = new DialogueGraph(Guid.NewGuid(), -1, new DialogueNode[0]);
            var length = DialogueLengthEstimator.Estimate(empty, DialogueAutoAdvance.AfterSeconds, 5f);
            Assert.IsTrue(length.IsKnown);
            Assert.AreEqual(0d, length.Seconds);

            var one = new DialogueGraph(Guid.NewGuid(), 0, new[] { Line(0, -1) });
            Assert.AreEqual(0d, DialogueLengthEstimator.Estimate(one, DialogueAutoAdvance.AfterSeconds, -3f).Seconds);
        }
    }
}

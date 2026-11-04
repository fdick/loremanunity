using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Loreman.Dialogues.Tests
{
    public class SessionTests
    {
        // line0 → choice1 { A → line2 → end ; B → end ; C → line0 }
        private static readonly Guid[] Ids = { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        private static DialogueGraph Graph() => new DialogueGraph(Guid.NewGuid(), 0, new[]
        {
            DialogueNode.CreateLine(0, Ids[0], 0, new TextKey(1), "", 1, TextKey.None),
            DialogueNode.CreateChoice(1, Ids[1], 0, new TextKey(2), "", 1, new[]
            {
                new DialogueOption(Guid.NewGuid(), new TextKey(3), 2, TextKey.None),
                new DialogueOption(Guid.NewGuid(), new TextKey(4), -1, TextKey.None),
                new DialogueOption(Guid.NewGuid(), new TextKey(5), 0, TextKey.None),
            }),
            DialogueNode.CreateLine(2, Ids[2], 1, new TextKey(6), "", -1, TextKey.None),
        });

        private static (DialogueSession Session, List<Guid> Seen, Action Ended) Make(DialogueGraph graph = null)
        {
            var s = new DialogueSession(graph ?? Graph());
            var seen = new List<Guid>();
            var ended = 0;
            s.NodeEntered += n => seen.Add(n.Id);
            s.Ended += () => ended++;
            return (s, seen, () => Assert.AreEqual(1, ended));
        }

        [Test]
        public void It_starts_at_the_entry_and_goes_line_to_choice()
        {
            var (s, seen, _) = Make();
            Assert.AreEqual(DialogueSessionState.NotStarted, s.State);
            Assert.IsNull(s.Current);
            s.Start();
            Assert.AreEqual(DialogueSessionState.AtLine, s.State);
            s.Advance();
            Assert.AreEqual(DialogueSessionState.AtChoice, s.State);
            CollectionAssert.AreEqual(new[] { Ids[0], Ids[1] }, seen);
        }

        [Test]
        public void An_answer_leads_to_its_own_node_or_ends_the_dialogue()
        {
            var (s, seen, assertEnded) = Make();
            s.Start();
            s.Advance();
            s.Choose(0);
            Assert.AreEqual(Ids[2], s.Current.Id);
            s.Advance(); // the last line has no next: the dialogue ends
            Assert.AreEqual(DialogueSessionState.Ended, s.State);
            Assert.IsNull(s.Current);
            assertEnded();

            var second = Make();
            second.Session.Start();
            second.Session.Advance();
            second.Session.Choose(1); // an answer that leads nowhere
            Assert.AreEqual(DialogueSessionState.Ended, second.Session.State);
        }

        [Test]
        public void An_answer_may_lead_back()
        {
            var (s, seen, _) = Make();
            s.Start();
            s.Advance();
            s.Choose(2);
            Assert.AreEqual(Ids[0], s.Current.Id);
            Assert.AreEqual(DialogueSessionState.AtLine, s.State);
        }

        [Test]
        public void A_choice_cannot_be_advanced_and_a_line_cannot_be_chosen_from()
        {
            var (s, _, _) = Make();
            s.Start();
            Assert.Throws<InvalidOperationException>(() => s.Choose(0));
            s.Advance();
            Assert.Throws<InvalidOperationException>(() => s.Advance());
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Choose(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Choose(-1));
        }

        [Test]
        public void Nothing_works_before_the_start_or_after_the_end()
        {
            var (s, _, _) = Make();
            Assert.Throws<InvalidOperationException>(() => s.Advance());
            s.Start();
            s.End();
            Assert.Throws<InvalidOperationException>(() => s.Advance());
            Assert.Throws<InvalidOperationException>(() => s.Choose(0));
        }

        [Test]
        public void An_empty_dialogue_ends_at_once()
        {
            var (s, seen, assertEnded) = Make(new DialogueGraph(Guid.NewGuid(), -1, new DialogueNode[0]));
            s.Start();
            Assert.AreEqual(DialogueSessionState.Ended, s.State);
            Assert.IsEmpty(seen);
            assertEnded();
        }

        [Test]
        public void A_choice_without_answers_ends_when_advanced()
        {
            var graph = new DialogueGraph(Guid.NewGuid(), 0, new[] { DialogueNode.CreateChoice(0, Guid.NewGuid(), -1, TextKey.None, "", -1, new DialogueOption[0]) });
            var (s, _, assertEnded) = Make(graph);
            s.Start();
            s.Advance();
            assertEnded();
        }

        [Test]
        public void It_can_carry_on_from_a_saved_node()
        {
            var (s, seen, _) = Make();
            Assert.IsTrue(s.JumpTo(Ids[1]));
            Assert.AreEqual(DialogueSessionState.AtChoice, s.State);
            Assert.IsFalse(s.JumpTo(Guid.NewGuid()));
            Assert.AreEqual(Ids[1], s.Current.Id); // a failed jump changes nothing
        }

        [Test]
        public void It_can_be_started_again()
        {
            var (s, seen, _) = Make();
            s.Start();
            s.End();
            s.Start();
            Assert.AreEqual(Ids[0], s.Current.Id);
        }
    }
}

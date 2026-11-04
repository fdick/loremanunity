using System;
using System.Linq;
using NUnit.Framework;
using Loreman.Dialogues.Editor;

namespace Loreman.Dialogues.Tests
{
    public class FormatTests
    {
        [Test]
        public void The_manifest_of_the_sample_says_what_is_inside()
        {
            using (var file = SampleBuild.Open())
            {
                var m = file.Manifest;
                Assert.AreEqual("loreman.build", m.format);
                Assert.AreEqual(1, m.formatVersion);
                Assert.AreEqual("My Game!", m.projectName);
                Assert.AreEqual("en", m.baseLanguage);
                CollectionAssert.AreEquivalent(new[] { "en", "ru", "de" }, m.languages.Select(l => l.code));
                Assert.AreEqual(1, m.dialogues.Count);
                Assert.AreEqual(2, m.speakers.Count);
                Assert.AreEqual(1, m.groups.Count);
                Assert.AreEqual(32, m.contentHash.Length);
                Assert.AreEqual(3, m.dialogues[0].nodes);
                Assert.AreEqual(m.groups[0].id, m.dialogues[0].groupId);
            }
        }

        [Test]
        public void The_graph_of_the_sample_is_read_node_by_node()
        {
            using (var file = SampleBuild.Open())
            {
                var g = SampleBuild.GateGraph(file);
                Assert.AreEqual(SampleBuild.Gate(file.Manifest).Guid, g.Id);
                Assert.AreEqual(0, g.Entry);
                Assert.AreEqual(3, g.Nodes.Count);

                var line = g.Nodes[0];
                Assert.IsFalse(line.IsChoice);
                Assert.AreEqual("shout", line.Note);
                Assert.AreEqual(1, line.Next);
                Assert.IsFalse(line.NextLabel.IsNone); // "after a pause"
                Assert.GreaterOrEqual(line.Speaker, 0);

                var choice = g.Nodes[1];
                Assert.IsTrue(choice.IsChoice);
                Assert.AreEqual("if: has_pass", choice.Note);
                Assert.AreEqual(2, choice.Options.Count);
                Assert.AreEqual(2, choice.Options[0].Next); // "Of course" → Bye
                Assert.AreEqual(-1, choice.Options[1].Next); // "Never" ends the dialogue
                Assert.AreNotEqual(choice.Speaker, choice.AnswerSpeaker); // the question is the guard's, the answers are the hero's

                var bye = g.Nodes[2];
                Assert.AreEqual(-1, bye.Next);
                Assert.IsTrue(g.TryGetNode(bye.Id, out var found));
                Assert.AreSame(bye, found);
                Assert.IsFalse(g.TryGetNode(Guid.NewGuid(), out _));
            }
        }

        [Test]
        public void The_texts_of_the_graph_are_in_the_table_of_the_language_under_their_keys()
        {
            using (var file = SampleBuild.Open())
            {
                var g = SampleBuild.GateGraph(file);
                var gate = SampleBuild.Gate(file.Manifest);
                var en = StringTableReader.Read(file.Read(LoreBuildFile.TextsPath("en", gate.Guid))).ToDictionary(e => e.Id);
                Assert.AreEqual("Halt! Who goes there?", en[g.Nodes[0].Text.Id].Text);
                Assert.AreEqual("Let me pass?", en[g.Nodes[1].Text.Id].Text);
                Assert.AreEqual("Of course", en[g.Nodes[1].Options[0].Text.Id].Text);
                Assert.AreEqual("Move along.", en[g.Nodes[2].Text.Id].Text);
                StringAssert.StartsWith("node:", en[g.Nodes[0].Text.Id].Key);

                var ru = StringTableReader.Read(file.Read(LoreBuildFile.TextsPath("ru", gate.Guid))).ToDictionary(e => e.Id);
                Assert.AreEqual("Стой! Кто идёт?", ru[g.Nodes[0].Text.Id].Text);
                Assert.IsFalse(ru.ContainsKey(g.Nodes[2].Text.Id)); // not translated: no entry, so the game falls back
                Assert.IsFalse(file.Has(LoreBuildFile.TextsPath("de", gate.Guid))); // nothing in German at all
            }
        }

        [Test]
        public void The_speakers_are_named_in_the_speakers_table()
        {
            using (var file = SampleBuild.Open())
            {
                var g = SampleBuild.GateGraph(file);
                var guard = file.Manifest.speakers[g.Nodes[0].Speaker];
                var hero = file.Manifest.speakers[g.Nodes[1].AnswerSpeaker];
                var en = StringTableReader.Read(file.Read(LoreBuildFile.SpeakerTextsPath("en"))).ToDictionary(e => e.Id);
                var ru = StringTableReader.Read(file.Read(LoreBuildFile.SpeakerTextsPath("ru"))).ToDictionary(e => e.Id);
                Assert.AreEqual("Guard", en[guard.nameKey].Text);
                Assert.AreEqual("Gruff, short sentences", en[guard.noteKey].Text);
                Assert.AreEqual("#ff0000", guard.color);
                Assert.AreEqual("Hero", en[hero.nameKey].Text);
                Assert.AreEqual(0, hero.noteKey);
                Assert.AreEqual("Стражник", ru[guard.nameKey].Text);
            }
        }

        // ── damaged data ──

        private static byte[] GateBytes()
        {
            using (var file = SampleBuild.Open()) return file.Read(SampleBuild.Gate(file.Manifest).graph);
        }

        [Test]
        public void A_graph_that_is_cut_short_is_refused_with_a_clear_message()
        {
            var bytes = GateBytes();
            Assert.Throws<DialogueFormatException>(() => DialogueGraphReader.Read(bytes.Take(bytes.Length - 5).ToArray()));
            Assert.Throws<DialogueFormatException>(() => DialogueGraphReader.Read(new byte[0]));
        }

        [Test]
        public void A_graph_with_another_magic_or_a_newer_version_is_refused()
        {
            var bytes = GateBytes();
            var wrongMagic = (byte[])bytes.Clone();
            wrongMagic[0] = (byte)'X';
            Assert.Throws<DialogueFormatException>(() => DialogueGraphReader.Read(wrongMagic));

            var newer = (byte[])bytes.Clone();
            newer[4] = 2;
            var e = Assert.Throws<DialogueFormatException>(() => DialogueGraphReader.Read(newer));
            StringAssert.Contains("version 2", e.Message);
        }

        [Test]
        public void A_graph_with_bytes_left_over_is_refused()
        {
            var bytes = GateBytes().Concat(new byte[] { 1 }).ToArray();
            Assert.Throws<DialogueFormatException>(() => DialogueGraphReader.Read(bytes));
        }

        [Test]
        public void A_string_table_that_is_not_one_is_refused()
        {
            Assert.Throws<DialogueFormatException>(() => StringTableReader.Read(new byte[] { 1, 2, 3 }));
            Assert.Throws<DialogueFormatException>(() => StringTableReader.Read(GateBytes()));
        }

        [Test]
        public void A_file_that_is_not_a_build_is_refused()
        {
            Assert.Throws<DialogueFormatException>(() => LoreBuildFile.Open(new System.IO.MemoryStream(new byte[] { 1, 2, 3, 4 })));
        }

        [Test]
        public void The_text_key_none_is_zero_and_equal_to_itself()
        {
            Assert.IsTrue(TextKey.None.IsNone);
            Assert.AreEqual(new TextKey(5), new TextKey(5));
            Assert.AreNotEqual(new TextKey(5), new TextKey(6));
        }
    }
}

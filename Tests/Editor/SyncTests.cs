using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Loreman.Dialogues.Editor;

namespace Loreman.Dialogues.Tests
{
    public class SyncTests
    {
        private static ManifestDialogue D(Guid id, string hash) => new ManifestDialogue { id = id.ToString("D"), name = "d", hash = hash };

        // ── the plan ──

        [Test]
        public void The_plan_adds_what_is_new_updates_what_changed_removes_what_is_gone_and_leaves_the_rest()
        {
            Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid(), gone = Guid.NewGuid();
            var plan = SyncPlan.Compute(
                new[] { D(a, "1"), D(b, "2"), D(c, "3") },
                new Dictionary<Guid, string> { [b] = "OLD", [c] = "3", [gone] = "x" });
            CollectionAssert.AreEqual(new[] { a }, plan.ToAdd.Select(d => d.Guid));
            CollectionAssert.AreEqual(new[] { b }, plan.ToUpdate.Select(d => d.Guid));
            CollectionAssert.AreEqual(new[] { gone }, plan.ToRemove);
            Assert.AreEqual(1, plan.Unchanged);
            Assert.IsFalse(plan.IsEmpty);
        }

        [Test]
        public void The_plan_of_a_build_the_project_already_has_is_empty()
        {
            var id = Guid.NewGuid();
            Assert.IsTrue(SyncPlan.Compute(new[] { D(id, "1") }, new Dictionary<Guid, string> { [id] = "1" }).IsEmpty);
        }

        [Test]
        public void A_dialogue_listed_twice_or_without_an_id_is_taken_once_or_not_at_all()
        {
            var id = Guid.NewGuid();
            var plan = SyncPlan.Compute(new[] { D(id, "1"), D(id, "1"), new ManifestDialogue { id = "garbage" } }, new Dictionary<Guid, string>());
            Assert.AreEqual(1, plan.ToAdd.Count);
        }

        // ── locale codes ──

        [TestCase("en", "en")]
        [TestCase("ru", "ru")]
        [TestCase("pt-br", "pt-BR")]
        [TestCase("zh-hans", "zh-Hans")]
        public void A_loreman_code_becomes_the_name_unity_uses(string loreman, string unity) => Assert.AreEqual(unity, LocaleCodes.ToUnity(loreman));

        [Test]
        public void The_settings_may_choose_the_locale_of_a_language()
        {
            var map = new Dictionary<string, string> { ["pt-br"] = "pt" };
            Assert.AreEqual("pt", LocaleCodes.ToUnity("pt-br", map));
            Assert.AreEqual("ru", LocaleCodes.ToUnity("ru", map));
        }

        // ── the synchronizer, against a fake target ──

        private sealed class FakeTarget : ISyncTarget
        {
            public string Hash = "";
            public Dictionary<Guid, string> Existing = new Dictionary<Guid, string>();
            public readonly List<string> Log = new List<string>();
            public readonly Dictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<StringTableEntry>>> Texts = new Dictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<StringTableEntry>>>();
            public readonly Dictionary<Guid, IReadOnlyList<SyncAudioFile>> Audio = new Dictionary<Guid, IReadOnlyList<SyncAudioFile>>();
            public IReadOnlyList<string> Languages;
            public bool Ended;

            public string ContentHash => Hash;
            public IReadOnlyDictionary<Guid, string> ExistingDialogues() => Existing;
            public void Begin() => Log.Add("begin");
            public void End() { Log.Add("end"); Ended = true; }
            public void EnsureLocales(IReadOnlyList<string> languages) { Languages = languages; Log.Add("locales"); }
            public void WriteDialogue(ManifestDialogue d, byte[] graph, IReadOnlyDictionary<string, IReadOnlyList<StringTableEntry>> texts, IReadOnlyList<SyncAudioFile> audio) { Log.Add("write " + d.name); Texts[d.Guid] = texts; Audio[d.Guid] = audio; }
            public void RemoveDialogue(Guid id) => Log.Add("remove " + id);
            public void WriteSpeakers(IReadOnlyDictionary<string, IReadOnlyList<StringTableEntry>> texts) => Log.Add("speakers " + string.Join(",", texts.Keys.OrderBy(k => k)));
            public void WriteCatalog(BuildManifest manifest) => Log.Add("catalog");
        }

        [Test]
        public void A_first_sync_writes_the_dialogue_the_speakers_and_the_catalog_with_the_texts_of_each_language()
        {
            using (var file = SampleBuild.Open())
            {
                var target = new FakeTarget();
                var report = BuildSynchronizer.Run(file, target);
                Assert.AreEqual(1, report.Added);
                Assert.IsFalse(report.WasUpToDate);
                CollectionAssert.AreEqual(new[] { "begin", "locales", "write Gate", "speakers en,ru", "catalog", "end" }, target.Log);
                CollectionAssert.AreEquivalent(new[] { "en", "ru", "de" }, target.Languages);
                var texts = target.Texts[SampleBuild.Gate(file.Manifest).Guid];
                CollectionAssert.AreEquivalent(new[] { "en", "ru" }, texts.Keys); // there is nothing in German
                Assert.AreEqual(6, texts["en"].Count);
                Assert.AreEqual(2, texts["ru"].Count);
            }
        }

        [Test]
        public void The_voice_overs_of_the_dialogue_go_to_the_target_with_a_way_to_read_each_recording()
        {
            using (var file = SampleBuild.Open())
            {
                var target = new FakeTarget();
                BuildSynchronizer.Run(file, target);
                var audio = target.Audio[SampleBuild.Gate(file.Manifest).Guid];
                Assert.AreEqual(3, audio.Count);
                CollectionAssert.AreEquivalent(new[] { "en", "ru", "ru" }, audio.Select(a => a.Language));
                foreach (var a in audio)
                {
                    Assert.AreEqual("wav", a.Extension);
                    var bytes = a.Read();
                    Assert.AreEqual("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4)); // the recording as it was uploaded
                }
            }
        }

        [Test]
        public void Syncing_the_same_build_again_writes_nothing()
        {
            using (var file = SampleBuild.Open())
            {
                var gate = SampleBuild.Gate(file.Manifest);
                var target = new FakeTarget { Hash = file.Manifest.contentHash, Existing = new Dictionary<Guid, string> { [gate.Guid] = gate.hash } };
                var report = BuildSynchronizer.Run(file, target);
                Assert.IsTrue(report.WasUpToDate);
                Assert.AreEqual(1, report.Unchanged);
                Assert.IsEmpty(target.Log);
            }
        }

        [Test]
        public void A_changed_dialogue_is_written_again_and_one_that_is_gone_is_removed()
        {
            using (var file = SampleBuild.Open())
            {
                var gate = SampleBuild.Gate(file.Manifest);
                var gone = Guid.NewGuid();
                var target = new FakeTarget { Hash = "older", Existing = new Dictionary<Guid, string> { [gate.Guid] = "old hash", [gone] = "x" } };
                var report = BuildSynchronizer.Run(file, target);
                Assert.AreEqual(1, report.Updated);
                Assert.AreEqual(1, report.Removed);
                Assert.Contains("write Gate", target.Log);
                Assert.Contains("remove " + gone, target.Log);
            }
        }

        [Test]
        public void A_forced_sync_writes_everything_even_when_nothing_changed()
        {
            using (var file = SampleBuild.Open())
            {
                var gate = SampleBuild.Gate(file.Manifest);
                var target = new FakeTarget { Hash = file.Manifest.contentHash, Existing = new Dictionary<Guid, string> { [gate.Guid] = gate.hash } };
                BuildSynchronizer.Run(file, target, force: true);
                Assert.Contains("write Gate", target.Log);
            }
        }

        [Test]
        public void The_target_is_always_told_that_the_sync_is_over()
        {
            using (var file = SampleBuild.Open())
            {
                var target = new ThrowingTarget();
                Assert.Throws<InvalidOperationException>(() => BuildSynchronizer.Run(file, target));
                Assert.IsTrue(target.Ended);
            }
        }

        private sealed class ThrowingTarget : ISyncTarget
        {
            public bool Ended;
            public string ContentHash => "";
            public IReadOnlyDictionary<Guid, string> ExistingDialogues() => new Dictionary<Guid, string>();
            public void Begin() { }
            public void End() => Ended = true;
            public void EnsureLocales(IReadOnlyList<string> languages) => throw new InvalidOperationException("no locale");
            public void WriteDialogue(ManifestDialogue d, byte[] graph, IReadOnlyDictionary<string, IReadOnlyList<StringTableEntry>> texts, IReadOnlyList<SyncAudioFile> audio) { }
            public void RemoveDialogue(Guid id) { }
            public void WriteSpeakers(IReadOnlyDictionary<string, IReadOnlyList<StringTableEntry>> texts) { }
            public void WriteCatalog(BuildManifest manifest) { }
        }
    }
}

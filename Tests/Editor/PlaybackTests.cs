using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Loreman.Dialogues.Tests
{
    /// <summary>Loads from memory and counts what is loaded and released, so the tests can see that nothing leaks.</summary>
    internal sealed class FakeLoader : IAssetLoader
    {
        public readonly Dictionary<string, Object> Assets = new Dictionary<string, Object>();
        public readonly List<string> Loads = new List<string>();
        public readonly List<Object> Released = new List<Object>();
        public TaskCompletionSource<bool> Gate; // when set, loads wait for it

        public async Task<T> LoadAsync<T>(string address, CancellationToken cancellationToken) where T : Object
        {
            Loads.Add(address);
            if (Gate != null) await Gate.Task;
            if (!Assets.TryGetValue(address, out var asset)) throw new InvalidOperationException("no such asset: " + address);
            return (T)asset;
        }

        public void Release(Object asset) => Released.Add(asset);
    }

    internal sealed class FakeTexts : IDialogueTextSource
    {
        public readonly Dictionary<long, string> Texts = new Dictionary<long, string>();
        public readonly Dictionary<long, string> Speakers = new Dictionary<long, string>();
        public readonly List<string> Calls = new List<string>();
        public int Prepared;
        public event Action TextsChanged;

        public Task InitializeAsync(DialogueCatalog catalog, CancellationToken cancellationToken) { Calls.Add("init"); return Task.CompletedTask; }
        public Task PrepareAsync(Guid dialogueId, CancellationToken cancellationToken) { Prepared++; Calls.Add("prepare"); return Task.CompletedTask; }
        public void Release(Guid dialogueId) { Prepared--; Calls.Add("release"); }
        public string GetText(Guid dialogueId, TextKey key) => Texts.TryGetValue(key.Id, out var t) ? t : "";
        public string GetSpeakerText(TextKey key) => Speakers.TryGetValue(key.Id, out var t) ? t : "";
        public void Dispose() { }
        public void Change() => TextsChanged?.Invoke();
    }

    public class PlaybackTests
    {
        private readonly List<Object> _made = new List<Object>();
        private Guid _id;
        private FakeLoader _loader;
        private FakeTexts _texts;

        [SetUp]
        public void SetUp()
        {
            _id = Guid.NewGuid();
            _loader = new FakeLoader();
            _texts = new FakeTexts();

            // line (Guard) → choice (answers: "Yes" → end, "No" → end)
            byte[] graph;
            using (var file = SampleBuild.Open())
                graph = file.Read(SampleBuild.Gate(file.Manifest).graph);
            var dialogue = DialogueGraphReader.Read(graph);
            _id = dialogue.Id;

            var asset = ScriptableObject.CreateInstance<DialogueAsset>();
            asset.Populate(_id, "h", graph);
            var catalog = ScriptableObject.CreateInstance<DialogueCatalog>();
            using (var file = SampleBuild.Open())
            {
                var m = file.Manifest;
                catalog.Populate(m.projectName, m.contentHash, m.baseLanguage, new[] { "en" }, m.speakers, m.groups,
                    m.dialogues.Select(d => new CatalogDialogue { id = d.id, name = d.name, hash = d.hash, nodes = d.nodes }));
                _texts.Speakers[m.speakers[dialogue.Nodes[0].Speaker].nameKey] = "Guard";
                _texts.Speakers[m.speakers[dialogue.Nodes[1].AnswerSpeaker].nameKey] = "Hero";
            }
            _texts.Texts[dialogue.Nodes[0].Text.Id] = "Halt!";
            _texts.Texts[dialogue.Nodes[1].Text.Id] = "Pass?";
            _texts.Texts[dialogue.Nodes[1].Options[0].Text.Id] = "Of course";
            _texts.Texts[dialogue.Nodes[1].Options[1].Text.Id] = "Never";
            _texts.Texts[dialogue.Nodes[2].Text.Id] = "Move along.";

            _loader.Assets[LoremanNames.CatalogAddress] = catalog;
            _loader.Assets[LoremanNames.DialogueAddress(_id)] = asset;
            _made.Add(asset);
            _made.Add(catalog);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // waits for the task, and throws what the task threw (not an AggregateException around it)
        private static T Run<T>(Task<T> task)
        {
            Run((Task)task);
            return task.Result;
        }

        private static void Run(Task task)
        {
            try
            {
                Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(5)), "the task did not finish");
            }
            catch (AggregateException e)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            }
        }

        private DialogueService Service()
        {
            var s = new DialogueService(_loader, _texts);
            Run(s.InitializeAsync());
            return s;
        }

        // ── library ──

        [Test]
        public void A_dialogue_is_loaded_only_when_asked_for_and_only_once()
        {
            var library = new DialogueLibrary(_loader);
            Run(library.InitializeAsync(CancellationToken.None));
            Assert.AreEqual(0, library.LoadedCount);
            CollectionAssert.AreEqual(new[] { LoremanNames.CatalogAddress }, _loader.Loads);

            var a = Run(library.AcquireAsync(_id, CancellationToken.None));
            var b = Run(library.AcquireAsync(_id, CancellationToken.None));
            Assert.AreEqual(1, library.LoadedCount);
            Assert.AreEqual(1, _loader.Loads.Count(l => l == LoremanNames.DialogueAddress(_id)));
            Assert.AreSame(a.Graph, b.Graph);
        }

        [Test]
        public void A_dialogue_is_let_go_when_the_last_lease_is_disposed()
        {
            var library = new DialogueLibrary(_loader);
            Run(library.InitializeAsync(CancellationToken.None));
            var a = Run(library.AcquireAsync(_id, CancellationToken.None));
            var b = Run(library.AcquireAsync(_id, CancellationToken.None));
            a.Dispose();
            a.Dispose(); // twice is the same as once
            Assert.IsTrue(library.IsLoaded(_id));
            Assert.IsEmpty(_loader.Released);
            b.Dispose();
            Assert.IsFalse(library.IsLoaded(_id));
            Assert.AreEqual(1, _loader.Released.Count);
        }

        [Test]
        public void Two_requests_made_while_the_first_is_loading_share_one_load()
        {
            var library = new DialogueLibrary(_loader);
            Run(library.InitializeAsync(CancellationToken.None));
            _loader.Gate = new TaskCompletionSource<bool>();
            var first = library.AcquireAsync(_id, CancellationToken.None);
            var second = library.AcquireAsync(_id, CancellationToken.None);
            _loader.Gate.SetResult(true);
            Run(first);
            Run(second);
            Assert.AreEqual(1, _loader.Loads.Count(l => l == LoremanNames.DialogueAddress(_id)));
            first.Result.Dispose();
            Assert.IsTrue(library.IsLoaded(_id)); // the second still holds it
            second.Result.Dispose();
            Assert.IsFalse(library.IsLoaded(_id));
        }

        [Test]
        public void A_dialogue_the_build_does_not_have_is_refused_and_so_is_asking_before_the_start()
        {
            var library = new DialogueLibrary(_loader);
            Assert.Throws<InvalidOperationException>(() => Run(library.AcquireAsync(_id, CancellationToken.None)));
            Run(library.InitializeAsync(CancellationToken.None));
            Assert.Throws<KeyNotFoundException>(() => Run(library.AcquireAsync(Guid.NewGuid(), CancellationToken.None)));
        }

        [Test]
        public void A_load_that_fails_leaves_nothing_behind_and_can_be_tried_again()
        {
            var library = new DialogueLibrary(_loader);
            Run(library.InitializeAsync(CancellationToken.None));
            var asset = _loader.Assets[LoremanNames.DialogueAddress(_id)];
            _loader.Assets.Remove(LoremanNames.DialogueAddress(_id));
            Assert.Throws<InvalidOperationException>(() => Run(library.AcquireAsync(_id, CancellationToken.None)));
            Assert.AreEqual(0, library.LoadedCount);
            _loader.Assets[LoremanNames.DialogueAddress(_id)] = asset;
            Run(library.AcquireAsync(_id, CancellationToken.None)).Dispose();
        }

        [Test]
        public void Disposing_the_library_lets_everything_go()
        {
            var library = new DialogueLibrary(_loader);
            Run(library.InitializeAsync(CancellationToken.None));
            Run(library.AcquireAsync(_id, CancellationToken.None));
            library.Dispose();
            Assert.AreEqual(2, _loader.Released.Count); // the dialogue and the catalog
        }

        // ── service and conversation ──

        [Test]
        public void A_conversation_shows_lines_with_their_speakers_and_leads_through_a_choice()
        {
            var service = Service();
            using (var talk = Run(service.StartAsync(_id)))
            {
                var shown = new List<DialogueView>();
                var finished = 0;
                talk.Presented += shown.Add;
                talk.Finished += () => finished++;

                talk.Start();
                Assert.AreEqual("Halt!", talk.Current.Text);
                Assert.AreEqual("Guard", talk.Current.Speaker.Name);
                Assert.AreEqual(Color.red, talk.Current.Speaker.Color);
                Assert.AreEqual("shout", talk.Current.Note);
                Assert.IsFalse(talk.Current.IsChoice);

                talk.Advance();
                Assert.IsTrue(talk.Current.IsChoice);
                Assert.AreEqual("Pass?", talk.Current.Text);
                Assert.AreEqual("Hero", talk.Current.AnswerSpeaker.Name);
                CollectionAssert.AreEqual(new[] { "Of course", "Never" }, talk.Current.Options.Select(o => o.Text));
                CollectionAssert.AreEqual(new[] { 0, 1 }, talk.Current.Options.Select(o => o.Index));

                talk.Choose(0);
                Assert.AreEqual("Move along.", talk.Current.Text);
                talk.Advance();
                Assert.IsTrue(talk.IsEnded);
                Assert.IsNull(talk.Current);
                Assert.AreEqual(1, finished);
                Assert.AreEqual(3, shown.Count);
            }
        }

        [Test]
        public void Disposing_a_conversation_lets_the_dialogue_and_its_texts_go()
        {
            var service = Service();
            var talk = Run(service.StartAsync(_id));
            Assert.AreEqual(1, service.LoadedCount);
            Assert.AreEqual(1, _texts.Prepared);
            talk.Dispose();
            talk.Dispose();
            Assert.AreEqual(0, service.LoadedCount);
            Assert.AreEqual(0, _texts.Prepared);
        }

        [Test]
        public void Changing_the_language_shows_the_same_line_again_in_the_new_words()
        {
            var service = Service();
            using (var talk = Run(service.StartAsync(_id)))
            {
                var refreshed = new List<DialogueView>();
                talk.Refreshed += refreshed.Add;
                talk.Start();
                var node = talk.Current.NodeId;
                _texts.Texts[_texts.Texts.Keys.First()] = "Стой!";
                _texts.Change();
                Assert.AreEqual(1, refreshed.Count);
                Assert.AreEqual(node, refreshed[0].NodeId);
                Assert.AreEqual("Стой!", talk.Current.Text);
            }
        }

        [Test]
        public void A_preloaded_dialogue_stays_until_the_handle_is_disposed()
        {
            var service = Service();
            var handle = Run(service.PreloadAsync(_id));
            Assert.AreEqual(1, service.LoadedCount);
            using (Run(service.StartAsync(_id))) { }
            Assert.AreEqual(1, service.LoadedCount); // the preload still holds it
            handle.Dispose();
            Assert.AreEqual(0, service.LoadedCount);
        }

        [Test]
        public void Nothing_starts_before_the_service_is_initialized()
        {
            var service = new DialogueService(_loader, _texts);
            Assert.Throws<InvalidOperationException>(() => Run(service.StartAsync(_id)));
        }

        [Test]
        public void A_conversation_can_carry_on_from_a_saved_node()
        {
            var service = Service();
            using (var talk = Run(service.StartAsync(_id)))
            {
                talk.Start();
                talk.Advance();
                var saved = talk.Current.NodeId;
                talk.Stop();
                Assert.IsTrue(talk.IsEnded);
                Assert.IsTrue(talk.JumpTo(saved));
                Assert.IsTrue(talk.Current.IsChoice);
            }
        }
    }
}

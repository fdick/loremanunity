using System;
using System.Collections.Generic;
using UnityEngine;

namespace Loreman.Dialogues
{
    /// <summary>
    /// A dialogue being played: the walk through the graph (<see cref="DialogueSession"/>) joined with the words in the current language. The game calls
    /// <see cref="Advance"/> and <see cref="Choose"/>; the screen listens to <see cref="Presented"/>. Dispose it when the dialogue is over: the graph and
    /// the texts are let go. Made by <see cref="DialogueService.StartAsync"/>.
    /// </summary>
    public sealed class DialogueConversation : IDisposable
    {
        private readonly DialogueSession _session;
        private readonly DialogueCatalog _catalog;
        private readonly IDialogueTextSource _texts;
        private readonly DialogueLease _lease;
        private bool _disposed;

        internal DialogueConversation(DialogueLease lease, DialogueCatalog catalog, IDialogueTextSource texts)
        {
            _lease = lease;
            _catalog = catalog;
            _texts = texts;
            _session = new DialogueSession(lease.Graph);
            _session.NodeEntered += OnNodeEntered;
            _session.Ended += OnEnded;
            _texts.TextsChanged += OnTextsChanged;
        }

        public Guid DialogueId => _lease.Id;

        /// <summary>What is on the screen now (null before <see cref="Start"/> and after the end).</summary>
        public DialogueView Current { get; private set; }

        public bool IsEnded => _session.State == DialogueSessionState.Ended;

        /// <summary>A new line or choice came: show it.</summary>
        public event Action<DialogueView> Presented;

        /// <summary>The language was changed: the same line or choice again, in the new language.</summary>
        public event Action<DialogueView> Refreshed;

        /// <summary>The dialogue is over.</summary>
        public event Action Finished;

        public void Start() => _session.Start();

        /// <summary>Goes on after a line.</summary>
        public void Advance() => _session.Advance();

        /// <summary>Picks an answer of the choice on the screen (<see cref="OptionView.Index"/>).</summary>
        public void Choose(int optionIndex) => _session.Choose(optionIndex);

        /// <summary>Carries on from a node saved earlier (<see cref="DialogueView.NodeId"/>). False when the dialogue has no such node any more.</summary>
        public bool JumpTo(Guid nodeId) => _session.JumpTo(nodeId);

        /// <summary>Ends the dialogue now.</summary>
        public void Stop() => _session.End();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _session.NodeEntered -= OnNodeEntered;
            _session.Ended -= OnEnded;
            _texts.TextsChanged -= OnTextsChanged;
            _texts.Release(_lease.Id);
            _lease.Dispose();
        }

        private void OnNodeEntered(DialogueNode node)
        {
            Current = Build(node);
            Presented?.Invoke(Current);
        }

        private void OnEnded()
        {
            Current = null;
            Finished?.Invoke();
        }

        private void OnTextsChanged()
        {
            var node = _session.Current;
            if (node == null || _disposed) return;
            Current = Build(node);
            Refreshed?.Invoke(Current);
        }

        private DialogueView Build(DialogueNode node)
        {
            var id = _lease.Id;
            IReadOnlyList<OptionView> options = null;
            if (node.IsChoice)
            {
                var list = new List<OptionView>(node.Options.Count);
                for (var i = 0; i < node.Options.Count; i++)
                {
                    var o = node.Options[i];
                    list.Add(new OptionView(i, _texts.GetText(id, o.Text), _texts.GetText(id, o.Label)));
                }
                options = list;
            }
            return new DialogueView(
                node.Id, node.IsChoice, Speaker(node.Speaker), node.IsChoice ? Speaker(node.AnswerSpeaker) : default, _texts.GetText(id, node.Text), node.Note,
                node.IsChoice ? string.Empty : _texts.GetText(id, node.NextLabel), options);
        }

        private SpeakerView Speaker(int index)
        {
            if (!_catalog.TryGetSpeaker(index, out var s)) return new SpeakerView(Guid.Empty, string.Empty, string.Empty, Color.white);
            return new SpeakerView(s.Id, _texts.GetSpeakerText(s.Name), _texts.GetSpeakerText(s.Note), s.Color);
        }
    }
}

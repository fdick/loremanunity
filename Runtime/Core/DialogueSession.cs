using System;

namespace Loreman.Dialogues
{
    public enum DialogueSessionState
    {
        NotStarted,
        AtLine,
        AtChoice,
        Ended,
    }

    /// <summary>
    /// Walks through the graph of a dialogue: the state machine and nothing else. It knows no texts, no speakers and no Unity: what is shown is up to
    /// the layer above (<c>DialogueConversation</c>). That is why it is small, and why it can be tested without the editor.
    /// </summary>
    public sealed class DialogueSession
    {
        private readonly DialogueGraph _graph;
        private int _current = -1;

        public DialogueSession(DialogueGraph graph)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        }

        public DialogueGraph Graph => _graph;

        public DialogueSessionState State { get; private set; } = DialogueSessionState.NotStarted;

        /// <summary>The node the dialogue is at (null before <see cref="Start"/> and after the end).</summary>
        public DialogueNode Current => _current < 0 ? null : _graph.Nodes[_current];

        /// <summary>The dialogue came to a node (a line to show, a choice to make).</summary>
        public event Action<DialogueNode> NodeEntered;

        /// <summary>The dialogue is over.</summary>
        public event Action Ended;

        /// <summary>Begins (or begins again) at the first node. A dialogue with nothing in it ends at once.</summary>
        public void Start() => Enter(_graph.Entry);

        /// <summary>Goes on after a line. After a choice only <see cref="Choose"/> goes on (a choice without answers is the exception: nothing is left to pick, so this ends it).</summary>
        public void Advance()
        {
            var node = Current;
            switch (State)
            {
                case DialogueSessionState.AtLine:
                    Enter(node.Next);
                    break;
                case DialogueSessionState.AtChoice when node.Options.Count == 0:
                    Enter(-1);
                    break;
                case DialogueSessionState.AtChoice:
                    throw new InvalidOperationException("The dialogue is at a choice: call Choose with the number of an answer.");
                default:
                    throw new InvalidOperationException($"The dialogue cannot advance: it is {State}.");
            }
        }

        /// <summary>Picks an answer of the choice the dialogue is at (0 is the first answer).</summary>
        public void Choose(int optionIndex)
        {
            if (State != DialogueSessionState.AtChoice) throw new InvalidOperationException($"The dialogue cannot choose: it is {State}.");
            var options = Current.Options;
            if (optionIndex < 0 || optionIndex >= options.Count) throw new ArgumentOutOfRangeException(nameof(optionIndex), $"The choice has {options.Count} answers.");
            Enter(options[optionIndex].Next);
        }

        /// <summary>Goes to a node by its id: to carry on from a saved place. Returns false (and changes nothing) when the dialogue has no such node.</summary>
        public bool JumpTo(Guid nodeId)
        {
            if (!_graph.TryGetNode(nodeId, out var node)) return false;
            Enter(node.Index);
            return true;
        }

        /// <summary>Ends the dialogue wherever it is.</summary>
        public void End()
        {
            if (State != DialogueSessionState.Ended) Enter(-1);
        }

        private void Enter(int index)
        {
            if (index < 0)
            {
                _current = -1;
                State = DialogueSessionState.Ended;
                Ended?.Invoke();
                return;
            }
            _current = index;
            var node = _graph.Nodes[index];
            State = node.IsChoice ? DialogueSessionState.AtChoice : DialogueSessionState.AtLine;
            NodeEntered?.Invoke(node);
        }
    }
}

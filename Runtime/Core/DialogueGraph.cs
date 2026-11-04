using System;
using System.Collections.Generic;

namespace Loreman.Dialogues
{
    public enum DialogueNodeKind : byte
    {
        /// <summary>Somebody says something, then the dialogue goes on to <see cref="DialogueNode.Next"/>.</summary>
        Line = 0,

        /// <summary>A question with answers: the player picks one, and every answer leads to its own next node.</summary>
        Choice = 1,
    }

    /// <summary>One answer of a choice.</summary>
    public sealed class DialogueOption
    {
        public DialogueOption(Guid id, TextKey text, int next, TextKey label, TextKey audio = default)
        {
            Id = id;
            Text = text;
            Next = next;
            Label = label;
            Audio = audio;
        }

        public Guid Id { get; }

        /// <summary>The words of the answer.</summary>
        public TextKey Text { get; }

        /// <summary>The index of the node the answer leads to, or -1 when it ends the dialogue.</summary>
        public int Next { get; }

        /// <summary>The words on the arrow that leaves the answer, or <see cref="TextKey.None"/>.</summary>
        public TextKey Label { get; }

        /// <summary>Where the voice-over of the answer is (the key in the audio table), or <see cref="TextKey.None"/> when it has none.</summary>
        public TextKey Audio { get; }

        public bool HasAudio => !Audio.IsNone;
    }

    /// <summary>A line or a choice of a dialogue. Nodes are read-only: a graph is made once, when it is loaded.</summary>
    public sealed class DialogueNode
    {
        private static readonly IReadOnlyList<DialogueOption> NoOptions = Array.Empty<DialogueOption>();

        private DialogueNode(
            int index, DialogueNodeKind kind, Guid id, int speaker, TextKey text, string note, int next, TextKey nextLabel, int answerSpeaker, IReadOnlyList<DialogueOption> options,
            TextKey audio)
        {
            Audio = audio;
            Index = index;
            Kind = kind;
            Id = id;
            Speaker = speaker;
            Text = text;
            Note = note ?? string.Empty;
            Next = next;
            NextLabel = nextLabel;
            AnswerSpeaker = answerSpeaker;
            Options = options ?? NoOptions;
        }

        /// <summary>A line. <paramref name="next"/> is the index of the node after it, -1 when the dialogue ends here.</summary>
        public static DialogueNode CreateLine(int index, Guid id, int speaker, TextKey text, string note, int next, TextKey nextLabel, TextKey audio = default) =>
            new DialogueNode(index, DialogueNodeKind.Line, id, speaker, text, note, next, nextLabel, -1, null, audio);

        /// <summary>A choice: <paramref name="text"/> is the question, the options are the answers.</summary>
        public static DialogueNode CreateChoice(int index, Guid id, int speaker, TextKey text, string note, int answerSpeaker, IReadOnlyList<DialogueOption> options, TextKey audio = default) =>
            new DialogueNode(index, DialogueNodeKind.Choice, id, speaker, text, note, -1, TextKey.None, answerSpeaker, options, audio);

        /// <summary>The place of the node in <see cref="DialogueGraph.Nodes"/>.</summary>
        public int Index { get; }

        public DialogueNodeKind Kind { get; }

        public bool IsChoice => Kind == DialogueNodeKind.Choice;

        /// <summary>The id the node has in Loreman: it stays the same between builds, so the game may keep it (a save, a quest step).</summary>
        public Guid Id { get; }

        /// <summary>Who speaks: the index in the speakers of the catalog, or -1 for nobody.</summary>
        public int Speaker { get; }

        /// <summary>What is said (a line), or the question (a choice).</summary>
        public TextKey Text { get; }

        /// <summary>Where the voice-over of the line (or of the question) is, or <see cref="TextKey.None"/> when it has none in any language.</summary>
        public TextKey Audio { get; }

        public bool HasAudio => !Audio.IsNone;

        /// <summary>The note of the designer, as it was written in Loreman (conditions, effects…). The package does not read it: the game may.</summary>
        public string Note { get; }

        /// <summary>The next node of a line (-1: the dialogue ends). A choice has none: its answers lead on.</summary>
        public int Next { get; }

        /// <summary>The words on the arrow that leaves the line, or <see cref="TextKey.None"/>.</summary>
        public TextKey NextLabel { get; }

        /// <summary>Who says the answers of a choice (the index in the speakers of the catalog, -1 for nobody).</summary>
        public int AnswerSpeaker { get; }

        /// <summary>The answers of a choice (empty for a line).</summary>
        public IReadOnlyList<DialogueOption> Options { get; }
    }

    /// <summary>One dialogue: nodes joined by links, read from the binary graph of a build. Texts are not here, only their keys.</summary>
    public sealed class DialogueGraph
    {
        private readonly Dictionary<Guid, int> _byId;

        public DialogueGraph(Guid id, int entry, IReadOnlyList<DialogueNode> nodes)
        {
            Id = id;
            Entry = entry;
            Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _byId = new Dictionary<Guid, int>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                _byId[nodes[i].Id] = i;
                if (nodes[i].HasAudio) HasAudio = true;
                foreach (var option in nodes[i].Options)
                    if (option.HasAudio) HasAudio = true;
            }
        }

        public Guid Id { get; }

        /// <summary>The index of the first node (the one Start leads to), or -1 when the dialogue is empty.</summary>
        public int Entry { get; }

        public IReadOnlyList<DialogueNode> Nodes { get; }

        /// <summary>True when some line or answer has a voice-over (in some language). A dialogue without any needs no audio table.</summary>
        public bool HasAudio { get; }

        public bool TryGetNode(Guid nodeId, out DialogueNode node)
        {
            if (_byId.TryGetValue(nodeId, out var index))
            {
                node = Nodes[index];
                return true;
            }
            node = null;
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Loreman.Dialogues
{
    /// <summary>Who speaks, as the screen needs it.</summary>
    public readonly struct SpeakerView
    {
        public SpeakerView(Guid id, string name, string note, Color color)
        {
            Id = id;
            Name = name;
            Note = note;
            Color = color;
        }

        /// <summary>The id of the speaker in Loreman (an empty id: nobody speaks).</summary>
        public Guid Id { get; }

        public string Name { get; }

        public string Note { get; }

        public Color Color { get; }

        public bool IsNobody => Id == Guid.Empty;
    }

    /// <summary>One answer of a choice, ready to be shown.</summary>
    public readonly struct OptionView
    {
        public OptionView(int index, string text, string label, bool hasAudio = false)
        {
            Index = index;
            Text = text;
            Label = label;
            HasAudio = hasAudio;
        }

        /// <summary>The number to give to <see cref="DialogueConversation.Choose"/>.</summary>
        public int Index { get; }

        public string Text { get; }

        /// <summary>The words on the arrow that leaves the answer (empty when there are none).</summary>
        public string Label { get; }

        /// <summary>The answer has a voice-over (load it with <see cref="DialogueConversation.LoadAudioAsync"/>; it may still be missing in the current language).</summary>
        public bool HasAudio { get; }
    }

    /// <summary>What the screen shows at one moment of a dialogue: a line, or a question with its answers. Texts are in the current language.</summary>
    public sealed class DialogueView
    {
        private static readonly IReadOnlyList<OptionView> NoOptions = Array.Empty<OptionView>();

        public DialogueView(
            Guid nodeId, bool isChoice, SpeakerView speaker, SpeakerView answerSpeaker, string text, string note, string arrowLabel, IReadOnlyList<OptionView> options,
            bool hasAudio = false)
        {
            HasAudio = hasAudio;
            NodeId = nodeId;
            IsChoice = isChoice;
            Speaker = speaker;
            AnswerSpeaker = answerSpeaker;
            Text = text;
            Note = note;
            ArrowLabel = arrowLabel;
            Options = options ?? NoOptions;
        }

        /// <summary>The id of the node in Loreman. Keep it to carry on from here later (<see cref="DialogueConversation.JumpTo"/>).</summary>
        public Guid NodeId { get; }

        public bool IsChoice { get; }

        /// <summary>Who says the line (or asks the question).</summary>
        public SpeakerView Speaker { get; }

        /// <summary>Who says the answers of a choice.</summary>
        public SpeakerView AnswerSpeaker { get; }

        /// <summary>The line (or the question) has a voice-over. Load it with <see cref="DialogueConversation.LoadAudioAsync"/>.</summary>
        public bool HasAudio { get; }

        /// <summary>The line, or the question.</summary>
        public string Text { get; }

        /// <summary>The note of the designer (conditions, effects…), as it was written in Loreman. The package does not read it.</summary>
        public string Note { get; }

        /// <summary>The words on the arrow that leaves a line (empty when there are none).</summary>
        public string ArrowLabel { get; }

        /// <summary>The answers of a choice (empty for a line).</summary>
        public IReadOnlyList<OptionView> Options { get; }
    }
}

using System;

namespace Loreman.Dialogues
{
    /// <summary>How long a dialogue takes when it goes on by itself (see <see cref="DialogueAutoAdvance"/>).</summary>
    public readonly struct DialogueLength
    {
        public DialogueLength(bool isKnown, double seconds, int lines, bool hasChoices, bool hasLoop)
        {
            IsKnown = isKnown;
            Seconds = seconds;
            Lines = lines;
            HasChoices = hasChoices;
            HasLoop = hasLoop;
        }

        /// <summary>False when it cannot be said: a dialogue the player goes through with Next takes as long as the player takes.</summary>
        public bool IsKnown { get; }

        /// <summary>The time of the longest way through the dialogue, without the loading of the recordings.</summary>
        public double Seconds { get; }

        /// <summary>The number of lines (and questions) on that way.</summary>
        public int Lines { get; }

        /// <summary>The dialogue has a choice. The player decides there, so the time is that of the longest answer, and the time the player thinks is not counted.</summary>
        public bool HasChoices { get; }

        /// <summary>The dialogue can go round in a circle (an answer leads back). The way is counted up to the second time round.</summary>
        public bool HasLoop { get; }
    }

    /// <summary>
    /// Counts how long a dialogue takes with an automatic advance. Pure: the length of a recording is asked through a function, so it is tested without Unity.
    /// A line takes its <c>seconds</c> (<see cref="DialogueAutoAdvance.AfterSeconds"/>), or its recording plus <c>seconds</c> (<see cref="DialogueAutoAdvance.AfterVoiceOver"/>;
    /// a line without a recording takes <c>seconds</c>). A choice waits for the player: only its question is counted, and then the longest answer.
    /// </summary>
    public static class DialogueLengthEstimator
    {
        private struct Way
        {
            public double Seconds;
            public int Lines;
        }

        /// <param name="voiceSeconds">The length of the recording of a line or question (0 when there is none).</param>
        public static DialogueLength Estimate(DialogueGraph graph, DialogueAutoAdvance mode, float seconds, Func<TextKey, double> voiceSeconds = null)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (mode == DialogueAutoAdvance.Manual) return default;
            if (graph.Entry < 0) return new DialogueLength(true, 0d, 0, false, false);

            var walker = new Walker(graph, mode, Math.Max(0f, seconds), voiceSeconds ?? (_ => 0d));
            var way = walker.Walk(graph.Entry);
            return new DialogueLength(true, way.Seconds, way.Lines, walker.HasChoices, walker.HasLoop);
        }

        private sealed class Walker
        {
            private readonly DialogueGraph _graph;
            private readonly DialogueAutoAdvance _mode;
            private readonly double _seconds;
            private readonly Func<TextKey, double> _voice;
            private readonly Way[] _memo;
            private readonly byte[] _state; // 0 not seen, 1 on the way, 2 done

            public Walker(DialogueGraph graph, DialogueAutoAdvance mode, double seconds, Func<TextKey, double> voice)
            {
                _graph = graph;
                _mode = mode;
                _seconds = seconds;
                _voice = voice;
                _memo = new Way[graph.Nodes.Count];
                _state = new byte[graph.Nodes.Count];
            }

            public bool HasChoices { get; private set; }

            public bool HasLoop { get; private set; }

            public Way Walk(int index)
            {
                if (index < 0 || index >= _memo.Length) return default;
                if (_state[index] == 2) return _memo[index];
                if (_state[index] == 1)
                {
                    HasLoop = true;
                    return default;
                }
                _state[index] = 1;

                var node = _graph.Nodes[index];
                var own = _mode == DialogueAutoAdvance.AfterVoiceOver && node.HasAudio ? Math.Max(0d, _voice(node.Audio)) : 0d;
                Way rest = default;
                if (node.IsChoice)
                {
                    HasChoices = true; // the question is said, then the player decides: no time of its own
                    foreach (var option in node.Options)
                    {
                        var way = Walk(option.Next);
                        if (way.Seconds > rest.Seconds || (way.Seconds == rest.Seconds && way.Lines > rest.Lines)) rest = way;
                    }
                }
                else
                {
                    own += _seconds;
                    rest = Walk(node.Next);
                }

                var result = new Way { Seconds = own + rest.Seconds, Lines = 1 + rest.Lines };
                _memo[index] = result;
                _state[index] = 2;
                return result;
            }
        }
    }
}

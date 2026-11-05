using UnityEngine;
using UnityEngine.Playables;

namespace Loreman.Dialogues.Timeline
{
    /// <summary>The playable of a <see cref="DialogueClip"/>: tells <see cref="DialogueClipController"/> when the clip starts and ends. Nothing happens while the timeline is only previewed in the editor.</summary>
    public sealed class DialogueBehaviour : PlayableBehaviour
    {
        private DialogueClip _clip;
        private DialoguePlayer _player;
        private DialogueClipController _controller;
        private bool _warned;

        internal void Configure(DialogueClip clip, DialoguePlayer player)
        {
            _clip = clip;
            _player = player;
        }

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (!Application.isPlaying || _clip == null) return;
            if (_player == null)
            {
                if (!_warned && _clip.dialogue.IsSet)
                {
                    _warned = true;
                    Debug.LogWarning("Loreman: a dialogue clip has no Dialogue Player to play it. Bind a DialoguePlayer to its Dialogue Track.");
                }
                return;
            }
            _controller ??= new DialogueClipController(
                new DialoguePlayerCue(_player), new GraphTimeHold(playable.GetGraph()), _clip.dialogue.Guid, _clip.ending, _clip.autoAdvance, _clip.seconds);
            _controller.Begin();
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (_controller == null) return;
            // The director was paused or stopped, not the clip left: the dialogue goes on, and starts no second time when the director resumes.
            var graph = playable.GetGraph();
            if (graph.IsValid() && !graph.IsPlaying()) return;
            _controller.End();
        }

        public override void OnPlayableDestroy(Playable playable) => _controller?.End();
    }

    /// <summary>Holds the timeline by setting the speed of the root of its graph to zero: the graph keeps being evaluated (so the clips keep listening), the time does not move.</summary>
    internal sealed class GraphTimeHold : ITimeHold
    {
        private readonly PlayableGraph _graph;

        public GraphTimeHold(PlayableGraph graph)
        {
            _graph = graph;
        }

        public void Hold() => SetSpeed(0d);

        public void Release() => SetSpeed(1d);

        private void SetSpeed(double speed)
        {
            if (!_graph.IsValid() || _graph.GetRootPlayableCount() == 0) return;
            _graph.GetRootPlayable(0).SetSpeed(speed);
        }
    }
}

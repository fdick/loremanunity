using System;

namespace Loreman.Dialogues.Timeline
{
    /// <summary>What a dialogue clip needs of whatever plays the dialogue. <see cref="DialoguePlayerCue"/> is a <see cref="DialoguePlayer"/>; a test puts its own.</summary>
    public interface IDialogueCue
    {
        /// <summary>The dialogue is over (it ended, or it was stopped).</summary>
        event Action Finished;

        /// <summary>The dialogue could not be played.</summary>
        event Action<Exception> Failed;

        DialogueAutoAdvance AutoAdvance { get; }

        float AutoAdvanceSeconds { get; }

        void SetAutoAdvance(DialogueAutoAdvance mode, float seconds);

        void Play(Guid dialogueId);

        void Stop();
    }

    /// <summary>Stops the time of the timeline and lets it go on again. The graph keeps being evaluated while it is stopped.</summary>
    public interface ITimeHold
    {
        void Hold();

        void Release();
    }

    /// <summary>How a dialogue clip ends.</summary>
    public enum DialogueClipEnding
    {
        /// <summary>The timeline waits where the clip starts until the dialogue is over (the length of the clip does not matter), then goes on.</summary>
        WaitForDialogue,

        /// <summary>The timeline does not wait: the dialogue plays during the clip and is stopped when the clip ends (if it is not over by then).</summary>
        StopAtEndOfClip,
    }

    /// <summary>
    /// What happens at a dialogue clip, apart from Timeline and apart from Unity: start the dialogue when the clip starts, hold the timeline while the dialogue runs
    /// (or stop the dialogue when the clip ends), and never leave the timeline held for ever when the dialogue fails.
    /// </summary>
    public sealed class DialogueClipController
    {
        private readonly IDialogueCue _cue;
        private readonly ITimeHold _hold;
        private readonly Guid _dialogue;
        private readonly DialogueClipEnding _ending;
        private readonly DialogueAutoAdvance _autoAdvance;
        private readonly float _seconds;

        private bool _started;
        private bool _finished;
        private bool _holding;
        private DialogueAutoAdvance _previousAutoAdvance;
        private float _previousSeconds;

        public DialogueClipController(
            IDialogueCue cue, ITimeHold hold, Guid dialogue, DialogueClipEnding ending, DialogueAutoAdvance autoAdvance, float seconds)
        {
            _cue = cue ?? throw new ArgumentNullException(nameof(cue));
            _hold = hold ?? throw new ArgumentNullException(nameof(hold));
            _dialogue = dialogue;
            _ending = ending;
            _autoAdvance = autoAdvance;
            _seconds = seconds;
        }

        /// <summary>The clip has started and has not ended.</summary>
        public bool IsRunning => _started;

        /// <summary>The timeline is held by this clip.</summary>
        public bool IsHolding => _holding;

        /// <summary>The clip starts. Starting twice is the same as starting once.</summary>
        public void Begin()
        {
            if (_started) return;
            if (_dialogue == Guid.Empty) return; // a clip with no dialogue chosen does nothing
            _started = true;
            _finished = false;
            _previousAutoAdvance = _cue.AutoAdvance;
            _previousSeconds = _cue.AutoAdvanceSeconds;
            _cue.Finished += OnOver;
            _cue.Failed += OnFailed;
            _cue.SetAutoAdvance(_autoAdvance, _seconds);
            if (_ending == DialogueClipEnding.WaitForDialogue)
            {
                _holding = true;
                _hold.Hold();
            }
            _cue.Play(_dialogue);
        }

        /// <summary>The clip ends (the time passed its end, or the timeline was left or stopped). Ending without having started is nothing.</summary>
        public void End()
        {
            if (!_started) return;
            _started = false;
            _cue.Finished -= OnOver;
            _cue.Failed -= OnFailed;
            // A dialogue still running when the clip is over (the end of a clip that does not wait, a timeline that was stopped or left) is stopped.
            if (!_finished) _cue.Stop();
            ReleaseHold();
            _cue.SetAutoAdvance(_previousAutoAdvance, _previousSeconds);
        }

        private void OnOver()
        {
            _finished = true;
            ReleaseHold();
        }

        // A dialogue that cannot be played must not freeze the timeline.
        private void OnFailed(Exception e)
        {
            _finished = true;
            ReleaseHold();
        }

        private void ReleaseHold()
        {
            if (!_holding) return;
            _holding = false;
            _hold.Release();
        }
    }

    /// <summary>A <see cref="DialoguePlayer"/> as a cue for a clip.</summary>
    public sealed class DialoguePlayerCue : IDialogueCue
    {
        private readonly DialoguePlayer _player;

        public DialoguePlayerCue(DialoguePlayer player)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public event Action Finished
        {
            add => _player.Finished += value;
            remove => _player.Finished -= value;
        }

        public event Action<Exception> Failed
        {
            add => _player.Failed += value;
            remove => _player.Failed -= value;
        }

        public DialogueAutoAdvance AutoAdvance => _player.AutoAdvance;

        public float AutoAdvanceSeconds => _player.AutoAdvanceSeconds;

        public void SetAutoAdvance(DialogueAutoAdvance mode, float seconds) => _player.SetAutoAdvance(mode, seconds);

        public void Play(Guid dialogueId) => _player.Play(dialogueId);

        public void Stop() => _player.Stop();
    }
}

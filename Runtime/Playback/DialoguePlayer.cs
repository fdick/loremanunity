using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Loreman.Dialogues
{
    /// <summary>
    /// A component that plays dialogues for a scene, so that a screen (a Canvas, UI Toolkit, anything) only has to show them. It owns the
    /// <see cref="DialogueService"/>, opens and closes conversations, plays the voice-over of each line and passes on the events:
    /// <code>
    /// player.Presented += view => ...;   // a line, or a choice: show it
    /// player.Refreshed += view => ...;   // the language was changed: the same line in the new words
    /// player.Finished  += () => ...;
    /// player.Play();  player.Advance();  player.Choose(i);  player.Stop();
    /// </code>
    /// Put it on a GameObject, pick the dialogue (or give an id to <see cref="Play(Guid)"/>). Everything it does can also be done by hand with
    /// <see cref="DialogueService"/>; see the Basic Dialogue sample.
    /// </summary>
    [AddComponentMenu("Loreman/Dialogue Player")]
    public sealed class DialoguePlayer : MonoBehaviour
    {
        [Tooltip("The dialogue Play() starts. Empty: the first dialogue of the catalog.")]
        [SerializeField] private DialogueReference dialogue;

        [Tooltip("Plays the voice-overs. One is added when this is empty and a line has a recording.")]
        [SerializeField] private AudioSource voice;

        [Tooltip("Start the dialogue as soon as the player is ready.")]
        [SerializeField] private bool playOnStart;

        private DialogueAutoAdvance _autoAdvance;
        private float _autoSeconds = 3f;
        private float _shownFor;
        private int _voiceLoading;
        private DialogueService _service;
        private bool _ownsService;
        private Task _initialization;
        private DialogueConversation _talk;
        private CancellationTokenSource _lifetime = new CancellationTokenSource();
        private int _generation;

        /// <summary>The service and its catalog are loaded: dialogues can be played.</summary>
        public event Action Ready;

        /// <summary>A new line, or a choice, is to be shown.</summary>
        public event Action<DialogueView> Presented;

        /// <summary>The language was changed: the line that is shown is given again in the new language.</summary>
        public event Action<DialogueView> Refreshed;

        /// <summary>The dialogue is over (it ended, or <see cref="Stop"/> was called).</summary>
        public event Action Finished;

        /// <summary>Something went wrong (a dialogue could not be loaded…). It is also written to the Console.</summary>
        public event Action<Exception> Failed;

        public bool IsReady => _service != null && _service.IsInitialized;

        public bool IsPlaying => _talk != null;

        /// <summary>What is on the screen now (null when no dialogue is playing).</summary>
        public DialogueView Current => _talk?.Current;

        public DialogueService Service => _service;

        /// <summary>A voice-over is being loaded or is playing. The line it belongs to is not "said" yet.</summary>
        public bool IsVoiceBusy => _voiceLoading > 0 || (voice != null && voice.isPlaying);

        /// <summary>How the player goes on after a line by itself. Choices always wait for <see cref="Choose"/>.</summary>
        public DialogueAutoAdvance AutoAdvance => _autoAdvance;

        /// <summary>The time of <see cref="AutoAdvance"/> (the reading time of a line, or the pause after its voice-over).</summary>
        public float AutoAdvanceSeconds => _autoSeconds;

        /// <summary>
        /// Makes the player go on after each line by itself (a cutscene): after <paramref name="seconds"/> (<see cref="DialogueAutoAdvance.AfterSeconds"/>) or
        /// once the voice-over is said, plus <paramref name="seconds"/> (<see cref="DialogueAutoAdvance.AfterVoiceOver"/>). A choice still waits for the player.
        /// </summary>
        public void SetAutoAdvance(DialogueAutoAdvance mode, float seconds = 3f)
        {
            _autoAdvance = mode;
            _autoSeconds = Mathf.Max(0f, seconds);
        }

        /// <summary>The dialogues of all the builds in the project (empty until <see cref="Ready"/>).</summary>
        public IReadOnlyList<CatalogDialogue> Dialogues => IsReady ? _service.Dialogues : Array.Empty<CatalogDialogue>();

        /// <summary>
        /// Plays with a service of your own (a game that shares one service, a test). Call it before the player starts; the service is not disposed by the player.
        /// Without it the player makes one with Addressables and Unity Localization.
        /// </summary>
        public void UseService(DialogueService service)
        {
            if (_initialization != null) throw new InvalidOperationException("Loreman: UseService must be called before the player starts.");
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _ownsService = false;
        }

        private async void Start()
        {
            try
            {
                await InitializeAsync();
                if (playOnStart) Play();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Loads the catalog (once). Called by the player itself; await it when you need the player ready.</summary>
        public Task InitializeAsync() => _initialization ??= InitializeCoreAsync();

        private async Task InitializeCoreAsync()
        {
            if (_service == null)
            {
                _service = DialogueService.CreateDefault();
                _ownsService = true;
            }
            await _service.InitializeAsync(_lifetime.Token);
            Ready?.Invoke();
        }

        // ── playing ──

        /// <summary>Plays the dialogue chosen in the inspector (the first of the catalog when none is chosen).</summary>
        public void Play()
        {
            if (!IsReady) { PlayWhenReady(Guid.Empty); return; }
            Play(dialogue.IsSet ? dialogue.Guid : Dialogues.Count > 0 ? Dialogues[0].Guid : Guid.Empty);
        }

        public async void Play(Guid dialogueId)
        {
            try
            {
                await PlayAsync(dialogueId);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private async void PlayWhenReady(Guid dialogueId)
        {
            try
            {
                await InitializeAsync();
                if (dialogueId == Guid.Empty) Play();
                else await PlayAsync(dialogueId);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Loads the dialogue and starts it. Whatever was playing is stopped first.</summary>
        public async Task PlayAsync(Guid dialogueId)
        {
            await InitializeAsync();
            if (dialogueId == Guid.Empty) throw new InvalidOperationException("Loreman: there is no dialogue to play (the catalog is empty and none is chosen).");
            CloseConversation();
            var generation = ++_generation;

            var talk = await _service.StartAsync(dialogueId, _lifetime.Token);
            if (generation != _generation)
            {
                talk.Dispose(); // another dialogue was asked for, or Stop was called, while this one was loading
                return;
            }
            _talk = talk;
            talk.Presented += OnPresented;
            talk.Refreshed += OnRefreshed;
            talk.Finished += OnFinished;
            talk.Start();
        }

        /// <summary>Goes on after a line.</summary>
        public void Advance() => _talk?.Advance();

        /// <summary>Picks an answer of the choice that is shown (<see cref="OptionView.Index"/>).</summary>
        public void Choose(int optionIndex) => _talk?.Choose(optionIndex);

        /// <summary>Ends the dialogue now (<see cref="Finished"/> is raised).</summary>
        public void Stop()
        {
            ++_generation; // a dialogue that is still loading must not start
            var talk = _talk;
            if (talk == null) return;
            talk.Stop(); // raises Finished through OnFinished, which lets the conversation go
        }

        // ── events of the conversation ──

        private void OnPresented(DialogueView view)
        {
            _shownFor = 0f;
            Presented?.Invoke(view);
            PlayVoice(view);
        }

        private void OnRefreshed(DialogueView view)
        {
            Refreshed?.Invoke(view);
            PlayVoice(view);
        }

        private void OnFinished()
        {
            CloseConversation();
            Finished?.Invoke();
        }

        private void CloseConversation()
        {
            var talk = _talk;
            if (talk == null) return;
            _talk = null;
            talk.Presented -= OnPresented;
            talk.Refreshed -= OnRefreshed;
            talk.Finished -= OnFinished;
            if (voice != null) voice.Stop();
            talk.Dispose();
        }

        private async void PlayVoice(DialogueView view)
        {
            if (voice != null) voice.Stop();
            var talk = _talk;
            if (talk == null || !view.HasAudio) return;
            _voiceLoading++;
            try
            {
                var clip = await talk.LoadAudioAsync(view, -1, _lifetime.Token);
                if (clip == null || _talk != talk || talk.Current != view) return; // silent in this language, or the line has changed meanwhile
                if (voice == null) voice = gameObject.AddComponent<AudioSource>();
                voice.clip = clip; // a long recording is streamed, and a streamed clip is played as the clip of the source
                voice.Play();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Fail(e);
            }
            finally
            {
                _voiceLoading--;
            }
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        /// <summary>One step of the automatic advance (<see cref="Update"/> calls it; a test can too).</summary>
        internal void Tick(float deltaTime)
        {
            var view = Current;
            if (_autoAdvance == DialogueAutoAdvance.Manual || view == null || view.IsChoice) return;
            // a line with a voice-over is not over while the recording is loading or playing: the pause is counted from the end of the voice
            if (_autoAdvance == DialogueAutoAdvance.AfterVoiceOver && view.HasAudio && IsVoiceBusy)
            {
                _shownFor = 0f;
                return;
            }
            _shownFor += deltaTime;
            if (_shownFor < _autoSeconds) return;
            _shownFor = 0f;
            Advance();
        }

        private void Fail(Exception e)
        {
            Debug.LogException(e);
            Failed?.Invoke(e);
        }

        private void OnDestroy()
        {
            _lifetime.Cancel();
            _generation++;
            CloseConversation();
            if (_ownsService) _service?.Dispose();
            _lifetime.Dispose();
        }
    }
}

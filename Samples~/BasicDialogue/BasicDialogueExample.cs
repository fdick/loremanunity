using System;
using Loreman.Dialogues;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Loreman.Dialogues.Samples
{
    /// <summary>
    /// The smallest complete use of the package: start a dialogue, show its lines and choices, play the voice-over, switch the language while it runs.
    /// Open the scene of the sample and press Play. The screen is drawn with OnGUI, so the sample needs no UI package; in a real game you would do the same
    /// calls from your own UI (see <see cref="OnPresented"/>).
    /// </summary>
    public sealed class BasicDialogueExample : MonoBehaviour
    {
        [Tooltip("The dialogue to play. Leave it empty to play the first dialogue of the catalog (the sample's own City Gate, or your own build).")]
        [SerializeField] private DialogueReference dialogue;

        [Tooltip("Plays the voice-overs. One is added when this is empty.")]
        [SerializeField] private AudioSource voice;

        private DialogueService _service;
        private DialogueConversation _talk;
        private DialogueView _view;
        private string _error;

        private async void Start()
        {
            try
            {
                if (voice == null) voice = gameObject.AddComponent<AudioSource>();

                // 1. One service for the whole game. It loads the catalog and the names of the speakers, and nothing else.
                _service = DialogueService.CreateDefault();
                await _service.InitializeAsync();
            }
            catch (Exception e)
            {
                _error = e.Message + "\n\nIs the build synced? Tools → Loreman → Window.";
                Debug.LogException(e);
            }
        }

        private async void StartTalking()
        {
            try
            {
                // 2. Opening a dialogue loads its graph and its words, in the language being played. Only this dialogue's.
                var id = dialogue.IsSet ? dialogue.Guid : _service.Dialogues[0].Guid;
                _talk = await _service.StartAsync(id);
                _talk.Presented += OnPresented;   // a new line, or a choice
                _talk.Refreshed += OnRefreshed;   // the same line again, because the language was changed
                _talk.Finished += OnFinished;
                _talk.Start();
            }
            catch (Exception e)
            {
                _error = e.Message;
                Debug.LogException(e);
            }
        }

        private void OnPresented(DialogueView view)
        {
            _view = view;
            PlayVoice(view);
        }

        private void OnRefreshed(DialogueView view)
        {
            _view = view;
            PlayVoice(view); // the voice-over of the new language, if there is one
        }

        private void OnFinished()
        {
            _view = null;
            _talk.Dispose(); // lets the graph, the words and the clips go
            _talk = null;
        }

        private async void PlayVoice(DialogueView view)
        {
            voice.Stop();
            if (!view.HasAudio) return;
            try
            {
                // 3. A clip is loaded when it is asked for. It can be missing in this language: then it is null and the line is silent.
                var clip = await _talk.LoadAudioAsync(view);
                if (clip == null || _view != view) return;
                voice.clip = clip; // a long recording is streamed, and a streamed clip is played as the clip of the source
                voice.Play();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnGUI()
        {
            var scale = Mathf.Max(1f, Screen.height / 540f); // readable on a big or a high-resolution screen
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.skin.label.richText = true;
            GUI.skin.label.wordWrap = true;
            GUILayout.BeginArea(new Rect(20, 20, Mathf.Min(560, Screen.width / scale - 40), Screen.height / scale - 40));

            GUILayout.Label("<b>Loreman Dialogues — Basic Dialogue</b>");
            if (_error != null) GUILayout.Label("<color=#ff6b6b>" + _error + "</color>");
            else if (_service == null || !_service.IsInitialized) GUILayout.Label("Loading…");
            else if (_service.Dialogues.Count == 0) GUILayout.Label("The catalog has no dialogues.");
            else if (_talk == null)
            {
                GUILayout.Label("Dialogue: " + (dialogue.IsSet ? dialogue.ToString() : _service.Dialogues[0].name));
                if (GUILayout.Button("Talk")) StartTalking();
            }
            else if (_view != null) DrawView(_view);

            DrawLanguages();
            GUILayout.EndArea();
        }

        private void DrawView(DialogueView view)
        {
            GUILayout.Space(8);
            var speaker = view.Speaker;
            if (!speaker.IsNobody) GUILayout.Label("<b><color=#" + ColorUtility.ToHtmlStringRGB(speaker.Color) + ">" + speaker.Name + "</color></b>");
            GUILayout.Label(view.Text + (view.HasAudio ? "   ♪" : string.Empty));

            if (!view.IsChoice)
            {
                if (GUILayout.Button("Next")) _talk.Advance();
                return;
            }
            var answerer = view.AnswerSpeaker;
            if (!answerer.IsNobody) GUILayout.Label("<color=#" + ColorUtility.ToHtmlStringRGB(answerer.Color) + ">" + answerer.Name + "…</color>");
            foreach (var option in view.Options)
                if (GUILayout.Button(option.Text + (option.HasAudio ? "   ♪" : string.Empty))) _talk.Choose(option.Index); // 4. an answer leads to its own next line
        }

        private void DrawLanguages()
        {
            if (_service == null || !_service.IsInitialized) return;
            GUILayout.Space(14);
            GUILayout.Label("Language (changes the open line at once):");
            GUILayout.BeginHorizontal();
            // 5. The language is Unity Localization's own. Change it and every open dialogue says the same line in the new language.
            foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
                if (GUILayout.Button(locale.Identifier.Code)) LocalizationSettings.SelectedLocale = locale;
            GUILayout.EndHorizontal();
        }

        private void OnDestroy()
        {
            _talk?.Dispose();
            _service?.Dispose();
        }
    }
}

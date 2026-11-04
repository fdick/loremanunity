using System;
using Loreman.Dialogues;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Loreman.Dialogues.Samples
{
    /// <summary>
    /// The smallest complete use of the package: start a dialogue, show its lines and choices, play the voice-over, switch the language while it runs.
    /// Put this component on an empty GameObject, pick the dialogue in the inspector and press Play. The screen is drawn with OnGUI, so the sample needs
    /// no UI package; in a real game you would do the same calls from your own UI (see <see cref="OnPresented"/>).
    /// </summary>
    public sealed class BasicDialogueExample : MonoBehaviour
    {
        [Tooltip("The dialogue to play. The list is made from the catalog of the .lorebuild you imported.")]
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
                _error = e.Message;
                Debug.LogException(e);
            }
        }

        private async void StartTalking()
        {
            try
            {
                // 2. Opening a dialogue loads its graph and its words, in the language being played. Only this dialogue's.
                _talk = await _service.StartAsync(dialogue);
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
            if (!view.HasAudio) return;
            try
            {
                // 3. A clip is loaded when it is asked for. It can be missing in this language: then it is null.
                var clip = await _talk.LoadAudioAsync(view);
                if (clip != null && _view == view)
                {
                    voice.Stop();
                    voice.PlayOneShot(clip);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, Mathf.Min(520, Screen.width - 40), Screen.height - 40));

            if (_error != null) GUILayout.Label("<color=red>" + _error + "</color>");
            else if (_service == null || !_service.IsInitialized) GUILayout.Label("Loading…");
            else if (!dialogue.IsSet) GUILayout.Label("Pick a dialogue in the inspector.");
            else if (_talk == null)
            {
                if (GUILayout.Button("Talk")) StartTalking();
            }
            else if (_view != null) DrawView(_view);

            DrawLanguages();
            GUILayout.EndArea();
        }

        private void DrawView(DialogueView view)
        {
            var speaker = view.Speaker;
            if (!speaker.IsNobody) GUILayout.Label("<b><color=#" + ColorUtility.ToHtmlStringRGB(speaker.Color) + ">" + speaker.Name + "</color></b>");
            GUILayout.Label(view.Text);

            if (!view.IsChoice)
            {
                if (GUILayout.Button("Next")) _talk.Advance();
                return;
            }
            foreach (var option in view.Options)
                if (GUILayout.Button(option.Text)) _talk.Choose(option.Index); // 4. an answer leads to its own next line
        }

        private void DrawLanguages()
        {
            if (_service == null || !_service.IsInitialized) return;
            GUILayout.Space(12);
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

using System;
using System.Collections;
using System.Collections.Generic;
using Loreman.Dialogues;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Loreman.Dialogues.Samples.Canvas
{
    /// <summary>
    /// A dialogue screen made with uGUI: a panel at the bottom of a Canvas with the speaker's name, the line (typed out letter by letter), the answers as buttons
    /// and a Next button. It does not touch the package's loading or playing code: it only listens to a <see cref="DialoguePlayer"/> and shows what it is told.
    /// <list type="bullet">
    /// <item><c>Presented</c> → show a line or a choice. <c>Refreshed</c> → the language changed: show the same thing in the new words, without typing it again.</item>
    /// <item><c>Finished</c> → hide the panel.</item>
    /// <item>Buttons call <c>player.Advance()</c> / <c>player.Choose(i)</c>.</item>
    /// </list>
    /// Everything on the screen is in the scene (look at the Canvas), so restyle it there. The labels are the legacy <see cref="Text"/> so that the sample
    /// needs nothing but uGUI; with TextMeshPro, change the fields to <c>TMP_Text</c> and keep the rest.
    /// </summary>
    public sealed class CanvasDialogueScreen : MonoBehaviour
    {
        [Header("Dialogue")]
        [SerializeField] private DialoguePlayer player;

        [Header("Panel")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Text speakerLabel;
        [SerializeField] private Text lineLabel;
        [SerializeField] private Button nextButton;
        [SerializeField] private RectTransform choicesRoot;
        [Tooltip("An inactive button; one copy is made for every answer.")]
        [SerializeField] private Button choiceTemplate;

        [Header("Start and language")]
        [SerializeField] private Button talkButton;
        [SerializeField] private RectTransform languagesRoot;
        [Tooltip("An inactive button; one copy is made for every language of the project.")]
        [SerializeField] private Button languageTemplate;
        [SerializeField] private Text statusLabel;

        [Header("Typing")]
        [Tooltip("How fast a line is typed out. 0 shows it at once.")]
        [SerializeField] private float charsPerSecond = 45f;

        private readonly List<GameObject> _choiceButtons = new List<GameObject>();
        private Coroutine _typing;
        private string _fullText = string.Empty;
        private bool _languagesBuilt;

        /// <summary>The line is being typed out.</summary>
        public bool IsTyping => _typing != null;

        // ── set up ──

        private void Awake()
        {
            EnsureEventSystem();
            panel.SetActive(false);
            choiceTemplate.gameObject.SetActive(false);
            languageTemplate.gameObject.SetActive(false);
            talkButton.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            player.Ready += OnReady;
            player.Presented += OnPresented;
            player.Refreshed += OnRefreshed;
            player.Finished += OnFinished;
            player.Failed += OnFailed;
            talkButton.onClick.AddListener(OnTalk);
            nextButton.onClick.AddListener(OnNext);
            if (player.IsReady) OnReady();
            else statusLabel.text = "Loading…";
        }

        private void OnDisable()
        {
            player.Ready -= OnReady;
            player.Presented -= OnPresented;
            player.Refreshed -= OnRefreshed;
            player.Finished -= OnFinished;
            player.Failed -= OnFailed;
            talkButton.onClick.RemoveListener(OnTalk);
            nextButton.onClick.RemoveListener(OnNext);
        }

        /// <summary>uGUI needs an EventSystem to hear clicks. One is made if the scene has none, with the module that fits the project's input.</summary>
        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            var module = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (module != null)
            {
                go.AddComponent(module);
                return;
            }
#endif
            go.AddComponent<StandaloneInputModule>();
        }

        // ── events of the player ──

        private void OnReady()
        {
            BuildLanguageButtons();
            statusLabel.text = "Ready";
            talkButton.gameObject.SetActive(!player.IsPlaying);
        }

        private void OnFailed(Exception e) => statusLabel.text = e.Message;

        private void OnPresented(DialogueView view)
        {
            statusLabel.text = string.Empty;
            talkButton.gameObject.SetActive(false);
            panel.SetActive(true);
            ShowSpeaker(view);
            ShowChoices(view);
            nextButton.gameObject.SetActive(!view.IsChoice);
            StartTyping(view.Text);
        }

        private void OnRefreshed(DialogueView view)
        {
            // the language changed: the same line in the new words, shown whole (it was already read)
            ShowSpeaker(view);
            ShowChoices(view);
            StopTyping();
            lineLabel.text = view.Text;
        }

        private void OnFinished()
        {
            StopTyping();
            ClearChoices();
            panel.SetActive(false);
            talkButton.gameObject.SetActive(true);
        }

        // ── buttons ──

        private void OnTalk() => player.Play();

        private void OnNext()
        {
            if (IsTyping) CompleteTyping(); // the first press shows the whole line, the second goes on
            else player.Advance();
        }

        // ── drawing ──

        private void ShowSpeaker(DialogueView view)
        {
            var speaker = view.Speaker;
            speakerLabel.gameObject.SetActive(!speaker.IsNobody);
            speakerLabel.text = speaker.Name;
            speakerLabel.color = speaker.Color;
        }

        private void ShowChoices(DialogueView view)
        {
            ClearChoices();
            if (!view.IsChoice) return;
            foreach (var option in view.Options)
            {
                var button = Instantiate(choiceTemplate, choicesRoot);
                button.gameObject.SetActive(true);
                button.GetComponentInChildren<Text>().text = "▸ " + option.Text + (option.HasAudio ? "  ♪" : string.Empty);
                var index = option.Index;
                button.onClick.AddListener(() => player.Choose(index));
                _choiceButtons.Add(button.gameObject);
            }
        }

        private void ClearChoices()
        {
            foreach (var go in _choiceButtons) Destroy(go);
            _choiceButtons.Clear();
        }

        private void BuildLanguageButtons()
        {
            if (_languagesBuilt) return;
            _languagesBuilt = true;
            foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
            {
                var button = Instantiate(languageTemplate, languagesRoot);
                button.gameObject.SetActive(true);
                button.GetComponentInChildren<Text>().text = locale.Identifier.Code.ToUpperInvariant();
                var chosen = locale;
                // Changing the language is Unity Localization's own job; an open dialogue answers with Refreshed.
                button.onClick.AddListener(() => LocalizationSettings.SelectedLocale = chosen);
            }
        }

        // ── typing out ──

        private void StartTyping(string text)
        {
            StopTyping();
            _fullText = text ?? string.Empty;
            if (charsPerSecond <= 0f || _fullText.Length == 0)
            {
                lineLabel.text = _fullText;
                return;
            }
            lineLabel.text = string.Empty;
            _typing = StartCoroutine(Type());
        }

        private IEnumerator Type()
        {
            var shown = 0f;
            while (shown < _fullText.Length)
            {
                shown += charsPerSecond * Time.unscaledDeltaTime;
                lineLabel.text = _fullText.Substring(0, Mathf.Min(_fullText.Length, Mathf.FloorToInt(shown)));
                yield return null;
            }
            _typing = null;
            lineLabel.text = _fullText;
        }

        /// <summary>Shows the whole line at once.</summary>
        public void CompleteTyping()
        {
            StopTyping();
            lineLabel.text = _fullText;
        }

        private void StopTyping()
        {
            if (_typing == null) return;
            StopCoroutine(_typing);
            _typing = null;
        }
    }
}

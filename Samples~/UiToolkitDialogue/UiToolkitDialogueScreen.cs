using System;
using System.Collections;
using Loreman.Dialogues;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace Loreman.Dialogues.Samples.UiToolkit
{
    /// <summary>
    /// A dialogue screen made with UI Toolkit: the look is in <c>DialogueScreen.uxml</c> (the elements) and <c>DialogueScreen.uss</c> (the style), and this script only
    /// fills it. Like the Canvas sample, it does not touch the package's loading or playing code: it listens to a <see cref="DialoguePlayer"/>.
    /// <list type="bullet">
    /// <item><c>Presented</c> → show a line or a choice (the line is typed out). <c>Refreshed</c> → the language changed: the same thing in the new words.</item>
    /// <item><c>Finished</c> → hide the panel. Buttons call <c>player.Advance()</c> / <c>player.Choose(i)</c>.</item>
    /// </list>
    /// To restyle the dialogue, edit the USS; to move things around, edit the UXML (keep the element names the script looks for).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UiToolkitDialogueScreen : MonoBehaviour
    {
        [SerializeField] private DialoguePlayer player;

        [Tooltip("How fast a line is typed out. 0 shows it at once.")]
        [SerializeField] private float charsPerSecond = 45f;

        private VisualElement _panel;
        private VisualElement _choices;
        private VisualElement _languages;
        private Label _speaker;
        private Label _line;
        private Label _status;
        private Button _talk;
        private Button _next;

        private IVisualElementScheduledItem _typing;
        private string _fullText = string.Empty;
        private float _typed;
        private bool _languagesBuilt;
        private bool _bound;

        /// <summary>The line is being typed out.</summary>
        public bool IsTyping => _typing != null;

        private void OnEnable()
        {
            if (!TryBind()) StartCoroutine(BindWhenTheDocumentIsBuilt());
        }

        private IEnumerator BindWhenTheDocumentIsBuilt()
        {
            // the UIDocument makes its elements on its own schedule; try again until they are there
            for (var i = 0; i < 10 && !TryBind(); i++) yield return null;
        }

        private bool TryBind()
        {
            if (_bound) return true;
            var root = GetComponent<UIDocument>().rootVisualElement;
            _panel = root?.Q<VisualElement>("panel");
            if (_panel == null) return false;

            _choices = root.Q<VisualElement>("choices");
            _languages = root.Q<VisualElement>("languages");
            _speaker = root.Q<Label>("speaker");
            _line = root.Q<Label>("line");
            _status = root.Q<Label>("status");
            _talk = root.Q<Button>("talk");
            _next = root.Q<Button>("next");

            Hide(_panel);
            Hide(_talk);
            _talk.clicked += OnTalk;
            _next.clicked += OnNext;
            player.Ready += OnReady;
            player.Presented += OnPresented;
            player.Refreshed += OnRefreshed;
            player.Finished += OnFinished;
            player.Failed += OnFailed;
            _bound = true;

            if (player.IsReady) OnReady();
            else _status.text = "Loading…";
            return true;
        }

        private void OnDisable()
        {
            if (!_bound) return;
            _bound = false;
            _talk.clicked -= OnTalk;
            _next.clicked -= OnNext;
            player.Ready -= OnReady;
            player.Presented -= OnPresented;
            player.Refreshed -= OnRefreshed;
            player.Finished -= OnFinished;
            player.Failed -= OnFailed;
            StopTyping();
        }

        // ── events of the player ──

        private void OnReady()
        {
            BuildLanguageButtons();
            _status.text = "Ready";
            Show(_talk, !player.IsPlaying);
        }

        private void OnFailed(Exception e) => _status.text = e.Message;

        private void OnPresented(DialogueView view)
        {
            _status.text = string.Empty;
            Hide(_talk);
            Show(_panel, true);
            ShowSpeaker(view);
            ShowChoices(view);
            Show(_next, !view.IsChoice);
            StartTyping(view.Text);
        }

        private void OnRefreshed(DialogueView view)
        {
            // the language changed: the same line in the new words, shown whole (it was already read)
            ShowSpeaker(view);
            ShowChoices(view);
            StopTyping();
            _line.text = view.Text;
        }

        private void OnFinished()
        {
            StopTyping();
            _choices.Clear();
            Hide(_panel);
            Show(_talk, true);
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
            Show(_speaker, !speaker.IsNobody);
            _speaker.text = speaker.Name;
            _speaker.style.color = new StyleColor(speaker.Color);
        }

        private void ShowChoices(DialogueView view)
        {
            _choices.Clear();
            if (!view.IsChoice) return;
            foreach (var option in view.Options)
            {
                var index = option.Index;
                var button = new Button(() => player.Choose(index)) { text = "▸ " + option.Text + (option.HasAudio ? "  ♪" : string.Empty) };
                button.AddToClassList("choice");
                _choices.Add(button);
            }
        }

        private void BuildLanguageButtons()
        {
            if (_languagesBuilt) return;
            _languagesBuilt = true;
            foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
            {
                var chosen = locale;
                // Changing the language is Unity Localization's own job; an open dialogue answers with Refreshed.
                var button = new Button(() => LocalizationSettings.SelectedLocale = chosen) { text = locale.Identifier.Code.ToUpperInvariant() };
                button.AddToClassList("language");
                _languages.Add(button);
            }
        }

        private static void Show(VisualElement element, bool visible) => element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        private static void Hide(VisualElement element) => Show(element, false);

        // ── typing out ──

        private void StartTyping(string text)
        {
            StopTyping();
            _fullText = text ?? string.Empty;
            if (charsPerSecond <= 0f || _fullText.Length == 0)
            {
                _line.text = _fullText;
                return;
            }
            _line.text = string.Empty;
            _typed = 0f;
            _typing = _line.schedule.Execute(TypeOn).Every(16);
        }

        private void TypeOn()
        {
            _typed += charsPerSecond * Time.unscaledDeltaTime;
            var shown = Mathf.Min(_fullText.Length, Mathf.FloorToInt(_typed));
            _line.text = _fullText.Substring(0, shown);
            if (shown >= _fullText.Length) StopTyping();
        }

        /// <summary>Shows the whole line at once.</summary>
        public void CompleteTyping()
        {
            StopTyping();
            _line.text = _fullText;
        }

        private void StopTyping()
        {
            if (_typing == null) return;
            _typing.Pause();
            _typing = null;
            if (_line != null) _line.text = _fullText;
        }
    }
}

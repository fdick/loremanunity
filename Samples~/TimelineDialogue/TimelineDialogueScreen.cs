using System;
using Loreman.Dialogues;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Playables;

namespace Loreman.Dialogues.Samples.TimelineExample
{
    /// <summary>
    /// The screen of the Timeline sample. The timeline (a <see cref="PlayableDirector"/>) walks the hero to the gate, plays a dialogue clip, and goes on when the dialogue is over;
    /// this script only shows what the <see cref="DialoguePlayer"/> says, with OnGUI so that the sample needs no UI package. It does not know about the timeline's clips:
    /// the dialogue clip talks to the player by itself, exactly as <c>player.Play()</c> from code would.
    /// </summary>
    public sealed class TimelineDialogueScreen : MonoBehaviour
    {
        [SerializeField] private DialoguePlayer player;
        [SerializeField] private PlayableDirector director;

        private DialogueView _view;
        private string _error;

        private void OnEnable()
        {
            player.Presented += OnPresented;
            player.Refreshed += OnPresented;
            player.Finished += OnFinished;
            player.Failed += OnFailed;
        }

        private void OnDisable()
        {
            player.Presented -= OnPresented;
            player.Refreshed -= OnPresented;
            player.Finished -= OnFinished;
            player.Failed -= OnFailed;
        }

        private void OnPresented(DialogueView view) => _view = view;

        private void OnFinished() => _view = null;

        private void OnFailed(Exception e) => _error = e.Message + "\n\nIs the build synced? Tools → Loreman → Window.";

        // the timeline holds its last frame (Wrap Mode: Hold), so it never stops: it is over when its time reached the end
        private bool TimelineEnded => director.duration > 0d && director.time >= director.duration - 0.001d;

        private void Restart()
        {
            director.time = 0d;
            director.Play();
        }

        private void OnGUI()
        {
            var scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var height = Screen.height / scale;

            var label = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
            var button = new GUIStyle(GUI.skin.button) { fontSize = 20, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 14, 6, 6) };

            // the timeline: where it is, and why it does not move
            var state = TimelineEnded ? "finished" : player.IsPlaying ? "waiting for the dialogue to end" : director.state == PlayState.Playing ? "playing" : "starting";
            GUI.Box(new Rect(16, 16, 640, 40), GUIContent.none);
            GUI.Label(new Rect(28, 22, 620, 32), $"Timeline  {director.time:0.0} / {director.duration:0.0} s   —   {state}", label);

            if (LocalizationSettings.InitializationOperation.IsDone)
            {
                var x = width - 16f;
                var locales = LocalizationSettings.AvailableLocales.Locales;
                for (var i = locales.Count - 1; i >= 0; i--)
                {
                    x -= 70f;
                    if (GUI.Button(new Rect(x, 16, 62, 40), locales[i].Identifier.Code.ToUpperInvariant(), button)) LocalizationSettings.SelectedLocale = locales[i];
                }
            }

            if (TimelineEnded && GUI.Button(new Rect(16, 64, 220, 44), "Play again", button)) Restart();

            if (_error != null)
            {
                GUI.Box(new Rect(16, height - 200, width - 32, 184), GUIContent.none);
                GUI.Label(new Rect(32, height - 188, width - 64, 160), _error, label);
                return;
            }

            if (_view == null) return;

            var optionCount = _view.IsChoice ? _view.Options.Count : 1;
            var panelHeight = 150f + optionCount * 54f;
            var top = height - panelHeight - 16f;
            GUI.Box(new Rect(16, top, width - 32, panelHeight), GUIContent.none);

            var speaker = _view.IsChoice ? _view.AnswerSpeaker : _view.Speaker;
            if (!speaker.IsNobody)
            {
                var name = new GUIStyle(label) { fontStyle = FontStyle.Bold, fontSize = 22 };
                GUI.Label(new Rect(32, top + 10, width - 64, 30), speaker.Name, name);
            }
            GUI.Label(new Rect(32, top + 44, width - 64, 80), (_view.HasAudio ? "♪ " : "") + _view.Text, label);

            var y = top + 130f;
            if (_view.IsChoice)
            {
                foreach (var option in _view.Options)
                {
                    if (GUI.Button(new Rect(32, y, width - 64, 46), (option.HasAudio ? "♪ " : "") + option.Text, button)) player.Choose(option.Index);
                    y += 54f;
                }
            }
            else if (GUI.Button(new Rect(width - 32 - 220, y, 220, 46), "Next  ▶", button))
            {
                player.Advance();
            }
        }
    }
}

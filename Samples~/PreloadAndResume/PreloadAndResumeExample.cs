using System;
using Loreman.Dialogues;
using UnityEngine;

namespace Loreman.Dialogues.Samples
{
    /// <summary>
    /// Three things a real game does with dialogues besides playing them once:
    /// <list type="bullet">
    /// <item><b>Preload</b> a dialogue while the player is still walking to the character, so it opens without waiting.</item>
    /// <item><b>Save</b> the place in a dialogue (the id of the node) and <b>resume</b> from it after a restart.</item>
    /// <item>Read the <b>note</b> of a line, which the designer wrote in Loreman (conditions, effects…) and the package leaves to the game.</item>
    /// </list>
    /// Open the scene of the sample and press Play.
    /// </summary>
    public sealed class PreloadAndResumeExample : MonoBehaviour
    {
        private const string SaveKey = "Loreman.Sample.Node";

        [Tooltip("The dialogue to play. Leave it empty to play the first dialogue of the catalog.")]
        [SerializeField] private DialogueReference dialogue;

        private DialogueService _service;
        private IDisposable _preload;
        private DialogueConversation _talk;
        private DialogueView _view;
        private string _status = "Loading…";
        private string _note = string.Empty;

        private Guid DialogueId => dialogue.IsSet ? dialogue.Guid : _service.Dialogues[0].Guid;

        private async void Start()
        {
            try
            {
                _service = DialogueService.CreateDefault();
                await _service.InitializeAsync();
                _status = "Ready. Dialogues in memory: " + _service.LoadedCount;
            }
            catch (Exception e)
            {
                _status = e.Message + "\n\nIs the build synced? Tools → Loreman → Window.";
                Debug.LogException(e);
            }
        }

        // Call this when the player comes near the character (a trigger, a distance check…).
        private async void Preload()
        {
            if (_preload != null) return;
            _status = "Preloading…";
            try
            {
                // The dialogue stays in memory until the handle is disposed. StartAsync then finds it loaded and returns at once.
                _preload = await _service.PreloadAsync(DialogueId);
                _status = "Preloaded. Dialogues in memory: " + _service.LoadedCount;
            }
            catch (Exception e)
            {
                _status = e.Message;
                Debug.LogException(e);
            }
        }

        // Call this when the player walks away without talking: the memory is given back.
        private void Unload()
        {
            _preload?.Dispose();
            _preload = null;
            _status = "Unloaded. Dialogues in memory: " + _service.LoadedCount;
        }

        private async void Talk(bool resume)
        {
            try
            {
                _talk = await _service.StartAsync(DialogueId);
                _talk.Presented += view =>
                {
                    _view = view;
                    // The id of a node never changes between builds, so it is safe to keep in a save file.
                    PlayerPrefs.SetString(SaveKey, view.NodeId.ToString("D"));
                    _note = string.IsNullOrWhiteSpace(view.Note) ? string.Empty : view.Note;
                    if (_note.Length > 0) Debug.Log("Designer's note of this line: " + _note);
                };
                _talk.Finished += () =>
                {
                    PlayerPrefs.DeleteKey(SaveKey);
                    _view = null;
                    _note = string.Empty;
                    _talk.Dispose();
                    _talk = null;
                    _status = "Finished. Dialogues in memory: " + _service.LoadedCount;
                };

                if (resume && Guid.TryParse(PlayerPrefs.GetString(SaveKey), out var node) && _talk.JumpTo(node)) return;
                _talk.Start();
            }
            catch (Exception e)
            {
                _status = e.Message;
                Debug.LogException(e);
            }
        }

        private void LeaveWithoutFinishing()
        {
            _view = null;
            _note = string.Empty;
            _talk.Dispose();
            _talk = null;
            _status = "Left; the place is saved. Dialogues in memory: " + _service.LoadedCount;
        }

        private void OnGUI()
        {
            var scale = Mathf.Max(1f, Screen.height / 540f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.skin.label.richText = true;
            GUI.skin.label.wordWrap = true;
            GUILayout.BeginArea(new Rect(20, 20, Mathf.Min(560, Screen.width / scale - 40), Screen.height / scale - 40));
            GUILayout.Label("<b>Loreman — Preload and Resume</b>");
            GUILayout.Label(_status);

            if (_service != null && _service.IsInitialized && _service.Dialogues.Count > 0 && _talk == null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Preload")) Preload();
                if (GUILayout.Button("Unload")) Unload();
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Talk from the start")) Talk(false);
                if (PlayerPrefs.HasKey(SaveKey) && GUILayout.Button("Resume from the saved place")) Talk(true);
            }
            else if (_view != null)
            {
                GUILayout.Space(8);
                GUILayout.Label("<b>" + _view.Speaker.Name + "</b>: " + _view.Text);
                if (_note.Length > 0) GUILayout.Label("<i>designer's note: " + _note + "</i>");
                if (!_view.IsChoice)
                {
                    if (GUILayout.Button("Next")) _talk.Advance();
                }
                else
                {
                    foreach (var option in _view.Options)
                        if (GUILayout.Button(option.Text)) _talk.Choose(option.Index);
                }
                if (GUILayout.Button("Leave (the place stays saved)")) LeaveWithoutFinishing();
            }
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            _preload?.Dispose();
            _talk?.Dispose();
            _service?.Dispose();
        }
    }
}

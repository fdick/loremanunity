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
    /// Put the component on an empty GameObject, pick a dialogue and press Play.
    /// </summary>
    public sealed class PreloadAndResumeExample : MonoBehaviour
    {
        private const string SaveKey = "Loreman.Sample.Node";

        [SerializeField] private DialogueReference dialogue;

        private DialogueService _service;
        private IDisposable _preload;
        private DialogueConversation _talk;
        private DialogueView _view;
        private string _status = "Loading…";

        private async void Start()
        {
            try
            {
                _service = DialogueService.CreateDefault();
                await _service.InitializeAsync();
                _status = "Ready.";
            }
            catch (Exception e)
            {
                _status = e.Message;
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
                _preload = await _service.PreloadAsync(dialogue);
                _status = "Preloaded: " + _service.LoadedCount + " dialogue(s) in memory.";
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
            _status = "Unloaded: " + _service.LoadedCount + " dialogue(s) in memory.";
        }

        private async void Talk(bool resume)
        {
            try
            {
                _talk = await _service.StartAsync(dialogue);
                _talk.Presented += view =>
                {
                    _view = view;
                    // The id of a node never changes between builds, so it is safe to keep in a save file.
                    PlayerPrefs.SetString(SaveKey, view.NodeId.ToString("D"));
                    if (!string.IsNullOrWhiteSpace(view.Note)) Debug.Log("Designer's note of this line: " + view.Note);
                };
                _talk.Finished += () =>
                {
                    PlayerPrefs.DeleteKey(SaveKey);
                    _view = null;
                    _talk.Dispose();
                    _talk = null;
                    _status = "Finished. In memory: " + _service.LoadedCount;
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

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, Mathf.Min(520, Screen.width - 40), Screen.height - 40));
            GUILayout.Label(_status);

            if (_service != null && _service.IsInitialized && dialogue.IsSet && _talk == null)
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
                GUILayout.Label(_view.Speaker.Name + ": " + _view.Text);
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

        private void LeaveWithoutFinishing()
        {
            _view = null;
            _talk.Dispose();
            _talk = null;
            _status = "Left. Press Resume to carry on.";
        }

        private void OnDestroy()
        {
            _preload?.Dispose();
            _talk?.Dispose();
            _service?.Dispose();
        }
    }
}

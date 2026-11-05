namespace Loreman.Dialogues
{
    /// <summary>How a <see cref="DialoguePlayer"/> goes on after a line without being told to: for cutscenes, where nobody presses Next.</summary>
    public enum DialogueAutoAdvance
    {
        /// <summary>Waits for <see cref="DialoguePlayer.Advance"/> (a button, a key). The default.</summary>
        Manual,

        /// <summary>Goes on a fixed time after each line appears (the time is for reading it).</summary>
        AfterSeconds,

        /// <summary>
        /// Goes on when the voice-over of the line has been said, plus a pause. A line without a voice-over waits the pause alone.
        /// </summary>
        AfterVoiceOver,
    }
}

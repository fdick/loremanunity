using System.ComponentModel;
using UnityEngine.Timeline;

namespace Loreman.Dialogues.Timeline
{
    /// <summary>
    /// A track of dialogue clips. Bind a <see cref="DialoguePlayer"/> to it (drag it from the scene onto the track); add clips with right click → Add Dialogue Clip.
    /// </summary>
    [DisplayName("Loreman/Dialogue Track")]
    [TrackColor(0.85f, 0.33f, 0.31f)]
    [TrackClipType(typeof(DialogueClip))]
    [TrackBindingType(typeof(DialoguePlayer))]
    public sealed class DialogueTrack : TrackAsset
    {
    }
}

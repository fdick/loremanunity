using System;
using UnityEngine;

namespace Loreman.Dialogues.Editor
{
    /// <summary>What the project knows of a .lorebuild file: the summary shown in the inspector. The file itself stays outside the game build.</summary>
    public sealed class LoreBuildReference : ScriptableObject
    {
        public string projectName;
        public string contentHash;
        public string builtAt;
        public string baseLanguage;
        public string[] languages = Array.Empty<string>();
        public int dialogueCount;
        public int speakerCount;
        public string error;
    }
}

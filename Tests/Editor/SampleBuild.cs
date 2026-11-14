using System.IO;
using System.Linq;
using Loreman.Dialogues.Editor;
using UnityEditor.PackageManager;

namespace Loreman.Dialogues.Tests
{
    /// <summary>
    /// The sample build made by Loreman's own tests (Tests/Fixtures/sample.lorebuild), so this package is tested against the bytes the server really writes.
    /// It holds one dialogue "Gate": Guard says "Halt! Who goes there?" (note "shout"), then Hero's choice "Let me pass?" (note "if: has_pass") with the answers
    /// "Of course" (leads to Guard's "Move along.") and "Never" (ends). Languages: en (base), ru (partly translated), de (nothing translated).
    /// </summary>
    internal static class SampleBuild
    {
        public static string Path
        {
            get
            {
                var info = PackageInfo.FindForAssembly(typeof(SampleBuild).Assembly);
                return System.IO.Path.Combine(info.resolvedPath, "Tests", "Fixtures", "sample.lorebuild");
            }
        }

        public static LoreBuildFile Open() => LoreBuildFile.Open(Path);

        /// <summary>A build of another Loreman project (the demo build of the samples: "City Gate"). It lives beside <see cref="Open"/>'s, never replaces it.</summary>
        public static string OtherPath
        {
            get
            {
                var info = PackageInfo.FindForAssembly(typeof(SampleBuild).Assembly);
                return System.IO.Path.Combine(info.resolvedPath, "Tests", "Fixtures", "other.lorebuild");
            }
        }

        public static LoreBuildFile OpenOther() => LoreBuildFile.Open(OtherPath);

        /// <summary>
        /// A build with items only (Tests/Fixtures/items.lorebuild): Weapons/Swords/Sword (Damage 15 pt, Weight 2.5 kg, a text parameter Lore, a picture), Potion (Heal -30 hp, Damage 0 pt, the same picture,
        /// no description), Letter (a webp picture that is left out). English (base) and Russian (the name of the Sword and its Lore).
        /// </summary>
        public static string ItemsPath
        {
            get
            {
                var info = PackageInfo.FindForAssembly(typeof(SampleBuild).Assembly);
                return System.IO.Path.Combine(info.resolvedPath, "Tests", "Fixtures", "items.lorebuild");
            }
        }

        public static LoreBuildFile OpenItems() => LoreBuildFile.Open(ItemsPath);

        public static byte[] Bytes() => File.ReadAllBytes(Path);

        public static ManifestDialogue Gate(BuildManifest manifest) => manifest.dialogues.Single(d => d.name == "Gate");

        /// <summary>The graph of the Gate dialogue, read.</summary>
        public static DialogueGraph GateGraph(LoreBuildFile file) => DialogueGraphReader.Read(file.Read(Gate(file.Manifest).graph));
    }
}

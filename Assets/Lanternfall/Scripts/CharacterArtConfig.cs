using UnityEngine;

namespace Lanternfall
{
    /// <summary>
    /// Optional rigged models. Create one with Lanternfall > Create Character Art Config, drop prefabs
    /// (Starter Assets "PlayerArmature", Mixamo characters...) into the slots, and they replace the built-in bodies.
    /// Empty slots keep the procedural characters.
    /// </summary>
    [CreateAssetMenu(menuName = "Lanternfall/Character Art Config", fileName = "CharacterArt")]
    public sealed class CharacterArtConfig : ScriptableObject
    {
        public GameObject Cinder, Rime, Volt, Sol;
        public GameObject Cultist, Archer, Brute, Boss;
    }
}

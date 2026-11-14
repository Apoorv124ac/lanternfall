using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lanternfall.EditorTools
{
    /// <summary>pixelfx sheets must stay pixel-exact: point filtering, no compression, no mipmaps, no power-of-two rescale.</summary>
    public sealed class FxTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("Lanternfall/Resources/FX/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 4096;
            ti.sRGBTexture = true;
        }
    }

    [InitializeOnLoad]
    public static class LanternfallSetup
    {
        public const string ScenePath = "Assets/Lanternfall/Lanternfall.unity";

        static LanternfallSetup()
        {
            // First open of the project: build the slice scene so there is something to press Play on.
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(ScenePath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                CreateScene();
            };
        }

        [MenuItem("Lanternfall/Create Game Scene")]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Lanternfall").AddComponent<Game>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == ScenePath))
            {
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
            Debug.Log("Lanternfall: game scene ready at " + ScenePath + ". Press Play, pick a hero with A/D, then Enter.");
        }

        [MenuItem("Lanternfall/Create Character Art Config")]
        static void CreateArtConfig()
        {
            const string path = "Assets/Lanternfall/Resources/CharacterArt.asset";
            var existing = AssetDatabase.LoadAssetAtPath<CharacterArtConfig>(path);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<CharacterArtConfig>();
                AssetDatabase.CreateAsset(existing, path);
                AssetDatabase.SaveAssets();
            }
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log("Lanternfall: drag rigged character prefabs into the slots of CharacterArt. Empty slots keep the built-in characters.");
        }

        [MenuItem("Lanternfall/Open Game Scene")]
        static void Open()
        {
            if (!File.Exists(ScenePath)) { CreateScene(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
    }
}

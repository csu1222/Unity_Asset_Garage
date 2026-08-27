#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AssetGarage.CombatDemo.Editor
{
    public static class CombatDemoTimingGuideAssetGenerator
    {
        private const string Folder = "Assets/06.Data/CombatDemo/Resources";
        private const string AssetPath = Folder + "/TimingGuideCircle.png";

        [InitializeOnLoadMethod]
        private static void ScheduleEnsureAsset()
            => EditorApplication.delayCall += EnsureAsset;

        [MenuItem("Tools/CombatDemo/Generate Timing Guide Circle")]
        public static void EnsureAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath)) return;

            EnsureFolder("Assets/06.Data", "CombatDemo");
            EnsureFolder("Assets/06.Data/CombatDemo", "Resources");

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * .5f;
            float radius = size * .5f - 1f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius - distance + .5f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(AssetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif

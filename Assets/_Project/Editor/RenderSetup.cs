using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RallyAyiti.EditorTools
{
    /// <summary>
    /// Passe le projet sur URP (rendu mobile) et prépare le profil de post-traitement :
    /// bloom, tonemapping ACES, étalonnage des couleurs, balance des blancs chaude, vignette.
    /// </summary>
    public static class RenderSetup
    {
        private const string Dir = "Assets/_Project/Settings";
        private const string RendererPath = Dir + "/RallyAyiti_Renderer.asset";
        private const string PipelinePath = Dir + "/RallyAyiti_URP.asset";
        private const string ProfilePath = Dir + "/RallyAyiti_PostFX.asset";
        private const string PostDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        [MenuItem("Rally Ayiti/Réglages/Activer URP et le post-traitement")]
        public static void EnsureUrp()
        {
            EnsureFolder();
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                AssignPostProcessData(renderer);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            else
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if (renderer != null) AssignPostProcessData(renderer);
            }

            Configure(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Profil de post-traitement partagé par tous les circuits.</summary>
        public static VolumeProfile PostProfile()
        {
            EnsureFolder();
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(new Color(1f, 0.93f, 0.82f));

            var tonemap = Add<Tonemapping>(profile);
            tonemap.mode.Override(TonemappingMode.ACES);

            var color = Add<ColorAdjustments>(profile);
            color.postExposure.Override(0.3f);
            color.contrast.Override(14f);
            color.saturation.Override(20f);

            var white = Add<WhiteBalance>(profile);
            white.temperature.Override(10f);
            white.tint.Override(2f);

            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>(false);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static void AssignPostProcessData(UniversalRendererData renderer)
        {
            var so = new SerializedObject(renderer);
            SerializedProperty p = so.FindProperty("postProcessData") ?? so.FindProperty("m_PostProcessData");
            if (p == null || p.objectReferenceValue != null) return;
            var data = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostDataPath);
            if (data == null)
            {
                Debug.LogWarning("Rally Ayiti : PostProcessData introuvable, le post-traitement peut rester inactif.");
                return;
            }
            p.objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Configure(UniversalRenderPipelineAsset pipeline)
        {
            var so = new SerializedObject(pipeline);
            void Bool(string name, bool v) { SerializedProperty p = so.FindProperty(name); if (p != null) p.boolValue = v; }
            void Int(string name, int v) { SerializedProperty p = so.FindProperty(name); if (p != null) p.intValue = v; }
            void Float(string name, float v) { SerializedProperty p = so.FindProperty(name); if (p != null) p.floatValue = v; }

            Bool("m_SupportsHDR", true);
            Int("m_MSAA", 4);
            Float("m_RenderScale", 1f);
            Float("m_ShadowDistance", 150f);
            Int("m_ShadowCascadeCount", 2);
            Bool("m_SoftShadowsSupported", true);
            Int("m_MainLightShadowmapResolution", 2048);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Project", "Settings");
        }
    }
}

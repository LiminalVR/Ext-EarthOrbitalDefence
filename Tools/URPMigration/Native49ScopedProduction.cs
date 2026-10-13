using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Outside-Assets scoped candidate. Execute only after the serialized import lease.
public static class Native49ScopedProduction
{
    const string Folder = "Assets/URPMigration/Source49";
    static string Hash(string path) => BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    static JObject Packet() => JObject.Parse(File.ReadAllText(JObject.Parse(File.ReadAllText("Tools/URPMigration/ProductionCandidate-current.json")).Value<string>("stage") + "/manifest.json"));
    static string Key(string guid, long id) => guid + "/" + id;
    static string Key(Object o) { string guid; long id; if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out guid, out id)) throw new Exception("Missing identity: " + o); return Key(guid, id); }
    static Material Source(JObject row) => AssetDatabase.LoadAllAssetsAtPath(row.Value<string>("path")).OfType<Material>().Single(m => Key(m) == Key(row.Value<string>("guid"), row.Value<long>("localID")));
    static void Ready() { if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new Exception("Stopped ready Editor required"); }

    public static string VerifyCompilation() { Ready(); return "Scoped API compilation only; no conversion/configuration/save/import performed."; }

    static Material Convert(Material source, string shaderPath)
    {
        var sourceShader = source.shader; var sourceName = sourceShader.name;
        var target = new Material(source) { hideFlags = HideFlags.None };
        var queue = source.renderQueue; var gi = source.globalIlluminationFlags; var sided = source.doubleSidedGI;
        var targetShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (targetShader == null) throw new Exception("Missing deployed exact shader: " + shaderPath);
        if (sourceName.StartsWith("Legacy Shaders/Particles/", StringComparison.Ordinal)) target.shader = targetShader;
        else
        {
            var upgraders = MaterialUpgrader.FetchAllUpgradersForPipeline(typeof(UniversalRenderPipelineAsset))
                .Where(u => u.OldShaderPath == sourceName && !u.NewShaderPath.Contains("/2D/")).ToList();
            if (upgraders.Count != 1) throw new Exception("Ambiguous installed 3D transfer: " + sourceName);
            MaterialUpgrader.Upgrade(target, upgraders, MaterialUpgrader.UpgradeFlags.None);
            bool standard = sourceName == "Standard" || sourceName == "Standard (Specular setup)";
            if (standard)
            {
                bool map = source.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A") ||
                    source.IsKeywordEnabled(sourceName == "Standard" ? "_METALLICGLOSSMAP" : "_SPECGLOSSMAP");
                target.SetFloat("_Smoothness", source.GetFloat(map ? "_GlossMapScale" : "_Glossiness"));
            }
            else if (sourceName == "Particles/Standard Surface") target.SetFloat("_Smoothness", source.GetFloat("_Glossiness"));
            int mode = (int)source.GetFloat("_Mode");
            if (mode == 2 || mode == 3)
            {
                target.SetFloat("_Blend", 0);
                target.SetFloat("_BlendModePreserveSpecular", mode == 3 ? 1 : 0);
            }
            target.shader = targetShader;
            foreach (string name in new[] { "_Metallic", "_EmissionColor" })
            {
                int a = sourceShader.FindPropertyIndex(name), b = targetShader.FindPropertyIndex(name);
                if (a < 0 || b < 0) continue;
                var af = sourceShader.GetPropertyFlags(a); var bf = targetShader.GetPropertyFlags(b);
                if (sourceShader.GetPropertyType(a) == ShaderPropertyType.Color)
                {
                    var color = source.GetColor(name);
                    if (QualitySettings.activeColorSpace == ColorSpace.Linear && (af & ShaderPropertyFlags.HDR) == 0 && (bf & ShaderPropertyFlags.HDR) != 0) color = color.linear;
                    target.SetColor(name, color);
                }
                else
                {
                    float value = source.GetFloat(name);
                    if (QualitySettings.activeColorSpace == ColorSpace.Linear && (af & ShaderPropertyFlags.Gamma) != 0 && (bf & ShaderPropertyFlags.Gamma) == 0) value = Mathf.GammaToLinearSpace(value);
                    target.SetFloat(name, value);
                }
            }
            if (standard)
            {
                target.SetTexture("_MainTex", source.GetTexture("_MainTex"));
                target.SetTextureScale("_MainTex", source.GetTextureScale("_MainTex"));
                target.SetTextureOffset("_MainTex", source.GetTextureOffset("_MainTex"));
                target.SetColor("_Color", source.GetColor("_Color"));
            }
            bool emission = source.IsKeywordEnabled("_EMISSION");
            if (emission) target.globalIlluminationFlags |= (MaterialGlobalIlluminationFlags)8;
            else target.globalIlluminationFlags &= ~(MaterialGlobalIlluminationFlags)8;
            if (sourceName.StartsWith("Particles/", StringComparison.Ordinal))
                BaseShaderGUI.SetMaterialKeywords(target, sourceName == "Particles/Standard Surface" ? LitGUI.SetMaterialKeywords : null, ParticleGUI.SetMaterialKeywords);
            else BaseShaderGUI.SetMaterialKeywords(target, LitGUI.SetMaterialKeywords);
            if (emission) target.EnableKeyword("_EMISSION"); else target.DisableKeyword("_EMISSION");
            foreach (string name in new[] { "_SrcBlend", "_DstBlend", "_ZWrite" })
                if (source.HasProperty(name) && target.HasProperty(name)) target.SetFloat(name, source.GetFloat(name));
            if (source.HasProperty("_SrcBlend") && target.HasProperty("_SrcBlendAlpha")) target.SetFloat("_SrcBlendAlpha", source.GetFloat("_SrcBlend"));
            if (source.HasProperty("_DstBlend") && target.HasProperty("_DstBlendAlpha")) target.SetFloat("_DstBlendAlpha", source.GetFloat("_DstBlend"));
            if (target.HasProperty("_AlphaToMask")) target.SetFloat("_AlphaToMask", 0);
        }
        // Owned shaders have no normalizing CustomEditor. Restore original metadata after keyword derivation.
        target.name = source.name; target.renderQueue = queue; target.globalIlluminationFlags = gi;
        target.doubleSidedGI = sided; target.enableInstancing = source.enableInstancing;
        return target;
    }

    public static string ConvertExactScope()
    {
        Ready(); if (GraphicsSettings.currentRenderPipeline != null) throw new Exception("Original source pipeline required before transfer");
        var packet = Packet(); var stage = "Builds/URPMigration/ScopedConversion-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
        Directory.CreateDirectory(stage); File.Copy("Tools/URPMigration/Native49ScopedProduction.cs", stage + "/Native49ScopedProduction.cs");
        foreach (JObject row in (JArray)packet["preservedInputs"]) { var actual = Hash(row.Value<string>("path")); if (actual != row.Value<string>("sha256") && actual != row.Value<string>("allowedDeployedSHA256")) throw new Exception("Source preflight changed: " + row["path"]); }
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (active.path != "Assets/Scenes/The_Game.unity" || active.isDirty) throw new Exception("Exact clean Main source required");
        var rows = new JArray(); var errors = new JArray(); var replacements = new Dictionary<string, Material>();
        var sources = ((JArray)packet["materialIdentities"]).OfType<JObject>().Select(row => new { row, source = Source(row) }).ToArray();
        var importers = new Dictionary<ModelImporter, List<Tuple<AssetImporter.SourceAssetIdentifier, Material>>>();
        try
        {
            foreach (var item in sources)
            {
                var row = item.row; var source = item.source;
                if (source.shader.name != row.Value<string>("shader")) throw new Exception("Source shader changed: " + row["path"]);
                if (!row.Value<bool>("convert")) { rows.Add(new JObject { ["source"] = row.DeepClone(), ["kept"] = true }); continue; }
                var before = EditorJsonUtility.ToJson(source); var converted = Convert(source, row.Value<string>("targetShader"));
                Material output;
                if (row.Value<string>("path").EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                {
                    output = source; EditorUtility.CopySerialized(converted, output); Object.DestroyImmediate(converted);
                    EditorUtility.SetDirty(output); AssetDatabase.SaveAssetIfDirty(output);
                }
                else
                {
                    var outputPath = row.Value<string>("targetMaterial");
                    if (File.Exists(outputPath)) throw new Exception("Refuse overwrite extracted material: " + outputPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)); AssetDatabase.CreateAsset(converted, outputPath); output = converted;
                    if (AssetImporter.GetAtPath(row.Value<string>("path")) is ModelImporter importer)
                    {
                        if (!importers.ContainsKey(importer)) importers.Add(importer, new List<Tuple<AssetImporter.SourceAssetIdentifier, Material>>());
                        importers[importer].Add(Tuple.Create(new AssetImporter.SourceAssetIdentifier(source), output));
                    }
                    replacements.Add(Key(row.Value<string>("guid"), row.Value<long>("localID")), output);
                }
                rows.Add(new JObject { ["source"] = row.DeepClone(), ["beforeJSON"] = before, ["afterJSON"] = EditorJsonUtility.ToJson(output), ["outputPath"] = AssetDatabase.GetAssetPath(output), ["outputIdentity"] = Key(output) });
            }
            // Explicit FBX/default PPtrs are repaired before model import can remove original subasset IDs.
            var repairedFiles = new JArray();
            foreach (string path in ((JArray)packet["serializedReferenceFiles"]).Values<string>())
            {
                var bytes = File.ReadAllBytes(path); var text = System.Text.Encoding.UTF8.GetString(bytes); int count = 0;
                foreach (var pair in replacements)
                {
                    var split = pair.Key.Split('/'); string guid; long id; AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pair.Value, out guid, out id);
                    var pattern = @"\{fileID: " + Regex.Escape(split[1]) + @", guid: " + Regex.Escape(split[0]) + @", type: [023]\}";
                    text = Regex.Replace(text, pattern, match => { count++; return "{fileID: " + id + ", guid: " + guid + ", type: 2}"; });
                }
                if (count == 0) continue;
                var before = stage + "/SerializedBefore/" + path; Directory.CreateDirectory(Path.GetDirectoryName(before)); File.WriteAllBytes(before, bytes);
                File.WriteAllBytes(path, System.Text.Encoding.UTF8.GetBytes(text)); repairedFiles.Add(new JObject { ["path"] = path, ["replacements"] = count, ["preservedBefore"] = before });
            }
            // Reload exact disk bindings before reimporting models; do not save unchanged scene/other assets.
            EditorSceneManager.OpenScene(active.path);
            foreach (var pair in importers)
            {
                foreach (var map in pair.Value) pair.Key.AddRemap(map.Item1, map.Item2);
                pair.Key.SaveAndReimport();
            }
            foreach (JObject file in repairedFiles) AssetDatabase.ImportAsset(file.Value<string>("path"), ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            File.WriteAllText(stage + "/reference-repairs.json", repairedFiles.ToString());
        }
        catch (Exception error) { errors.Add(error.ToString()); throw; }
        finally { File.WriteAllText(stage + "/report.json", new JObject { ["utc"] = DateTime.UtcNow.ToString("o"), ["pid"] = System.Diagnostics.Process.GetCurrentProcess().Id, ["packet"] = packet["packetPath"], ["rows"] = rows, ["errors"] = errors, ["limits"] = "Scoped persistent transfer only. Native effective ordered-slot/remap/property audit, clean cold URP Main, natural gameplay and Android packed provider/profile remain separate required gates. No blanket SaveAssets/dirty clear/source gameplay edits." }.ToString()); }
        return Path.GetFullPath(stage + "/report.json");
    }

    public static string ConfigureExact()
    {
        Ready(); var folder = Folder + "/Pipeline"; Directory.CreateDirectory(folder);
        var stage = "Builds/URPMigration/ScopedConfigure-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"); Directory.CreateDirectory(stage);
        var settingPaths = new[] { "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset" };
        var settingsBefore = new JArray();
        foreach (var path in settingPaths) { File.Copy(path, stage + "/" + Path.GetFileName(path)); var objects = AssetDatabase.LoadAllAssetsAtPath(path); if (objects.Length != 1) throw new Exception("Native settings object ambiguity: " + path); settingsBefore.Add(new JObject { ["path"] = path, ["nativeJSON"] = EditorJsonUtility.ToJson(objects[0]) }); }
        if (File.Exists(folder + "/Source49Renderer.asset") || File.Exists(folder + "/Source49URP.asset")) throw new Exception("Refuse overwrite pipeline stage");
        var renderer = ScriptableObject.CreateInstance<UniversalRendererData>(); renderer.renderingMode = RenderingMode.Forward;
        AssetDatabase.CreateAsset(renderer, folder + "/Source49Renderer.asset");
        var pipeline = UniversalRenderPipelineAsset.Create(renderer); pipeline.name = "Earth Orbital Source URP";
        pipeline.renderScale = 1; pipeline.msaaSampleCount = 1; pipeline.supportsHDR = true;
        pipeline.supportsCameraDepthTexture = true; pipeline.supportsCameraOpaqueTexture = true; pipeline.shadowDistance = QualitySettings.shadowDistance;
        var so = new SerializedObject(pipeline);
        so.FindProperty("m_MainLightRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
        so.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
        so.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = 4;
        so.FindProperty("m_LightFalloffMode").intValue = (int)LightFalloffMode.Linear;
        so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(pipeline, folder + "/Source49URP.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; int originalQuality = QualitySettings.GetQualityLevel();
        try { for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = pipeline; } }
        finally { QualitySettings.SetQualityLevel(originalQuality, false); }
        AssetDatabase.SaveAssetIfDirty(renderer); AssetDatabase.SaveAssetIfDirty(pipeline);
        foreach (var path in settingPaths) UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(AssetDatabase.LoadAllAssetsAtPath(path), path, true);
        var levels = new JArray();
        try { for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); levels.Add(new JObject { ["index"] = i, ["name"] = QualitySettings.names[i], ["pipelinePath"] = AssetDatabase.GetAssetPath(QualitySettings.renderPipeline), ["pipelineIdentity"] = Key(QualitySettings.renderPipeline) }); } }
        finally { QualitySettings.SetQualityLevel(originalQuality, false); }
        File.WriteAllText(stage + "/report.json", new JObject { ["utc"] = DateTime.UtcNow.ToString("o"), ["pid"] = System.Diagnostics.Process.GetCurrentProcess().Id, ["before"] = settingsBefore, ["after"] = new JArray(settingPaths.Select(path => new JObject { ["path"] = path, ["nativeJSON"] = EditorJsonUtility.ToJson(AssetDatabase.LoadAllAssetsAtPath(path).Single()), ["diskSHA256"] = Hash(path) })), ["qualityLevels"] = levels, ["pipelineIdentity"] = Key(pipeline), ["pipelineJSON"] = EditorJsonUtility.ToJson(pipeline), ["rendererJSON"] = EditorJsonUtility.ToJson(renderer), ["limits"] = "Exact native settings objects persisted without global asset flush. Settings schema/default/flag changes must be classified against before-native and disk originals; cold process and actual disk pipeline PPtrs required separately. No art/player/gameplay settings edits." }.ToString());
        return Path.GetFullPath(stage + "/report.json");
    }
}

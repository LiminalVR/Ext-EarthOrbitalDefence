using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Explicit migration entry points, invoked with Unity Pipeline run_script.
public static class Experience49UrpMigration
{
    const string Folder = "Assets/Settings/Experience49URP";
    static void Upgrade3D(Material material)
    {
        // The full registry also contains Lit -> 2D upgraders. Restrict the pass to
        // the original shader so a 3D material is not upgraded a second time to 2D.
        var original=material.shader;
        bool standard=original.name=="Standard" || original.name=="Standard (Specular setup)";
        bool sourceSmoothMap=material.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A") || material.IsKeywordEnabled(original.name=="Standard"?"_METALLICGLOSSMAP":"_SPECGLOSSMAP");
        float sourceSmoothness=standard?material.GetFloat(sourceSmoothMap?"_GlossMapScale":"_Glossiness"):0;
        var emissionIndex=original.FindPropertyIndex("_EmissionColor");
        bool linearEmission=QualitySettings.activeColorSpace==ColorSpace.Linear && emissionIndex>=0 &&
            (original.name=="Standard" || original.name=="Standard (Specular setup)") &&
            (original.GetPropertyFlags(emissionIndex)&ShaderPropertyFlags.HDR)==0;
        var emission=emissionIndex>=0?material.GetColor("_EmissionColor"):Color.black;
        var transparent=material.HasProperty("_Mode") && material.GetFloat("_Mode")==3;
        var candidates = MaterialUpgrader.FetchAllUpgradersForPipeline(typeof(UniversalRenderPipelineAsset))
            .Where(u => u.OldShaderPath == material.shader.name && !u.NewShaderPath.Contains("/2D/")).ToList();
        if (candidates.Count > 0) MaterialUpgrader.Upgrade(material, candidates, MaterialUpgrader.UpgradeFlags.None);
        if(material.shader.name.StartsWith("Universal Render Pipeline/")) {
            if(standard && material.shader.name=="Universal Render Pipeline/Lit") material.SetFloat("_Smoothness",sourceSmoothness);
            if(linearEmission && material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor",emission.linear);
            if(transparent && material.HasProperty("_BlendModePreserveSpecular")) {
                material.SetFloat("_Blend",0);material.SetFloat("_BlendModePreserveSpecular",1);
                BaseShaderGUI.SetMaterialKeywords(material,LitGUI.SetMaterialKeywords);
            }
        }
    }
    public static string CorrectImported3DShaders()
    {
        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Folder })) {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (material.shader.name != "Universal Render Pipeline/2D/Mesh2D-Lit-Default") continue;
            // The prior Standard upgrade already populated the URP Lit properties.
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
            EditorUtility.SetDirty(material); count++;
        }
        SaveProjectAssets();
        return "Corrected " + count + " imported/default material shaders to 3D URP Lit.";
    }
    public static string ConvertBuiltinReferences()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode first.");
        Directory.CreateDirectory(Folder + "/BuiltinMaterials");
        AssetDatabase.Refresh();
        var converted = new Dictionary<Material, Material>();
        int references = 0;
        bool Replace(GameObject root) {
            bool changed = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) {
                var materials = renderer.sharedMaterials;
                bool modified = false;
                for (int i = 0; i < materials.Length; i++) {
                    var source = materials[i];
                    if (source == null || AssetDatabase.GetAssetPath(source) != "Resources/unity_builtin_extra") continue;
                    if (!converted.TryGetValue(source, out var target)) {
                        var path = Folder + "/BuiltinMaterials/" + source.name + ".mat";
                        target = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (target == null) {
                            target = new Material(source);
                            Upgrade3D(target);
                            ConvertSpecial(target);
                            AssetDatabase.CreateAsset(target, path);
                        }
                        converted[source] = target;
                    }
                    materials[i] = target; modified = true; references++;
                }
                if (modified) { renderer.sharedMaterials = materials; EditorUtility.SetDirty(renderer); changed = true; }
            }
            return changed;
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })) {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".prefab")) continue;
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root != null && Replace(root)) PrefabUtility.SavePrefabAsset(root);
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/The_Game.unity");
        bool sceneChanged = false;
        foreach (var root in scene.GetRootGameObjects()) sceneChanged |= Replace(root);
        if (sceneChanged) EditorSceneManager.SaveScene(scene);
        SaveProjectAssets();
        return "Replaced " + references + " Built-in material references using " + converted.Count + " local URP material copies.";
    }
    public static string ConvertRemaining()
    {
        var report = new JArray();
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" })) {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".mat")) {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                var before = mat.shader.name;
                if (ConvertSpecial(mat)) {
                    EditorUtility.SetDirty(mat);
                    report.Add(new JObject { ["path"] = path, ["before"] = before, ["after"] = mat.shader.name });
                }
            } else if (AssetImporter.GetAtPath(path) is ModelImporter importer) {
                var imported = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToArray();
                if (imported.Length == 0) continue;
                var output = Folder + "/ImportedMaterials/" + guid;
                Directory.CreateDirectory(output);
                AssetDatabase.Refresh();
                foreach (var source in imported) {
                    var targetPath = output + "/" + string.Join("_", source.name.Split(Path.GetInvalidFileNameChars())) + ".mat";
                    var target = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
                    if (target == null) {
                        target = new Material(source);
                        Upgrade3D(target);
                        ConvertSpecial(target);
                        AssetDatabase.CreateAsset(target, targetPath);
                    }
                    RestoreBaselineSmoothness(target,path,source.name);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source), target);
                    report.Add(new JObject { ["path"] = path, ["material"] = source.name, ["extracted"] = targetPath, ["after"] = target.shader.name });
                }
                ClearPackageMaterialDirtyFlags();
                importer.SaveAndReimport();
            }
        }
        SaveProjectAssets();
        File.WriteAllText("Builds/URPMigration/URP/additional-conversion.json", report.ToString());
        return "Converted/remapped " + report.Count + " additional materials.";
    }
    static bool ConvertSpecial(Material mat)
    {
        var old = mat.shader.name;
        string target = old.StartsWith("Legacy Shaders/Particles/") ? old.Replace("Legacy Shaders/Particles/", "Liminal/URP/Particles/") :
            old == "Mobile/Particles/Additive" ? "Liminal/URP/Particles/Mobile Additive" : null;
        if (target != null) {
            var shader=Shader.Find(target);
            if(shader==null) throw new Exception("Missing ported shader "+target);
            // Preserve original property names, color semantics and GPU-side factors of two.
            var queue=mat.renderQueue;
            mat.shader=shader;
            mat.renderQueue=queue;
            if(QualitySettings.softParticles && mat.HasProperty("_InvFade")) mat.EnableKeyword("SOFTPARTICLES_ON");
            return true;
        }
        if(old=="Unlit/Texture" || old=="Unlit/Color" || old=="Unlit/Transparent") {
            var texture=mat.HasProperty("_MainTex")?mat.GetTexture("_MainTex"):null;
            var scale=mat.HasProperty("_MainTex")?mat.GetTextureScale("_MainTex"):Vector2.one;
            var offset=mat.HasProperty("_MainTex")?mat.GetTextureOffset("_MainTex"):Vector2.zero;
            var color=mat.HasProperty("_Color")?mat.GetColor("_Color"):Color.white;
            mat.shader=Shader.Find("Universal Render Pipeline/Unlit");
            mat.SetTexture("_BaseMap",texture);mat.SetTextureScale("_BaseMap",scale);mat.SetTextureOffset("_BaseMap",offset);
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Surface",old=="Unlit/Transparent"?1:0);
            BaseShaderGUI.SetMaterialKeywords(mat);
            return true;
        }
        return false;
    }
    public static string RestoreAuthoredMaterials()
    {
        if(EditorApplication.isPlaying) throw new Exception("Stop Play first.");
        int count=0;
        foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets"})) {
            var path=AssetDatabase.GUIDToAssetPath(guid);
            if(!path.EndsWith(".mat"))continue;
            // Timeline animates shared assets in memory. Reimport the unchanged disk source before saving migration.
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);count++;
        }
        return "Restored authored disk values for "+count+" materials.";
    }
    public static string Configure()
    {
        if (!File.Exists("Builds/URPMigration/BuiltIn/appearance.json"))
            throw new Exception("Capture the Built-in appearance inventory before converting.");
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode before migration.");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Folder + "/Experience49Renderer.asset");
        if (renderer == null) {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.renderingMode = RenderingMode.Forward;
            AssetDatabase.CreateAsset(renderer, Folder + "/Experience49Renderer.asset");
        }
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Folder + "/Experience49URP.asset");
        if (pipeline == null) {
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.name = "Experience49 URP";
            pipeline.renderScale = 1;
            pipeline.msaaSampleCount = Math.Max(2, QualitySettings.antiAliasing);
            pipeline.supportsHDR = true;
            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.shadowDistance = QualitySettings.shadowDistance;
            AssetDatabase.CreateAsset(pipeline, Folder + "/Experience49URP.asset");
        }
        // Unity 6.7's compatibility falloff preserves the original Built-in lighting response.
        var serialized = new SerializedObject(pipeline);
        serialized.FindProperty("m_MainLightRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
        serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
        serialized.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = Math.Clamp(QualitySettings.pixelLightCount, 1, 8);
        serialized.FindProperty("m_LightFalloffMode").intValue = (int)LightFalloffMode.Linear;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        GraphicsSettings.defaultRenderPipeline = pipeline;
        var quality = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++) {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = pipeline;
        }
        QualitySettings.SetQualityLevel(quality, false);
        EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(pipeline);
        SaveProjectAssets();
        return "URP configured for every quality level with Forward rendering and Built-in-compatible light falloff.";
    }
    public static string ConvertMaterials()
    {
        if (GraphicsSettings.defaultRenderPipeline is not UniversalRenderPipelineAsset)
            throw new Exception("Configure URP first.");
        var upgraders = MaterialUpgrader.FetchAllUpgradersForPipeline(typeof(UniversalRenderPipelineAsset));
        var report = new JArray();
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" })) {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            // Imported model/substance subassets need extraction rather than in-place edits.
            if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;
            var before = mat.shader.name;
            var candidates = upgraders.Where(u => u.OldShaderPath == before && !u.NewShaderPath.Contains("/2D/")).ToList();
            if (candidates.Count > 0) {
                Undo.RecordObject(mat, "Experience49 URP material conversion");
                Upgrade3D(mat);
                EditorUtility.SetDirty(mat);
            }
            report.Add(new JObject { ["path"] = path, ["before"] = before, ["after"] = mat.shader.name,
                ["converted"] = before != mat.shader.name, ["hasOfficialUpgrader"] = candidates.Count > 0 });
        }
        SaveProjectAssets();
        Directory.CreateDirectory("Builds/URPMigration/URP");
        File.WriteAllText("Builds/URPMigration/URP/material-conversion.json", report.ToString());
        return $"Converted {report.Count(r => (bool)r["converted"])} of {report.Count} material assets. Review material-conversion.json for remaining shaders.";
    }

    static void SaveProjectAssets() {
        // A local SDK is shared by several editors. Never flush package asset changes.
        foreach(var asset in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
            if(EditorUtility.IsDirty(asset) && AssetDatabase.GetAssetPath(asset).StartsWith("Assets/",System.StringComparison.Ordinal))
                AssetDatabase.SaveAssetIfDirty(asset);
    }

    static void ClearPackageMaterialDirtyFlags() {
        foreach(var material in Resources.FindObjectsOfTypeAll<Material>())
            if(AssetDatabase.GetAssetPath(material).StartsWith("Packages/",StringComparison.Ordinal) && EditorUtility.IsDirty(material))
                EditorUtility.ClearDirty(material);
    }
    public static string ScheduleRemaining() {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play mode.");
        Directory.CreateDirectory("Builds/URPMigration/URP");
        var at=EditorApplication.timeSinceStartup+2;EditorApplication.CallbackFunction run=null;
        run=()=>{if(EditorApplication.timeSinceStartup<at)return;EditorApplication.update-=run;
          try{File.WriteAllText("Builds/URPMigration/URP/remaining-result.txt",ConvertRemaining());}
          catch(Exception error){File.WriteAllText("Builds/URPMigration/URP/remaining-error.txt",error.ToString());}
        };EditorApplication.update+=run;return "Remaining-material conversion scheduled; monitor report files.";
    }
    public static string ScheduleRestore() {
        Directory.CreateDirectory("Builds/URPMigration/Preparation");
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play mode.");
        var at=EditorApplication.timeSinceStartup+2;EditorApplication.CallbackFunction run=null;
        run=()=>{if(EditorApplication.timeSinceStartup<at)return;EditorApplication.update-=run;
          try{File.WriteAllText("Builds/URPMigration/Preparation/restore-result.txt",RestoreAuthoredMaterials());}
          catch(Exception error){File.WriteAllText("Builds/URPMigration/Preparation/restore-error.txt",error.ToString());}
        };EditorApplication.update+=run;return "Authored material restore scheduled; monitor Preparation/restore-result.txt.";
    }
    static void RestoreBaselineSmoothness(Material target,string sourcePath,string sourceName) {
        var inventory=PreservedSmoothnessInventory ?? (PreservedSmoothnessInventory=JObject.Parse(File.ReadAllText("Builds/URPMigration/BuiltIn/appearance.json")));
        var rows=inventory["materials"].Where(row=>(string)row["path"]==sourcePath && (string)row["name"]==sourceName).ToArray();
        if(rows.Length!=1) throw new Exception("Ambiguous/missing preserved model material: "+sourcePath+" / "+sourceName);
        var row=rows[0];var shader=(string)row["shader"];
        if(shader!="Standard" && shader!="Standard (Specular setup)") return;
        if(AssetDatabase.AssetPathToGUID(sourcePath)!=(string)row["guid"]) throw new Exception("Preserved model GUID changed: "+sourcePath);
        if(target.shader.name!="Universal Render Pipeline/Lit" && target.shader.name!="Liminal/URP/Lit Built-in Detail") return;
        var keywords=((JArray)row["keywords"]).Values<string>().ToArray();
        bool alpha=keywords.Contains("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        bool map=keywords.Contains(shader=="Standard"?"_METALLICGLOSSMAP":"_SPECGLOSSMAP");
        if(alpha!=target.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A")) throw new Exception("Imported smoothness channel needs source review: "+sourcePath+" / "+sourceName);
        var scalar=row["properties"]?[alpha||map?"_GlossMapScale":"_Glossiness"];
        if(scalar==null) throw new Exception("Preserved effective smoothness missing: "+sourcePath+" / "+sourceName);
        target.SetFloat("_Smoothness",(float)scalar);EditorUtility.SetDirty(target);
    }
    static JObject PreservedSmoothnessInventory;
    public static string ScheduleMaterials() {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play mode.");
        Directory.CreateDirectory("Builds/URPMigration/URP");
        var at=EditorApplication.timeSinceStartup+2;EditorApplication.CallbackFunction run=null;
        run=()=>{if(EditorApplication.timeSinceStartup<at)return;EditorApplication.update-=run;
          try{
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Play started before material conversion; stop and reschedule.");
            File.WriteAllText("Builds/URPMigration/URP/materials-result.txt",ConvertMaterials());
          }
          catch(Exception error){File.WriteAllText("Builds/URPMigration/URP/materials-error.txt",error.ToString());}
        };EditorApplication.update+=run;return "Material conversion scheduled; monitor URP/materials-result.txt.";
    }
}

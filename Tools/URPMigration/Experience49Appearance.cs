using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Run via Unity Pipeline's run_script command. Kept outside Assets to avoid player code.
public static class Experience49Appearance
{
    public const string ScenePath = "Assets/Scenes/The_Game.unity";
    public const string Root = "Builds/URPMigration";
    static JArray ColorValue(Color c) => new JArray(c.r, c.g, c.b, c.a);
    static JArray VectorValue(Vector3 v) => new JArray(v.x, v.y, v.z);
    static string Stage => GraphicsSettings.currentRenderPipeline == null ? "BuiltIn" : "URP";
    public static string Audit()
    {
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        var rows = new JArray(renderers.SelectMany(r => r.sharedMaterials.Where(m => m != null).Select(m => new JObject {
            ["renderer"] = r.name, ["active"] = r.enabled && r.gameObject.activeInHierarchy,
            ["material"] = m.name, ["path"] = AssetDatabase.GetAssetPath(m),
            ["shader"] = m.shader?.name, ["shaderPath"] = AssetDatabase.GetAssetPath(m.shader),
            ["supported"] = m.shader != null && m.shader.isSupported && m.shader.name != "Hidden/InternalErrorShader",
            ["passes"] = m.shader == null ? new JArray() : new JArray(Enumerable.Range(0,m.shader.passCount).Select(i=>m.shader.FindPassTagValue(i,new ShaderTagId("LightMode")).name)),
            ["errors"] = m.shader == null ? new JArray() : new JArray(ShaderUtil.GetShaderMessages(m.shader).Where(e => e.severity.ToString() == "Error").Select(e=>e.message))
        })));
        var report = new JObject {
            ["stage"] = Stage, ["playing"] = EditorApplication.isPlaying, ["materials"] = rows,
            ["nullMaterialSlots"] = new JArray(Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include)
                .Where(r=>r.enabled && r.gameObject.activeInHierarchy && (r.sharedMaterials.Length==0 || (r is ParticleSystemRenderer pr ? pr.sharedMaterial==null || (pr.GetComponent<ParticleSystem>().trails.enabled && pr.trailMaterial==null) : r.sharedMaterials.Any(m=>m==null)))).Select(r=>r.name)),
            ["particleTrails"] = new JArray(Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include)
                .Where(r=>r.GetComponent<ParticleSystem>().trails.enabled).Select(r=>new JObject {
                    ["name"]=r.name,["active"]=r.enabled&&r.gameObject.activeInHierarchy,
                    ["material"]=r.trailMaterial==null?null:r.trailMaterial.name,
                    ["shader"]=r.trailMaterial==null?null:r.trailMaterial.shader.name,
                    ["path"]=r.trailMaterial==null?null:AssetDatabase.GetAssetPath(r.trailMaterial) })),
            ["missingScripts"] = new JArray(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0).Select(t=>t.name)),
            ["directors"] = new JArray(Object.FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsInactive.Include).Select(d=>new JObject {
                ["name"]=d.name,["active"]=d.isActiveAndEnabled,["time"]=d.time,["duration"]=d.duration,["state"]=d.state.ToString()
            }))
        };
        Directory.CreateDirectory(Root + "/" + Stage);
        File.WriteAllText(Root + "/" + Stage + "/validation.json", report.ToString());
        return "Recorded " + rows.Count + " renderer material uses, missing scripts: " + ((JArray)report["missingScripts"]).Count;
    }
    public static string Baseline()
    {
        if (File.Exists(Root + "/BuiltIn/appearance.json")) throw new Exception("Baseline already exists; refusing overwrite.");
        OpenExperience49();
        var result = Record("BuiltIn");
        return result + "; " + CaptureViews("BuiltIn");
    }
    public static string After()
    {
        OpenExperience49();
        var result = Record("URP");
        return result + "; " + CaptureViews("URP");
    }
    public static string RepairedBaseline()
    {
        OpenExperience49();
        return Record("BuiltInRepaired") + "; " + CaptureViews("BuiltInRepaired");
    }
    public static string OpenExperience49()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode first.");
        EditorSceneManager.OpenScene(ScenePath);
        return "Opened " + ScenePath;
    }
    public static string Record(string stage)
    {
        Directory.CreateDirectory(Root + "/" + stage);
        var materials = new JArray();
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (var mat in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
            {
                var properties = new JObject();
                var propertyMetadata = new JObject();
                if (mat.shader != null)
                    for (int i = 0; i < mat.shader.GetPropertyCount(); i++)
                    {
                        var property = mat.shader.GetPropertyName(i);
                        propertyMetadata[property] = new JObject {
                            ["type"] = mat.shader.GetPropertyType(i).ToString(),
                            ["flags"] = mat.shader.GetPropertyFlags(i).ToString(),
                            ["attributes"] = new JArray(mat.shader.GetPropertyAttributes(i))
                        };
                        switch (mat.shader.GetPropertyType(i))
                        {
                            case ShaderPropertyType.Vector: var v = mat.GetVector(property); properties[property] = new JArray(v.x,v.y,v.z,v.w); break;
                            case ShaderPropertyType.Color: properties[property] = ColorValue(mat.GetColor(property)); break;
                            case ShaderPropertyType.Float:
                            case ShaderPropertyType.Range: properties[property] = mat.GetFloat(property); break;
                            case ShaderPropertyType.Texture:
                                var texture = mat.GetTexture(property);
                                var scale = mat.GetTextureScale(property);
                                var offset = mat.GetTextureOffset(property);
                                properties[property] = new JObject { ["asset"] = texture == null ? null : AssetDatabase.GetAssetPath(texture),
                                    ["scale"] = new JArray(scale.x, scale.y), ["offset"] = new JArray(offset.x, offset.y) };
                                break;
                        }
                    }
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mat, out string materialGuid, out long localFileID);
                materials.Add(new JObject { ["path"] = path, ["name"] = mat.name, ["guid"] = guid,
                    ["localFileID"] = localFileID, ["actualMaterialGUID"] = materialGuid,
                    ["shaderPath"] = mat.shader == null ? null : AssetDatabase.GetAssetPath(mat.shader), ["propertyMetadata"] = propertyMetadata,
                    ["shader"] = mat.shader == null ? null : mat.shader.name, ["supported"] = mat.shader != null && mat.shader.isSupported,
                    ["renderQueue"] = mat.renderQueue, ["keywords"] = new JArray(mat.shaderKeywords), ["properties"] = properties });
            }
        }
        var cameras = new JArray(Object.FindObjectsByType<Camera>(FindObjectsInactive.Include).Select(c => new JObject {
            ["name"] = c.name, ["active"] = c.isActiveAndEnabled, ["position"] = VectorValue(c.transform.position),
            ["rotation"] = VectorValue(c.transform.eulerAngles), ["fov"] = c.fieldOfView, ["hdr"] = c.allowHDR,
            ["background"] = ColorValue(c.backgroundColor), ["clearFlags"] = c.clearFlags.ToString() }));
        var lights = new JArray(Object.FindObjectsByType<Light>(FindObjectsInactive.Include).Select(l => new JObject {
            ["name"] = l.name, ["type"] = l.type.ToString(), ["color"] = ColorValue(l.color),
            ["intensity"] = l.intensity, ["range"] = l.range, ["shadows"] = l.shadows.ToString(),
            ["position"] = VectorValue(l.transform.position), ["rotation"] = VectorValue(l.transform.eulerAngles) }));
        var report = new JObject {
            ["unity"] = Application.unityVersion, ["stage"] = stage,
            ["pipeline"] = GraphicsSettings.currentRenderPipeline == null ? "Built-in" : GraphicsSettings.currentRenderPipeline.name,
            ["colorSpace"] = QualitySettings.activeColorSpace.ToString(), ["materials"] = materials,
            ["cameras"] = cameras, ["lights"] = lights,
            ["environment"] = new JObject { ["skybox"] = AssetDatabase.GetAssetPath(RenderSettings.skybox),
                ["ambientMode"] = RenderSettings.ambientMode.ToString(), ["ambientIntensity"] = RenderSettings.ambientIntensity,
                ["ambientSky"] = ColorValue(RenderSettings.ambientSkyColor), ["ambientEquator"] = ColorValue(RenderSettings.ambientEquatorColor),
                ["ambientGround"] = ColorValue(RenderSettings.ambientGroundColor), ["fog"] = RenderSettings.fog,
                ["fogColor"] = ColorValue(RenderSettings.fogColor), ["reflectionIntensity"] = RenderSettings.reflectionIntensity }
        };
        var output = Root + "/" + stage + "/appearance.json";
        File.WriteAllText(output, report.ToString());
        return $"Recorded {materials.Count} materials, {cameras.Count} cameras and {lights.Count} lights in {output}";
    }
    public static string CaptureViews(string stage)
    {
        Directory.CreateDirectory(Root + "/" + stage);
        var checkpoint = stage.Contains("/") ? stage.Substring(stage.IndexOf("/")+1) : "Static";
        var poseFile = Root + "/Baseline/camera-" + checkpoint + ".json";
        JObject pose;
        var original = Camera.main ?? Object.FindObjectsByType<Camera>(FindObjectsInactive.Include).FirstOrDefault(c => c.name.Contains("CenterEye"))
            ?? Object.FindObjectsByType<Camera>(FindObjectsInactive.Include).FirstOrDefault();
        if (original == null) throw new Exception("No camera available for reference captures.");
        if (File.Exists(poseFile)) pose = JObject.Parse(File.ReadAllText(poseFile));
        else {
            pose = new JObject { ["position"] = VectorValue(original.transform.position), ["rotation"] = VectorValue(original.transform.eulerAngles),
                ["fov"] = original.fieldOfView, ["near"] = original.nearClipPlane, ["far"] = original.farClipPlane,
                ["background"] = ColorValue(original.backgroundColor), ["clearFlags"] = (int)original.clearFlags, ["cullingMask"] = original.cullingMask };
            File.WriteAllText(poseFile, pose.ToString());
        }
        var go = new GameObject("Experience49 migration reference camera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.CopyFrom(original);
        cam.enabled = false;
        if (GraphicsSettings.currentRenderPipeline == null) cam.stereoTargetEye = StereoTargetEyeMask.None;
        else cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().allowXRRendering = false;
        var p = (JArray)pose["position"]; var r = (JArray)pose["rotation"];
        cam.transform.position = new Vector3((float)p[0], (float)p[1], (float)p[2]);
        cam.fieldOfView = (float)pose["fov"];
        cam.nearClipPlane = (float)pose["near"]; cam.farClipPlane = (float)pose["far"];
        cam.clearFlags = (CameraClearFlags)(int)pose["clearFlags"]; cam.cullingMask = (int)pose["cullingMask"];
        var bg = (JArray)pose["background"]; cam.backgroundColor = new Color((float)bg[0], (float)bg[1], (float)bg[2], (float)bg[3]);
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var observedRenderContexts = new JArray();
        try {
            cam.targetTexture = rt;
            foreach (var angle in new[] { 0, -45, 45, 90 }) {
                cam.transform.rotation = angle == 90 ? Quaternion.Euler((float)r[0]-60, (float)r[1], (float)r[2]) : Quaternion.Euler((float)r[0], (float)r[1] + angle, (float)r[2]);
                cam.Render(); RenderTexture.active = rt;
                observedRenderContexts.Add(RecordObservedRenderContext(cam, angle));
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                File.WriteAllBytes(Root + "/" + stage + "/view-" + angle + ".png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
        } finally { cam.targetTexture = null; RenderTexture.active = previous; Object.DestroyImmediate(rt); Object.DestroyImmediate(go); }
        File.WriteAllText(Root + "/" + stage + "/render-context.json", new JObject {
            ["stageLabel"] = stage, ["views"] = observedRenderContexts,
            ["limits"] = new JArray(
                "Captured immediately after each actual camera Render; no global values or environment settings reset.",
                "Shader.GetGlobalVector observes CPU lookup state; native per-camera GPU constant buffers may differ. Verify disputed uniforms with an authenticated GPU probe.",
                "Earlier renders in the same editor may have populated legacy globals. Fresh URP-only evidence is required for a durable fallback fix.")
        }.ToString());
        return "Captured four checkpoint views (90 = upward) in " + stage;
    }

    static JObject RecordObservedRenderContext(Camera camera, int viewID) {
        var globals = new JObject();
        foreach (var name in new[] { "glstate_lightmodel_ambient", "unity_IndirectSpecColor",
            "unity_AmbientSky", "unity_AmbientEquator", "unity_AmbientGround", "_GlossyEnvironmentColor",
            "unity_SHAr", "unity_SHAg", "unity_SHAb", "unity_SHBr", "unity_SHBg", "unity_SHBb", "unity_SHC", "_Time" }) {
            var value = Shader.GetGlobalVector(name);
            globals[name] = new JArray(value.x, value.y, value.z, value.w);
        }
        var probe = RenderSettings.ambientProbe;
        var channels = new JArray();
        for (int channel = 0; channel < 3; channel++) {
            var coefficients = new JArray();
            for (int coefficient = 0; coefficient < 9; coefficient++) coefficients.Add(probe[channel, coefficient]);
            channels.Add(coefficients);
        }
        return new JObject {
            ["viewID"] = viewID, ["processID"] = System.Diagnostics.Process.GetCurrentProcess().Id,
            ["unity"] = Application.unityVersion, ["playing"] = EditorApplication.isPlaying,
            ["editorSeconds"] = EditorApplication.timeSinceStartup,
            ["gameSeconds"] = Time.timeAsDouble, ["realtimeSeconds"] = Time.realtimeSinceStartupAsDouble,
            ["pipeline"] = GraphicsSettings.currentRenderPipeline == null ? "Built-in" : GraphicsSettings.currentRenderPipeline.name,
            ["colorSpace"] = QualitySettings.activeColorSpace.ToString(),
            ["graphicsDevice"] = SystemInfo.graphicsDeviceType.ToString(),
            ["cameraPosition"] = VectorValue(camera.transform.position),
            ["cameraRotation"] = VectorValue(camera.transform.eulerAngles), ["cameraHDR"] = camera.allowHDR,
            ["targetFormat"] = camera.targetTexture == null ? null : camera.targetTexture.format.ToString(),
            ["ambientMode"] = RenderSettings.ambientMode.ToString(),
            ["ambientIntensity"] = RenderSettings.ambientIntensity,
            ["ambientLightStored"] = ColorValue(RenderSettings.ambientLight),
            ["ambientLightLinear"] = ColorValue(RenderSettings.ambientLight.linear),
            ["ambientSkyStored"] = ColorValue(RenderSettings.ambientSkyColor),
            ["ambientSkyLinear"] = ColorValue(RenderSettings.ambientSkyColor.linear),
            ["ambientEquatorStored"] = ColorValue(RenderSettings.ambientEquatorColor),
            ["ambientGroundStored"] = ColorValue(RenderSettings.ambientGroundColor),
            ["skyboxAsset"] = RenderSettings.skybox == null ? null : AssetDatabase.GetAssetPath(RenderSettings.skybox),
            ["reflectionIntensity"] = RenderSettings.reflectionIntensity,
            ["ambientProbeRGBx9"] = channels, ["cpuGlobalLookups"] = globals
        };
    }
}

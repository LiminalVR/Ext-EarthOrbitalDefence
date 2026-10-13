// Run outside Assets via project-scoped Pipeline after conversion, while stopped.
// Reads current materials; it does not change assets or infer rendered parity.
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BaselineSmoothnessAudit
{
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play before auditing current assets.");
        if (GraphicsSettings.currentRenderPipeline == null) throw new Exception("Configure the final URP stage first.");
        var source = JObject.Parse(File.ReadAllText("Builds/URPMigration/BuiltIn/appearance.json"));
        const string remapPath = "Builds/URPMigration/URP/additional-conversion.json";
        var remaps = File.Exists(remapPath) ? JArray.Parse(File.ReadAllText(remapPath)) : new JArray();
        var checks = new JArray(); var problems = new JArray(); var excluded = new JArray();
        foreach (var entry in source["materials"])
        {
            string shader = (string)entry["shader"];
            if (shader != "Standard" && shader != "Standard (Specular setup)") continue;
            string path = (string)entry["path"];
            string targetPath = path;
            bool imported = !path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase);
            if (imported) {
                var matches = remaps.Where(row => (string)row["path"] == path && (string)row["material"] == (string)entry["name"] && row["extracted"] != null).ToArray();
                int sourceMatches = source["materials"].Count(row => (string)row["path"] == path && (string)row["name"] == (string)entry["name"]);
                if (matches.Length != 1 || sourceMatches != 1) {
                    excluded.Add(new JObject { ["path"] = path, ["name"] = entry["name"], ["reason"] = "Imported subasset needs one unambiguous preserved-source/extraction record.", ["sourceMatches"] = sourceMatches, ["remapMatches"] = matches.Length });
                    continue;
                }
                targetPath = (string)matches[0]["extracted"];
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                var current = importer?.GetExternalObjectMap().Where(row => row.Key.type == typeof(Material) && row.Key.name == (string)entry["name"]).Select(row => row.Value).ToArray();
                if (!targetPath.StartsWith("Assets/", StringComparison.Ordinal) || AssetDatabase.AssetPathToGUID(path) != (string)entry["guid"] || current == null || current.Length != 1 || AssetDatabase.GetAssetPath(current[0]) != targetPath) {
                    excluded.Add(new JObject { ["path"] = path, ["name"] = entry["name"], ["destination"] = targetPath, ["reason"] = "Current source GUID/model external remap does not authenticate the recorded project copy." });
                    continue;
                }
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            if (material == null || material.shader == null || (material.shader.name != "Universal Render Pipeline/Lit" && material.shader.name != "Liminal/URP/Lit Built-in Detail")) {
                excluded.Add(new JObject { ["path"] = path, ["destination"] = targetPath, ["reason"] = "Destination is missing or outside the verified stock/compatibility Lit families." });
                continue;
            }
            var keywords = (JArray)entry["keywords"];
            bool sourceAlpha = Contains(keywords, "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            bool sourceGlossMap = Contains(keywords, shader == "Standard" ? "_METALLICGLOSSMAP" : "_SPECGLOSSMAP");
            string sourceProperty = sourceAlpha || sourceGlossMap ? "_GlossMapScale" : "_Glossiness";
            var expectedToken = entry["properties"]?[sourceProperty];
            if (expectedToken == null) {
                excluded.Add(new JObject { ["path"] = path, ["reason"] = "Source scalar absent from preserved inventory.", ["property"] = sourceProperty });
                continue;
            }
            float expected = (float)expectedToken;
            float actual = material.GetFloat("_Smoothness");
            bool targetAlpha = material.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            var row = new JObject {
                ["path"] = path, ["name"] = entry["name"], ["sourceGUID"] = entry["guid"],
                ["destination"] = targetPath, ["destinationGUID"] = AssetDatabase.AssetPathToGUID(targetPath),
                ["importedRemap"] = imported, ["sourceShader"] = shader, ["targetShader"] = material.shader.name,
                ["sourceProperty"] = sourceProperty, ["sourceValue"] = expected, ["targetSmoothness"] = actual,
                ["sourceAlbedoAlpha"] = sourceAlpha, ["targetAlbedoAlpha"] = targetAlpha,
                ["sourceGlossMap"] = sourceGlossMap,
                ["targetSmoothnessTextureChannel"] = material.GetFloat("_SmoothnessTextureChannel"),
                ["sourceRenderingMode"] = entry["properties"]?["_Mode"]
            };
            checks.Add(row);
            if (Mathf.Abs(expected - actual) > Mathf.Max(0.00001f, Mathf.Abs(expected) * 0.000001f) || sourceAlpha != targetAlpha)
                problems.Add(row.DeepClone());
        }
        var report = new JObject {
            ["checked"] = checks.Count, ["mismatchCount"] = problems.Count, ["checks"] = checks,
            ["mismatches"] = problems, ["excluded"] = excluded,
            ["actualPipeline"] = RenderPipelineManager.currentPipeline?.GetType().FullName,
            ["limits"] = new JArray("Readonly scalar/keyword audit, not rendered parity or complete material semantics.",
                "Imported copies require a unique preserved path/name, unchanged source GUID and current ModelImporter external remap. Preserved inventories lack subasset file IDs; ambiguous names are excluded.",
                "Package materials, custom destinations, runtime writes and excluded rows require separate source-backed review.",
                "Do not blanket-fix constant smoothness or texture-alpha inputs; preserve the original Standard branch.")
        };
        Directory.CreateDirectory("Builds/URPMigration/URP");
        File.WriteAllText("Builds/URPMigration/URP/standard-smoothness-audit.json", report.ToString());
        return $"Checked {checks.Count} Standard smoothness mappings; {problems.Count} mismatches, {excluded.Count} explicit exclusions.";
    }

    static bool Contains(JArray keywords, string value)
    {
        if (keywords == null) return false;
        foreach (var keyword in keywords) if ((string)keyword == value) return true;
        return false;
    }
}

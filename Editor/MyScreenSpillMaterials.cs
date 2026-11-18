using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyScreen.Spill.Editor
{
    /// <summary>
    /// Puts materials on "MyScreen/Lit Spill" (and back). The spill shader has URP Lit's properties and Inspector, so
    /// a URP Lit material converts by swapping its shader: its textures, colours and settings stay. Other shaders are
    /// left alone (listed in the Console), since their properties would not carry over.
    /// </summary>
    public static class MyScreenSpillMaterials
    {
        public const string SpillShader = "MyScreen/Lit Spill";
        public const string LitShader = "Universal Render Pipeline/Lit";

        [MenuItem("MyScreen/Spill/Convert Selected Materials", false, 30)]
        public static void ConvertSelected()
        {
            Report(Swap(Selected(), LitShader, SpillShader), "converted to " + SpillShader);
        }

        [MenuItem("MyScreen/Spill/Convert Materials in Open Scene", false, 31)]
        public static void ConvertScene()
        {
            if (!EditorUtility.DisplayDialog("MyScreen Spill", "Put every URP Lit material used in the open scene on " + SpillShader + "? The materials themselves change (the assets), so every scene that uses them gets the spill. Undo works.", "Convert", "Cancel"))
                return;
            Report(Swap(InScene(SceneManager.GetActiveScene()), LitShader, SpillShader), "converted to " + SpillShader);
        }

        [MenuItem("MyScreen/Spill/Revert Selected Materials to URP Lit", false, 32)]
        public static void RevertSelected()
        {
            Report(Swap(Selected(), SpillShader, LitShader), "back on " + LitShader);
        }

        /// <summary>The selected materials, and those of the selected objects' renderers (children included).</summary>
        static List<Material> Selected()
        {
            var found = new List<Material>();
            foreach (var o in Selection.objects)
            {
                if (o is Material m) Add(found, m);
                else if (o is GameObject go)
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                        foreach (var rm in r.sharedMaterials) Add(found, rm);
            }
            return found;
        }

        static List<Material> InScene(Scene scene)
        {
            var found = new List<Material>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials) Add(found, m);
            return found;
        }

        static void Add(List<Material> list, Material m)
        {
            if (m != null && !list.Contains(m)) list.Add(m);
        }

        static (int done, List<string> skipped) Swap(List<Material> materials, string from, string to)
        {
            var target = Shader.Find(to);
            if (target == null)
            {
                EditorUtility.DisplayDialog("MyScreen Spill", "Shader \"" + to + "\" was not found. Let Unity finish importing the package (URP must be installed), then try again.", "OK");
                return (0, new List<string>());
            }
            var done = 0;
            var skipped = new List<string>();
            foreach (var m in materials)
            {
                if (m.shader != null && m.shader.name == to) continue;
                if (m.shader == null || m.shader.name != from)
                {
                    skipped.Add(m.name + " (" + (m.shader != null ? m.shader.name : "no shader") + ")");
                    continue;
                }
                Undo.RecordObject(m, "MyScreen Spill materials");
                // Only the shader changes: a material on its shader's own render queue (-1) stays on it.
                m.shader = target;
                EditorUtility.SetDirty(m);
                done++;
            }
            if (done > 0)
            {
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            return (done, skipped);
        }

        static void Report((int done, List<string> skipped) r, string what)
        {
            Debug.Log("[MyScreen Spill] " + r.done + " material(s) " + what + "." +
                      (r.skipped.Count > 0 ? " Left alone (not URP Lit): " + string.Join(", ", r.skipped) : ""));
        }
    }
}

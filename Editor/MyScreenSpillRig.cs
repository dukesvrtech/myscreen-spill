using System;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MyScreen.Spill.Editor
{
    /// <summary>
    /// Builds the MyScreen Spill rig in the open scene (MyScreen > Spill > Add Spill Rig). Running it again updates the
    /// rig in place and keeps where its screen marker stands. The rig is plain Unity plus one Visual Scripting graph,
    /// so it passes the Banter SDK's world checks; MyScreen's gateway script drives it at run time:
    ///
    ///   MyScreenSpill (far below the world, out of sight)
    ///     MyScreenSpillTarget   MyScreen puts the TV's picture on a plane under it
    ///     MyScreenSpillCamera   films the target into the capture render texture
    ///     MyScreenSpillState    local position x = 1 for a 2D picture (0: half-SBS 3D), y = strength (1 normal)
    ///     MyScreenSpillHub      the graph: sets the spill's shader globals from the markers
    ///   MyScreenSpillScreen     where the light comes from: centre, facing (-Z is the front), scale = width x height
    ///
    /// See README.md (for Unity and shader developers) and AGENTS.md (for AI assistants).
    /// </summary>
    public static class MyScreenSpillRig
    {
        public const string RootName = "MyScreenSpill";
        public const string TargetName = "MyScreenSpillTarget";
        public const string CameraName = "MyScreenSpillCamera";
        public const string StateName = "MyScreenSpillState";
        public const string HubName = "MyScreenSpillHub";
        public const string ScreenName = "MyScreenSpillScreen";
        public const string CaptureFolder = "Assets/MyScreenSpill";
        public const string CapturePath = CaptureFolder + "/MyScreenSpillCapture.renderTexture";

        /// <summary>Out of everyone's sight: the camera there sees only the target's picture.</summary>
        static readonly Vector3 RigPosition = new Vector3(0f, -1000f, 0f);
        /// <summary>The capture's size: the spill samples blurred mips only, and a bigger capture costs Quest frame time.</summary>
        const int CaptureWidth = 480;
        const int CaptureHeight = 270;
        /// <summary>The camera's half height: it sees 1.49 x 0.84 m, a little less than the picture plane MyScreen makes.</summary>
        const float CaptureHalfHeight = 0.42f;
        /// <summary>The hub sends every value at Start and then every this many frames (the screen may move).</summary>
        const int HubEveryFrames = 10;

        // The defaults a new rig gets (the hub's variables; the developer tunes them on MyScreenSpillHub).
        public const float DefaultRadius = 18f;
        public const float DefaultIntensity = 1.5f;
        /// <summary>The smear as a mip level of the 480-wide capture (Dukelines' look: LOD 3.6 on a 1920-wide one).</summary>
        public const float DefaultMip = 1.6f;

        [MenuItem("MyScreen/Spill/Add or Update Spill Rig", false, 10)]
        public static void AddOrUpdate()
        {
            try
            {
                var screen = Build(SceneManager.GetActiveScene());
                Selection.activeGameObject = screen;
                EditorGUIUtility.PingObject(screen);
                Debug.Log("[MyScreen Spill] The rig is in the scene. Move MyScreenSpillScreen onto your screen (its front is -Z, its scale is the screen's width and height), then convert your materials (MyScreen > Spill > Convert Selected Materials).");
            }
            catch (Exception err)
            {
                EditorUtility.DisplayDialog("MyScreen Spill", err.Message, "OK");
            }
        }

        [MenuItem("MyScreen/Spill/Select Spill Hub (tuning)", false, 11)]
        public static void SelectHub()
        {
            var hub = GameObject.Find(RootName + "/" + HubName);
            if (hub == null)
            {
                EditorUtility.DisplayDialog("MyScreen Spill", "There is no spill rig in this scene: MyScreen > Spill > Add or Update Spill Rig.", "OK");
                return;
            }
            Selection.activeGameObject = hub;
            EditorGUIUtility.PingObject(hub);
        }

        /// <summary>Builds or updates the rig in `scene`. Returns the screen marker.</summary>
        public static GameObject Build(Scene scene)
        {
            var objectId = BanterType("BS.BSObjectId");
            if (objectId == null)
                throw new InvalidOperationException("The Banter SDK is not in this project (no BS.BSObjectId). Install it first: MyScreen drives the rig through it.");

            var root = FindRoot(scene, RootName) ?? Created(new GameObject(RootName));
            root.transform.SetPositionAndRotation(RigPosition, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            var target = Child(root.transform, TargetName, Vector3.zero);
            Ensure(target, objectId);
            var state = Child(root.transform, StateName, new Vector3(0f, 1f, 0f), keepPosition: true);
            Ensure(state, objectId);
            var camera = Child(root.transform, CameraName, new Vector3(0f, 0f, -0.25f));
            ConfigureCamera(camera, EnsureCapture());
            var hub = Child(root.transform, HubName, Vector3.zero);

            var screen = FindRoot(scene, ScreenName);
            if (screen == null)
            {
                screen = Created(new GameObject(ScreenName));
                // A 4 m screen 2 m up, 5 m ahead of the origin, facing back at it: move it onto your screen.
                screen.transform.SetPositionAndRotation(new Vector3(0f, 2f, 5f), Quaternion.identity);
                screen.transform.localScale = new Vector3(4f, 2.25f, 1f);
            }
            Ensure(screen, objectId);

            WireHub(hub, screen, state);
            EditorSceneManager.MarkSceneDirty(scene);
            return screen;
        }

        static void WireHub(GameObject hub, GameObject screen, GameObject state)
        {
            var variables = Component<Variables>(hub);
            Undo.RecordObject(variables, "MyScreen Spill hub");
            var decls = variables.declarations;
            decls.Set("Screen", screen);
            decls.Set("State", state);
            decls.Set("Capture", AssetDatabase.LoadAssetAtPath<Texture>(CapturePath));
            if (!decls.IsDefined("Radius")) decls.Set("Radius", DefaultRadius);
            if (!decls.IsDefined("Intensity")) decls.Set("Intensity", DefaultIntensity);
            if (!decls.IsDefined("Mip")) decls.Set("Mip", DefaultMip);
            EditorUtility.SetDirty(variables);

            var machine = Component<ScriptMachine>(hub);
            Undo.RecordObject(machine, "MyScreen Spill graph");
            machine.nest.source = GraphSource.Embed;
            machine.nest.embed = MyScreenSpillGraph.Build(HubEveryFrames);
            EditorUtility.SetDirty(machine);
        }

        static RenderTexture EnsureCapture()
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(CapturePath);
            if (rt != null) return rt;
            if (!AssetDatabase.IsValidFolder(CaptureFolder))
                AssetDatabase.CreateFolder("Assets", "MyScreenSpill");
            // Mips for the smear; the 16-bit depth stays because URP's render graph refuses a camera output
            // texture without a depth buffer.
            rt = new RenderTexture(CaptureWidth, CaptureHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "MyScreenSpillCapture",
                antiAliasing = 1,
                useMipMap = true,
                autoGenerateMips = true,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            AssetDatabase.CreateAsset(rt, CapturePath);
            return AssetDatabase.LoadAssetAtPath<RenderTexture>(CapturePath);
        }

        static void ConfigureCamera(GameObject go, RenderTexture rt)
        {
            go.transform.localRotation = Quaternion.identity;
            var cam = Component<Camera>(go);
            Undo.RecordObject(cam, "MyScreen Spill camera");
            cam.orthographic = true;
            cam.orthographicSize = CaptureHalfHeight;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 0.45f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.targetTexture = rt;
            cam.depth = -20;
            cam.enabled = true;
            var urp = Component<UniversalAdditionalCameraData>(go);
            Undo.RecordObject(urp, "MyScreen Spill camera (URP)");
            urp.renderType = CameraRenderType.Base;
            urp.renderPostProcessing = false;
            urp.antialiasing = AntialiasingMode.None;
            urp.renderShadows = false;
            var listener = go.GetComponent<AudioListener>();
            if (listener != null) Undo.DestroyObjectImmediate(listener);
        }

        static GameObject Child(Transform parent, string name, Vector3 localPosition, bool keepPosition = false)
        {
            var t = parent.Find(name);
            if (t != null)
            {
                if (!keepPosition) t.localPosition = localPosition;
                return t.gameObject;
            }
            var go = Created(new GameObject(name));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        /// <summary>The object's T, added when it has none. (Unity's missing components compare equal to null but are not
        /// C# null, so the null-coalescing operator cannot be used for this.)</summary>
        static T Component<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : Undo.AddComponent<T>(go);
        }

        static GameObject Created(GameObject go)
        {
            Undo.RegisterCreatedObjectUndo(go, "MyScreen Spill rig");
            return go;
        }

        static void Ensure(GameObject go, Type component)
        {
            if (go.GetComponent(component) == null) Undo.AddComponent(go, component);
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == name) return r;
            return null;
        }

        /// <summary>A Banter SDK type by its full name, from whichever assembly the SDK ships it in.</summary>
        public static Type BanterType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        // The screen marker, drawn in the Scene view so it can be fitted to the screen.
        [InitializeOnLoadMethod]
        static void DrawMarker()
        {
            SceneView.duringSceneGui -= OnScene;
            SceneView.duringSceneGui += OnScene;
        }

        static void OnScene(SceneView view)
        {
            var screen = GameObject.Find(ScreenName);
            if (screen == null) return;
            var t = screen.transform;
            var hx = t.right * Mathf.Abs(t.lossyScale.x) * 0.5f;
            var hy = t.up * Mathf.Abs(t.lossyScale.y) * 0.5f;
            var c = t.position;
            Handles.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            Handles.DrawPolyLine(c - hx - hy, c + hx - hy, c + hx + hy, c - hx + hy, c - hx - hy);
            Handles.DrawLine(c, c - t.forward * 1.5f);
            Handles.Label(c - t.forward * 1.6f, "MyScreen spill (front)");
        }
    }
}

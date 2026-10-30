#if UNITY_EDITOR
// Farm Chaos - turns the Blender chibi (Assets/Models/Chibi/Chibi.fbx) into the player.
// Menu: Tools > Farm Chaos > 22. Build Chibi Player   (now builds Bean Buddy from Art/bean_buddy.py)
//  - import settings (looping clips, URP materials by slot name, crisp face pictures)
//  - Chibi.controller (Idle/Walk/Run blend by speed, Jump in the air, Wave + Cheer emotes)
//  - Assets/Prefabs/Chibi_Player.prefab (ChibiMotor + ChibiLook)
//  - puts the chibi on your ball in Farm_Main + Lobby, and on other players' balls (Net_Player prefab)
//  - adds the Customize / wardrobe button to the Lobby
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FarmChibiBuilder
{
    const string Dir = "Assets/Models/Chibi";
    const string Fbx = Dir + "/Chibi.fbx";
    const string MatDir = Dir + "/Materials";
    const string ControllerPath = Dir + "/Chibi.controller";
    const string PrefabPath = "Assets/Prefabs/Chibi_Player.prefab";
    const string NetPrefab = "Assets/Prefabs/Online/Net_Player.prefab";
    static readonly string[] Scenes = { "Assets/Scenes/Farm_Main.unity", "Assets/Scenes/Lobby.unity" };
    static readonly string[] FaceNames = { "Smile", "Happy", "Surprised", "Angry", "Sad", "Sleepy", "Wink", "Determined", "Love" };
    const float TargetHeight = 1.25f;

    [MenuItem("Tools/Farm Chaos/22. Build Chibi Player")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        if (!File.Exists(Fbx)) { EditorUtility.DisplayDialog("Farm Chaos", "Chibi.fbx not found in " + Dir, "OK"); return; }
        EditorSceneManager.SaveOpenScenes();
        string returnTo = EditorSceneManager.GetActiveScene().path;

        var faces = FaceTextures();
        var mats = Materials(faces[0]);
        ImportSettings(mats);
        var controller = Controller();
        var prefab = MakePrefab(controller, faces);

        foreach (var sp in Scenes) if (File.Exists(sp)) PutInScene(sp, prefab);
        PutOnNetPlayer(prefab);
        if (!string.IsNullOrEmpty(returnTo) && File.Exists(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log("[Farm Chaos] Chibi player built. Press Play: move = walk/run, Space = jump, V = wave, C = cheer. Lobby: Customize button.");
    }

    // ------------------------------------------------------------ faces + materials
    static Texture2D[] FaceTextures()
    {
        var list = new Texture2D[FaceNames.Length];
        for (int i = 0; i < FaceNames.Length; i++)
        {
            string p = $"{Dir}/Faces/Face_{FaceNames[i]}.png";
            if (AssetImporter.GetAtPath(p) is TextureImporter ti)
            {
                bool dirty = ti.wrapMode != TextureWrapMode.Clamp || !ti.alphaIsTransparency;
                ti.wrapMode = TextureWrapMode.Clamp; ti.alphaIsTransparency = true; ti.mipmapEnabled = true;
                if (dirty) ti.SaveAndReimport();
            }
            list[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        return list;
    }

    static readonly (string slot, string hex)[] Slots =
    {
        ("Skin", "FFE3D3"), ("Shirt", "FFFFFF"), ("Socks", "FFFFFF"), ("Outfit", "8EC5F0"), ("Shoes", "A87B5C"), ("Hair", "7A5240"),
        ("Accent", "F4B8C1"), ("Face", "FFFFFF"), ("HatMain", "E9CF7A"), ("HatAccent", "F28FB1"),
        ("FixedWhite", "FFFFFF"), ("FixedRed", "E0505C"), ("FixedOrange", "F5A623"), ("FixedInk", "2E2A3A"),
        ("FixedLeaf", "7FC98A"), ("FixedYellow", "FFE07A"),
        // Bean Buddy face pieces
        ("FixedEyeRim", "2A2230"), ("FixedEyeWhite", "FFFFFF"), ("FixedPupil", "120F16"), ("FixedPupilShade", "57515F"),
        ("FixedHighlight", "FFFFFF"), ("FixedBrow", "3A2A33"), ("FixedMouth", "4E2C38"), ("FixedTongue", "F28A99"),
        ("FixedBlush", "F7A1B6"), ("FixedTear", "9FD3F7"),
    };

    static System.Collections.Generic.Dictionary<string, Material> Materials(Texture2D smile)
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(Dir, "Materials");
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var d = new System.Collections.Generic.Dictionary<string, Material>();
        foreach (var (slot, hex) in Slots)
        {
            string p = $"{MatDir}/Chibi_{slot}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(lit) { name = "Chibi_" + slot }; AssetDatabase.CreateAsset(m, p); }
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", slot == "Shoes" || slot.StartsWith("Hat") ? 0.35f : 0.18f);
            if (slot == "Face")
            {
                m.SetTexture("_BaseMap", smile);
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Smoothness", 0f);
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            if (slot == "Hair" || slot.StartsWith("Hat")) { m.SetFloat("_Cull", 0f); m.doubleSidedGI = true; }   // hair caps are open shells
            if (slot == "FixedPupil" || slot == "FixedPupilShade" || slot == "FixedEyeWhite" || slot == "FixedTear") m.SetFloat("_Smoothness", 0.8f);   // glossy eyes
            if (slot == "FixedHighlight" || slot == "FixedEyeWhite")
            {
                m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor("_EmissionColor", Color.white * (slot == "FixedHighlight" ? 0.8f : 0.12f));
            }
            EditorUtility.SetDirty(m);
            d[slot] = m;
        }
        AssetDatabase.SaveAssets();
        return d;
    }

    static void ImportSettings(System.Collections.Generic.Dictionary<string, Material> mats)
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.importAnimation = true;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        foreach (var kv in mats) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            string n = c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name;
            c.name = n;
            c.loopTime = n != "Jump" && n != "Fall";
            c.loopPose = false;
        }
        mi.clipAnimations = clips;
        mi.SaveAndReimport();
    }

    // ------------------------------------------------------------ animator
    static AnimationClip Clip(string name) =>
        AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == name && !c.name.StartsWith("__preview__"));

    static AnimatorController Controller()
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
        ac.AddParameter("Wave", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Cheer", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Fall", AnimatorControllerParameterType.Trigger);
        var sm = ac.layers[0].stateMachine;

        var loco = ac.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
        tree.AddChild(Clip("Idle"), 0f);
        tree.AddChild(Clip("Walk"), 2f);
        tree.AddChild(Clip("Run"), 6f);
        sm.defaultState = loco;

        var air = sm.AddState("Air"); air.motion = Clip("Jump");
        var wave = sm.AddState("Wave"); wave.motion = Clip("Wave");
        var cheer = sm.AddState("Cheer"); cheer.motion = Clip("Cheer");

        var toAir = sm.AddAnyStateTransition(air);
        toAir.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded"); toAir.hasExitTime = false; toAir.duration = 0.08f; toAir.canTransitionToSelf = false;
        var land = air.AddTransition(loco); land.AddCondition(AnimatorConditionMode.If, 0, "Grounded"); land.hasExitTime = false; land.duration = 0.1f;

        foreach (var (state, trig, loops) in new[] { (wave, "Wave", 2f), (cheer, "Cheer", 2f) })
        {
            var t = loco.AddTransition(state); t.AddCondition(AnimatorConditionMode.If, 0, trig); t.hasExitTime = false; t.duration = 0.12f;
            var back = state.AddTransition(loco); back.hasExitTime = true; back.exitTime = loops; back.duration = 0.2f;
        }
        // knocked over (earthquake): from anywhere, then back to walking
        var fallClip = Clip("Fall");
        if (fallClip != null)
        {
            var fall = sm.AddState("Fall"); fall.motion = fallClip;
            var toFall = sm.AddAnyStateTransition(fall);
            toFall.AddCondition(AnimatorConditionMode.If, 0, "Fall"); toFall.hasExitTime = false; toFall.duration = 0.05f; toFall.canTransitionToSelf = false;
            var up = fall.AddTransition(loco); up.hasExitTime = true; up.exitTime = 0.97f; up.duration = 0.1f;
        }
        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        return ac;
    }

    // ------------------------------------------------------------ prefab
    static GameObject MakePrefab(AnimatorController controller, Texture2D[] faces)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        var root = new GameObject("Chibi");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        inst.name = "Model";
        inst.transform.SetParent(root.transform, false);

        // size: measure the character without hats (feet to top of head)
        System.Func<Bounds> measure = () =>
        {
            var rs = inst.GetComponentsInChildren<Renderer>(true).Where(r => !r.name.StartsWith("Hat_")).ToArray();
            var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
            return bb;
        };
        inst.transform.localScale *= TargetHeight / Mathf.Max(0.01f, measure().size.y);
        inst.transform.localPosition -= new Vector3(0, measure().min.y, 0);   // feet at the root

        var anim = inst.GetComponent<Animator>();
        if (anim == null) anim = inst.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var look = root.AddComponent<ChibiLook>();
        look.renderers = inst.GetComponentsInChildren<Renderer>(true);
        look.faces = faces;
        root.AddComponent<BeanWobble>();                 // floppy head + arms
        var motor = root.AddComponent<ChibiMotor>();
        motor.animator = anim; motor.look = look;
        foreach (var r in look.renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void Attach(Transform ball, GameObject prefab, float radius, bool savedLook)
    {
        foreach (var old in ball.Cast<Transform>().Where(t => t.name == "Chibi").ToList()) Object.DestroyImmediate(old.gameObject);
        var c = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ball);
        c.name = "Chibi";
        c.transform.localPosition = Vector3.zero; c.transform.localRotation = Quaternion.identity;
        var ls = ball.lossyScale;
        c.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);   // undo the ball's scale
        var m = c.GetComponent<ChibiMotor>(); m.ballRadius = radius;
        c.GetComponent<ChibiLook>().loadSaved = savedLook;
    }

    static void PutInScene(string path, GameObject prefab)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        foreach (var p in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SpherePlayer>(true)))
        {
            var sc = p.GetComponent<SphereCollider>();
            float radius = sc != null ? sc.radius * p.transform.lossyScale.y : 0.6f;
            Attach(p.transform, prefab, radius, true);
        }
        if (path.EndsWith("Lobby.unity"))
        {
            var ui = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LobbyUI>(true)).FirstOrDefault();
            if (ui != null)
            {
                var w = ui.GetComponent<ChibiWardrobeUI>();
                if (w == null) w = ui.gameObject.AddComponent<ChibiWardrobeUI>();
                w.rounded = FarmLobbyPolish.RoundedSprite();
                EditorUtility.SetDirty(w);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void PutOnNetPlayer(GameObject prefab)
    {
        if (!File.Exists(NetPrefab)) return;
        var root = PrefabUtility.LoadPrefabContents(NetPrefab);
        Attach(root.transform, prefab, 0.6f, false);   // other players: coloured by their party colour
        PrefabUtility.SaveAsPrefabAsset(root, NetPrefab);
        PrefabUtility.UnloadPrefabContents(root);
    }
}
#endif

#if UNITY_EDITOR
// Farm Chaos - cozy night mode: moonlight, stars, cute glowing lamp posts along the paths,
// glowing windows, fireflies, a little lantern-light on the player and soft bloom.
// Menu: Tools > Farm Chaos > 6. Lighting: Cozy Night   /   Lighting: Day
// Phone-friendly: lamps use painted-on light pools instead of dozens of real lights.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class FarmNightBuilder
{
    const string Root = "Farm_Night";
    const string Folder = "Assets/Lighting";
    const string SnapshotPath = Folder + "/DayLightingSnapshot.asset";
    const string PlayerLightName = "Night_PlayerLantern";

    static readonly Color Warm = new Color(1f, 0.78f, 0.48f);

    // =========================================================== NIGHT
    [MenuItem("Tools/Farm Chaos/6. Lighting: Cozy Night")]
    public static void Night()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        var scene = SceneManager.GetActiveScene();
        EnsureFolder();
        var sun = FindSun();
        var cam = FindCamera();
        Snapshot(sun, cam);               // only the first time, so Day can go back to the original

        foreach (var r in scene.GetRootGameObjects()) if (r.name == Root) Undo.DestroyObjectImmediate(r);
        var root = new GameObject(Root); Undo.RegisterCreatedObjectUndo(root, "Cozy Night");

        // ---- moonlight + ambient
        if (sun != null)
        {
            Undo.RecordObject(sun, "Cozy Night"); Undo.RecordObject(sun.transform, "Cozy Night");
            sun.color = new Color32(0x9C, 0xAE, 0xE8, 255);
            sun.intensity = 0.45f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sun.shadowStrength = 0.6f;
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color32(0x4A, 0x50, 0x8A, 255);
        RenderSettings.ambientEquatorColor = new Color32(0x3A, 0x3B, 0x6A, 255);
        RenderSettings.ambientGroundColor = new Color32(0x22, 0x22, 0x3C, 255);
        RenderSettings.fog = false;

        if (cam != null)
        {
            Undo.RecordObject(cam, "Cozy Night");
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x14, 0x17, 0x30, 255);
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data != null) { Undo.RecordObject(data, "Cozy Night"); data.renderPostProcessing = true; }
        }

        var mats = Materials();
        float groundY = GroundY();

        Sky(root.transform, mats);
        int lamps = Lamps(root.transform, mats, groundY);
        BuildingGlow(root.transform, mats, groundY, true);
        Fireflies(root.transform, mats);
        PlayerLantern(true);
        PostFX(root.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Farm Chaos] Cozy night on: {lamps} lamp posts placed (scene saved). Tools > Farm Chaos > Lighting: Day switches back.");
    }

    // =========================================================== DAY
    [MenuItem("Tools/Farm Chaos/Lighting: Day")]
    public static void Day()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        var scene = SceneManager.GetActiveScene();
        foreach (var r in scene.GetRootGameObjects()) if (r.name == Root) Undo.DestroyObjectImmediate(r);
        var snap = AssetDatabase.LoadAssetAtPath<FarmDaySnapshot>(SnapshotPath);
        var sun = FindSun(); var cam = FindCamera();
        if (snap != null)
        {
            if (sun != null)
            {
                Undo.RecordObject(sun, "Day"); Undo.RecordObject(sun.transform, "Day");
                sun.color = snap.sunColor; sun.intensity = snap.sunIntensity; sun.transform.rotation = snap.sunRotation; sun.shadowStrength = 1f;
            }
            RenderSettings.ambientMode = (AmbientMode)snap.ambientMode;
            RenderSettings.ambientSkyColor = snap.ambientSky; RenderSettings.ambientEquatorColor = snap.ambientEquator;
            RenderSettings.ambientGroundColor = snap.ambientGround; RenderSettings.ambientLight = snap.ambientLight;
            RenderSettings.ambientIntensity = snap.ambientIntensity;
            RenderSettings.fog = snap.fog;
            if (snap.skybox != null) RenderSettings.skybox = snap.skybox;
            if (cam != null)
            {
                Undo.RecordObject(cam, "Day");
                cam.clearFlags = (CameraClearFlags)snap.camClearFlags; cam.backgroundColor = snap.camBackground;
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data != null) { Undo.RecordObject(data, "Day"); data.renderPostProcessing = snap.camPostProcessing; }
            }
        }
        BuildingGlow(null, Materials(), 0, false);
        PlayerLantern(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Farm Chaos] Daytime lighting restored (scene saved).");
    }

    // =========================================================== pieces
    static Light FindSun() =>
        Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);

    static Camera FindCamera()
    {
        var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        return cams.FirstOrDefault(c => c.CompareTag("MainCamera")) ?? (cams.Length > 0 ? cams[0] : null);
    }

    static float GroundY()
    {
        var land = AllTransforms().FirstOrDefault(t => t.name == "land");
        return land != null && land.GetComponent<Renderer>() != null ? land.GetComponent<Renderer>().bounds.max.y : 0f;
    }

    static List<Transform> AllTransforms() =>
        SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Lighting");
    }

    static void Snapshot(Light sun, Camera cam)
    {
        if (AssetDatabase.LoadAssetAtPath<FarmDaySnapshot>(SnapshotPath) != null) return;
        var s = ScriptableObject.CreateInstance<FarmDaySnapshot>();
        if (sun != null) { s.sunColor = sun.color; s.sunIntensity = sun.intensity; s.sunRotation = sun.transform.rotation; }
        s.ambientMode = (int)RenderSettings.ambientMode;
        s.ambientSky = RenderSettings.ambientSkyColor; s.ambientEquator = RenderSettings.ambientEquatorColor;
        s.ambientGround = RenderSettings.ambientGroundColor; s.ambientLight = RenderSettings.ambientLight;
        s.ambientIntensity = RenderSettings.ambientIntensity;
        s.fog = RenderSettings.fog; s.skybox = RenderSettings.skybox;
        if (cam != null)
        {
            s.camClearFlags = (int)cam.clearFlags; s.camBackground = cam.backgroundColor;
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            s.camPostProcessing = data != null && data.renderPostProcessing;
        }
        AssetDatabase.CreateAsset(s, SnapshotPath);
    }

    // ---------- materials
    static Dictionary<string, Material> Materials()
    {
        EnsureFolder();
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        return new Dictionary<string, Material>
        {
            ["pole"] = Mat("Mat_lamp_pole", lit, new Color32(0x6C, 0x5B, 0x7B, 255)),
            ["cap"] = Mat("Mat_lamp_cap", lit, new Color32(0xF4, 0xB8, 0xC1, 255)),
            ["mint"] = Mat("Mat_lamp_mint", lit, new Color32(0xC8, 0xE6, 0xD0, 255)),
            ["glow"] = Mat("Mat_lamp_glow", lit, new Color32(0xFF, 0xE2, 0xA8, 255), new Color(1f, 0.72f, 0.38f) * 3.2f),
            ["moon"] = Mat("Mat_moon", unlit, new Color(1.6f, 1.55f, 1.3f)),
            ["star"] = Mat("Mat_stars", unlit, new Color(1.8f, 1.8f, 2.0f)),
            ["pool"] = Additive("Mat_light_pool", new Color(1f, 0.72f, 0.4f, 0.55f)),
            ["firefly"] = Additive("Mat_firefly", new Color(2.2f, 2.0f, 0.9f, 1f)),
        };
    }

    static Material Mat(string name, Shader sh, Color c, Color? emission = null)
    {
        string path = Folder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(sh ?? Shader.Find("Standard"));
        m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // soft additive glow using Unity's built-in round particle texture
    static Material Additive(string name, Color c)
    {
        string path = Folder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        m = new Material(sh);
        var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        m.SetTexture("_BaseMap", tex); m.mainTexture = tex;
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Surface", 1f);   // transparent
        m.SetFloat("_Blend", 2f);     // additive
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);      // visible from both sides
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 lp, Vector3 scale, Material m, bool collider = false)
    {
        var g = GameObject.CreatePrimitive(t);
        g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = lp; g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>(); r.sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static GameObject Pool(string name, Transform parent, Vector3 worldPos, float size, Material m)
    {
        var q = Prim(PrimitiveType.Quad, name, parent, Vector3.zero, Vector3.one * size, m);
        q.transform.position = worldPos;
        q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var r = q.GetComponent<Renderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        return q;
    }

    // ---------- sky
    static void Sky(Transform root, Dictionary<string, Material> m)
    {
        var sky = new GameObject("Sky").transform; sky.SetParent(root, false);
        var moon = Prim(PrimitiveType.Sphere, "Moon", sky, new Vector3(-70f, 36f, 190f), Vector3.one * 16f, m["moon"]);
        moon.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        Pool("Moon_Halo", sky, moon.transform.position, 45f, m["pool"]).transform.LookAt(Vector3.zero);

        // soft, dim stars (no bloom glare)
        m["star"].SetColor("_BaseColor", new Color(0.78f, 0.8f, 0.95f)); EditorUtility.SetDirty(m["star"]);
        // one mesh of small star quads facing the middle of the map
        var rng = new System.Random(7);
        var v = new List<Vector3>(); var tri = new List<int>();
        for (int i = 0; i < 120; i++)
        {
            float az = (float)rng.NextDouble() * Mathf.PI * 2f;
            float el = Mathf.Lerp(3f, 28f, (float)rng.NextDouble()) * Mathf.Deg2Rad;   // low in the sky, where the game camera can see
            Vector3 p = new Vector3(Mathf.Cos(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(az) * Mathf.Cos(el)) * 220f;
            float s = Mathf.Lerp(0.35f, 0.9f, Mathf.Pow((float)rng.NextDouble(), 2f));   // mostly tiny, a few bigger
            Vector3 f = -p.normalized, r = Vector3.Cross(Vector3.up, f).normalized, u = Vector3.Cross(f, r);
            int b = v.Count;
            v.Add(p + (-r - u) * s); v.Add(p + (-r + u) * s); v.Add(p + (r + u) * s); v.Add(p + (r - u) * s);
            tri.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3, b, b + 2, b + 1, b, b + 3, b + 2 });
        }
        var mesh = new Mesh { name = "Stars_Mesh" };
        mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds();
        var stars = new GameObject("Stars"); stars.transform.SetParent(sky, false);
        stars.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = stars.AddComponent<MeshRenderer>(); mr.sharedMaterial = m["star"];
        mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
    }

    // ---------- lamp posts along the paths
    static int Lamps(Transform root, Dictionary<string, Material> m, float groundY)
    {
        var parent = new GameObject("Lamp_Posts").transform; parent.SetParent(root, false);
        var pathsRoot = AllTransforms().FirstOrDefault(t => t.name == "Farm_Paths_Rounded");
        if (pathsRoot == null) { Debug.LogWarning("[Farm Chaos] No Farm_Paths_Rounded found - build paths first."); return 0; }

        Physics.SyncTransforms();
        var pads = AllTransforms().Where(t => t.name.ToLowerInvariant().EndsWith("area") && t.GetComponent<Renderer>() != null)
                                  .Select(t => t.GetComponent<Renderer>().bounds).ToList();
        var placed = new List<Vector3>();
        int count = 0;

        foreach (Transform p in pathsRoot)
        {
            var mf = p.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            var verts = mf.sharedMesh.vertices;
            bool closed = p.name.Contains("Loop");
            int pairs = closed ? verts.Length / 2 : (verts.Length - 36) / 2;
            if (pairs < 2) continue;
            var centre = new List<Vector3>(); var left = new List<Vector3>();
            for (int i = 0; i < pairs; i++)
            {
                Vector3 a = p.TransformPoint(verts[i * 2]), b = p.TransformPoint(verts[i * 2 + 1]);
                centre.Add((a + b) / 2f); left.Add((a - b).normalized);
            }
            float halfW = Vector3.Distance(p.TransformPoint(verts[0]), p.TransformPoint(verts[1])) / 2f;

            float spacing = closed ? 7.5f : 8.5f, travelled = 0f, next = closed ? 0f : 3.5f;
            float total = 0f; for (int i = 1; i < centre.Count; i++) total += Vector3.Distance(centre[i - 1], centre[i]);
            int side = 1;
            for (int i = 1; i < centre.Count; i++)
            {
                travelled += Vector3.Distance(centre[i - 1], centre[i]);
                if (travelled < next) continue;
                if (!closed && travelled > total - 2.5f) break;
                next = travelled + spacing;

                Vector3 outward = left[i] * side;
                if (closed)
                {
                    // ring: always on the outside of the loop
                    Vector3 hq = pathsRoot.position; var bb = p.GetComponent<Renderer>().bounds; hq = bb.center;
                    Vector3 radial = centre[i] - hq; radial.y = 0;
                    if (Vector3.Dot(outward, radial) < 0) outward = -outward;
                }
                bool ok = false;
                foreach (var tryDir in new[] { outward, -outward })
                {
                    Vector3 pos = centre[i] + tryDir * (halfW + 0.9f); pos.y = groundY;
                    if (SpotFree(pos, pads, placed)) { BuildLamp(parent, pos, m, "Lamp_Post_" + (count + 1)); placed.Add(pos); count++; ok = true; break; }
                    if (closed) break;
                }
                if (ok) side = -side;
            }
        }
        return count;
    }

    static bool SpotFree(Vector3 pos, List<Bounds> pads, List<Vector3> placed)
    {
        foreach (var q in placed) if (Vector3.Distance(q, pos) < 5f) return false;
        foreach (var b in pads)
        {
            var e = b; e.Expand(new Vector3(1.2f, 100f, 1.2f));
            if (e.Contains(new Vector3(pos.x, b.center.y, pos.z))) return false;
        }
        foreach (var c in Physics.OverlapSphere(pos + Vector3.up * 1.3f, 0.7f, ~0, QueryTriggerInteraction.Ignore))
            if (c.name != "land") return false;
        return true;
    }

    static void BuildLamp(Transform parent, Vector3 pos, Dictionary<string, Material> m, string name)
    {
        var lamp = new GameObject(name).transform; lamp.SetParent(parent, false); lamp.position = pos;
        Prim(PrimitiveType.Cylinder, "Base", lamp, new Vector3(0, 0.1f, 0), new Vector3(0.5f, 0.1f, 0.5f), m["mint"]);
        Prim(PrimitiveType.Cylinder, "Pole", lamp, new Vector3(0, 1.3f, 0), new Vector3(0.13f, 1.2f, 0.13f), m["pole"], true);
        Prim(PrimitiveType.Cylinder, "Collar", lamp, new Vector3(0, 2.5f, 0), new Vector3(0.24f, 0.05f, 0.24f), m["cap"]);
        var lantern = Prim(PrimitiveType.Cube, "Lantern", lamp, new Vector3(0, 2.78f, 0), new Vector3(0.38f, 0.48f, 0.38f), m["glow"]);
        lantern.transform.localRotation = Quaternion.Euler(0, 45, 0);
        lantern.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        Prim(PrimitiveType.Cylinder, "Lantern_Roof", lamp, new Vector3(0, 3.07f, 0), new Vector3(0.62f, 0.07f, 0.62f), m["cap"]);
        Prim(PrimitiveType.Cylinder, "Lantern_Roof_Top", lamp, new Vector3(0, 3.17f, 0), new Vector3(0.36f, 0.05f, 0.36f), m["cap"]);
        Prim(PrimitiveType.Sphere, "Topper", lamp, new Vector3(0, 3.3f, 0), Vector3.one * 0.16f, m["mint"]);
        Pool("Light_Pool", lamp, pos + Vector3.up * 0.07f, 7f, m["pool"]);
        Pool("Lantern_Halo", lamp, lamp.TransformPoint(new Vector3(0, 2.78f, 0)), 1.6f, m["pool"]).transform.rotation = Quaternion.Euler(50f, 0f, 0f);
    }

    // ---------- glowing windows, porch lamps and inside lights
    static void BuildingGlow(Transform root, Dictionary<string, Material> m, float groundY, bool night)
    {
        var glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Buildings/Mat_window_glass.mat");
        if (glass != null)
        {
            Undo.RecordObject(glass, "Lighting");
            if (night)
            {
                glass.EnableKeyword("_EMISSION");
                glass.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.42f) * 1.6f);
                glass.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else { glass.SetColor("_EmissionColor", Color.black); glass.DisableKeyword("_EMISSION"); }
            EditorUtility.SetDirty(glass);
        }
        if (!night || root == null) return;

        var parent = new GameObject("Building_Lights").transform; parent.SetParent(root, false);
        var all = AllTransforms();
        foreach (var b in all.Where(t => t.name == "Zoo_HQ_Building" || t.name == "PowerStation_Building"))
        {
            bool hq = b.name.StartsWith("Zoo");
            var lightGo = new GameObject(hq ? "HQ_Inside_Light" : "PowerStation_Inside_Light");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.position = b.TransformPoint(new Vector3(0, 2.6f, 0));
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point; l.range = hq ? 9f : 7f; l.intensity = hq ? 3f : 2.2f;
            l.color = hq ? Warm : new Color(0.75f, 0.9f, 1f);
            l.shadows = LightShadows.None;
            // light spilling out of the door
            Pool("Door_Glow", parent, b.TransformPoint(new Vector3(0, 0, hq ? 5.6f : 3.8f)) + Vector3.up * 0.08f, 5f, m["pool"]);
        }
        foreach (var lamp in all.Where(t => t.name == "Lamp" && t.parent != null && t.parent.name == "Zoo_HQ_Building"))
            Pool("Porch_Lamp_Glow", parent, new Vector3(lamp.position.x, groundY + 0.31f, lamp.position.z), 4.5f, m["pool"]);
    }

    // ---------- fireflies
    static void Fireflies(Transform root, Dictionary<string, Material> m)
    {
        var go = new GameObject("Fireflies"); go.transform.SetParent(root, false);
        go.transform.position = new Vector3(0, 1.8f, 0);
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // box shape lies flat over the map
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true; main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.55f), new Color(0.8f, 1f, 0.55f));
        main.maxParticles = 140;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 16f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(90f, 90f, 2.5f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.7f; noise.frequency = 0.25f; noise.scrollSpeed = 0.2f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.3f, 0.5f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = m["firefly"]; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
    }

    // ---------- small warm light that travels with the player ball
    static void PlayerLantern(bool on)
    {
        var ball = AllTransforms().FirstOrDefault(t => t.name == "Player_Sphere");
        if (ball == null) return;
        var existing = ball.Find(PlayerLightName);
        if (!on) { if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject); return; }
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
        // sits at the centre of the ball, so the ball rolling does not matter
        var go = new GameObject(PlayerLightName);
        Undo.RegisterCreatedObjectUndo(go, "Cozy Night");
        go.transform.SetParent(ball, false);
        go.transform.localPosition = Vector3.zero;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.range = 7f; l.intensity = 2.4f; l.color = Warm; l.shadows = LightShadows.None;
    }

    // ---------- soft bloom + vignette
    static void PostFX(Transform root)
    {
        string path = Folder + "/NightPostFX.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f); bloom.intensity.Override(1.3f); bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.9f, 0.8f));
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.3f); vig.smoothness.Override(0.45f); vig.color.Override(new Color32(0x0B, 0x0C, 0x1E, 255));
            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.25f); ca.saturation.Override(12f); ca.contrast.Override(8f);
            foreach (var c in profile.components) { c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }
        var go = new GameObject("Night_PostFX"); go.transform.SetParent(root, false);
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true; vol.priority = 10f; vol.sharedProfile = profile;
    }
}
#endif

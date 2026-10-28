#if UNITY_EDITOR
// Farm Chaos - builds electric pop-open gates in the open side of each animal pen.
// Menu: Tools > Farm Chaos > 3. Build Electric Gates
// Only creates/deletes the root "Farm_ElectricGates" (+ gate materials if missing). Never edits existing objects.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FarmGateBuilder
{
    const string Root = "Farm_ElectricGates";
    const string MatFolder = "Assets/Materials/ElectricGate";
    static readonly string[] Pens = { "chicken_area", "sheep_area", "cow_area", "rabbit_area" };
    const float BayTarget = 4f;     // each pair of leaves spans about this many units

    class Piece { public Transform t; public Vector3[] corners; }

    [MenuItem("Tools/Farm Chaos/3. Build Electric Gates")]
    public static void Build()
    {
        DestroyRoot();
        var all = SceneManager.GetActiveScene().GetRootGameObjects()
            .Where(r => r.name != Root)
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();

        // fence pieces = renderers whose prefab/name looks like a fence
        var fences = new List<Piece>();
        foreach (var t in all)
        {
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            bool isFence = false;
            for (var p = t; p != null; p = p.parent)
                if (p.name.ToLowerInvariant().Contains("fence")) { isFence = true; break; }
            if (!isFence) continue;
            var b = mf.sharedMesh.bounds;
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++)
                c[i] = t.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
            fences.Add(new Piece { t = t, corners = c });
        }

        var mats = Materials();
        var root = new GameObject(Root);
        Undo.RegisterCreatedObjectUndo(root, "Build Electric Gates");
        int built = 0;

        foreach (var pen in Pens)
        {
            var area = all.FirstOrDefault(t => t.name.ToLowerInvariant().Replace("_", "") == pen.Replace("_", ""));
            if (area == null) { Debug.LogWarning("[Farm Chaos] Pen not found: " + pen); continue; }
            Vector3 ac = area.position;
            Quaternion yaw = Quaternion.Euler(0, area.eulerAngles.y, 0), inv = Quaternion.Inverse(yaw);
            Vector3 L(Vector3 w) => inv * (w - ac);
            Vector3 W(Vector3 l) => ac + yaw * l;

            // pieces near this pen
            var ab = area.GetComponent<Renderer>().bounds; ab.Expand(new Vector3(8, 50, 8));
            var mine = fences.Where(f => ab.Contains(f.t.position) && NearestPen(f.t.position, all) == area).ToList();
            if (mine.Count == 0) { Debug.LogWarning("[Farm Chaos] No fence found around " + pen); continue; }

            var lc = mine.SelectMany(f => f.corners).Select(L).ToList();
            float xmin = lc.Min(v => v.x), xmax = lc.Max(v => v.x), zmin = lc.Min(v => v.z), zmax = lc.Max(v => v.z);
            float midx = (xmin + xmax) / 2, midz = (zmin + zmax) / 2;
            float baseY = mine.SelectMany(f => f.corners).Min(v => v.y);
            float fenceH = mine.SelectMany(f => f.corners).Max(v => v.y) - baseY;
            float H = Mathf.Clamp(fenceH + 0.15f, 1.2f, 2.2f);

            // sides: 0=+x 1=-x 2=+z 3=-z ; intervals along the side + line position
            var cover = new List<Vector2>[4]; var linePos = new List<float>[4];
            for (int i = 0; i < 4; i++) { cover[i] = new List<Vector2>(); linePos[i] = new List<float>(); }
            foreach (var f in mine)
            {
                var c = f.corners.Select(L).ToArray();
                float sx = c.Max(v => v.x) - c.Min(v => v.x), sz = c.Max(v => v.z) - c.Min(v => v.z);
                Vector3 m = L(f.t.position); m = c.Aggregate(Vector3.zero, (a, v) => a + v) / 8f;
                if (sx >= sz) { int s = m.z > midz ? 2 : 3; cover[s].Add(new Vector2(c.Min(v => v.x), c.Max(v => v.x))); linePos[s].Add(m.z); }
                else { int s = m.x > midx ? 0 : 1; cover[s].Add(new Vector2(c.Min(v => v.z), c.Max(v => v.z))); linePos[s].Add(m.x); }
            }

            // find the biggest uncovered stretch on any side
            float bestLen = 0; int bestSide = -1; Vector2 bestGap = default;
            for (int s = 0; s < 4; s++)
            {
                Vector2 range = s < 2 ? new Vector2(zmin, zmax) : new Vector2(xmin, xmax);
                var iv = cover[s].OrderBy(v => v.x).ToList();
                float cur = range.x;
                foreach (var v in iv)
                {
                    if (v.x - cur > bestLen) { bestLen = v.x - cur; bestSide = s; bestGap = new Vector2(cur, v.x); }
                    cur = Mathf.Max(cur, v.y);
                }
                if (range.y - cur > bestLen) { bestLen = range.y - cur; bestSide = s; bestGap = new Vector2(cur, range.y); }
            }
            if (bestSide < 0 || bestLen < 1.5f) { Debug.LogWarning($"[Farm Chaos] {pen}: no opening found in the fence (largest gap {bestLen:0.0})."); continue; }

            float line = linePos[bestSide].Count > 0 ? linePos[bestSide].Average()
                       : bestSide == 0 ? xmax - 0.1f : bestSide == 1 ? xmin + 0.1f : bestSide == 2 ? zmax - 0.1f : zmin + 0.1f;
            Vector3 la, lb, outward;
            switch (bestSide)
            {
                case 0: la = new Vector3(line, 0, bestGap.x); lb = new Vector3(line, 0, bestGap.y); outward = Vector3.right; break;
                case 1: la = new Vector3(line, 0, bestGap.x); lb = new Vector3(line, 0, bestGap.y); outward = Vector3.left; break;
                case 2: la = new Vector3(bestGap.x, 0, line); lb = new Vector3(bestGap.y, 0, line); outward = Vector3.forward; break;
                default: la = new Vector3(bestGap.x, 0, line); lb = new Vector3(bestGap.y, 0, line); outward = Vector3.back; break;
            }
            Vector3 A = W(la), B = W(lb); A.y = B.y = baseY;
            Vector3 outW = yaw * outward;
            Vector3 right = Vector3.Cross(Vector3.up, outW);
            if (Vector3.Dot(B - A, right) < 0) { var tmp = A; A = B; B = tmp; }

            string nice = char.ToUpper(pen[0]) + pen.Substring(1).Replace("_area", "");
            BuildGate(root.transform, "ElectricGate_" + nice, nice, A, B, outW, H, mats);
            Debug.Log($"[Farm Chaos] {nice} pen: electric gate across a {bestLen:0.0}-unit opening.");
            built++;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        Debug.Log($"[Farm Chaos] Built {built} electric gate(s). Press Play, then P to cut/restore the power.");
    }

    static Transform NearestPen(Vector3 p, List<Transform> all)
    {
        Transform best = null; float bd = float.MaxValue;
        foreach (var pen in Pens)
        {
            var a = all.FirstOrDefault(t => t.name.ToLowerInvariant().Replace("_", "") == pen.Replace("_", ""));
            if (a == null) continue;
            var r = a.GetComponent<Renderer>(); if (r == null) continue;
            float d = Vector3.Distance(r.bounds.ClosestPoint(new Vector3(p.x, r.bounds.center.y, p.z)), new Vector3(p.x, r.bounds.center.y, p.z));
            if (d < bd) { bd = d; best = a; }
        }
        return best;
    }

    [MenuItem("Tools/Farm Chaos/Clear Electric Gates")]
    public static void DestroyRoot()
    {
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == Root) Undo.DestroyObjectImmediate(r);
    }

    // ------------------------------------------------------------------
    class Mats { public Material metal, wireOn, wireOff, cap, sign; }

    static Mats Materials()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Materials", "ElectricGate");
        return new Mats
        {
            metal = Mat("Mat_gate_metal", new Color32(0x5A, 0x60, 0x6B, 255), null),
            cap = Mat("Mat_gate_cap", new Color32(0xF4, 0xB8, 0xC1, 255), null),        // pastel pink caps to match the farm
            sign = Mat("Mat_gate_sign", new Color32(0xFF, 0xD2, 0x3F, 255), null),
            wireOn = Mat("Mat_gate_wire_on", new Color32(0xFF, 0xE0, 0x4A, 255), new Color(1f, 0.8f, 0.15f) * 2.2f),
            wireOff = Mat("Mat_gate_wire_off", new Color32(0x7A, 0x74, 0x66, 255), null),
        };
    }

    static Material Mat(string name, Color color, Color? emission)
    {
        string path = MatFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        m = new Material(sh);
        m.SetColor("_BaseColor", color); m.SetColor("_Color", color);
        m.SetFloat("_Smoothness", 0.35f);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, bool keepCollider = false)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static void BuildGate(Transform root, string name, string area, Vector3 A, Vector3 B, Vector3 outward, float H, Mats m)
    {
        var gate = new GameObject(name);
        gate.transform.SetParent(root, false);
        gate.transform.position = A;
        gate.transform.rotation = Quaternion.LookRotation(outward, Vector3.up); // local +x runs A->B, +z points out of the pen
        var eg = gate.AddComponent<ElectricGate>();
        eg.areaName = area; eg.wireOn = m.wireOn; eg.wireOff = m.wireOff;

        float len = Vector3.Distance(A, B);
        int bays = Mathf.Max(1, Mathf.RoundToInt(len / BayTarget));
        float bayLen = len / bays;
        const float post = 0.2f;

        for (int i = 0; i <= bays; i++)
        {
            float x = i * bayLen;
            var p = Box("Post_" + i, gate.transform, new Vector3(x, (H + 0.25f) / 2f, 0), new Vector3(post, H + 0.25f, post), m.metal, true);
            Box("Cap", p.transform.parent, new Vector3(x, H + 0.3f, 0), new Vector3(0.28f, 0.12f, 0.28f), m.cap);
        }

        int leafIndex = 0;
        for (int i = 0; i < bays; i++)
        {
            float x0 = i * bayLen + post / 2f + 0.03f, x1 = (i + 1) * bayLen - post / 2f - 0.03f;
            float leafLen = (x1 - x0) / 2f - 0.03f;
            var bay = new GameObject("Bay_" + i).transform;
            bay.SetParent(gate.transform, false);
            for (int side = 0; side < 2; side++)
            {
                float dir = side == 0 ? 1f : -1f;
                var hinge = new GameObject(side == 0 ? "Hinge_L" : "Hinge_R").transform;
                hinge.SetParent(bay, false);
                hinge.localPosition = new Vector3(side == 0 ? x0 : x1, 0, 0);
                BuildLeaf(hinge, dir, leafLen, H, m, eg, side == 0 && (bays == 1 || i % 2 == 0));
                var col = hinge.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(dir * leafLen / 2f, H / 2f, 0); col.size = new Vector3(leafLen, H, 0.12f);
                var rb = hinge.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
                eg.leaves.Add(new ElectricGate.Leaf
                {
                    hinge = hinge,
                    openAngle = (side == 0 ? -1f : 1f) * Random.Range(98f, 115f), // swing outwards
                    delay = leafIndex * 0.04f + Random.Range(0f, 0.05f)
                });
                leafIndex++;
            }
        }
    }

    static void BuildLeaf(Transform hinge, float dir, float L, float H, Mats m, ElectricGate eg, bool sign)
    {
        float r = 0.08f;
        Box("Rail_Bottom", hinge, new Vector3(dir * L / 2f, 0.18f, 0), new Vector3(L, r, r), m.metal);
        Box("Rail_Top", hinge, new Vector3(dir * L / 2f, H - r / 2f, 0), new Vector3(L, r, r), m.metal);
        Box("Stile_Hinge", hinge, new Vector3(dir * r / 2f, H / 2f, 0), new Vector3(r, H, r), m.metal);
        Box("Stile_End", hinge, new Vector3(dir * (L - r / 2f), H / 2f, 0), new Vector3(r, H, r), m.metal);
        foreach (float f in new[] { 0.36f, 0.56f, 0.76f })
        {
            var w = Box("Wire", hinge, new Vector3(dir * L / 2f, H * f, 0), new Vector3(L - r, 0.04f, 0.04f), m.wireOn);
            eg.wires.Add(w.GetComponent<Renderer>());
        }
        if (sign)
        {
            Box("Sign", hinge, new Vector3(dir * L * 0.5f, H * 0.46f, 0), new Vector3(0.45f, 0.45f, 0.03f), m.sign);
            Box("Sign_Bolt", hinge, new Vector3(dir * L * 0.5f, H * 0.46f, 0), new Vector3(0.08f, 0.3f, 0.045f), m.metal)
                .transform.localRotation = Quaternion.Euler(0, 0, 25f);
        }
    }
}
#endif

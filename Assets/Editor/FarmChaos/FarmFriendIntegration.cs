#if UNITY_EDITOR
// Farm Chaos - brings your friend's systems from Game.unity into Farm_Main:
//   power box (fuse + wiring puzzle) -> inside the Power Station,  shop -> HQ counter,
//   their UI canvas (puzzle, shop, coin counter, hint text) -> on screen,
//   plus a USE button for touch, a coin wallet, the power cut at the start and the 3 AM fuse.
// Game.unity itself is NOT modified (it's opened, objects are moved out, then closed without saving).
// Menu: Tools > Farm Chaos > 9. Add Shop + Electrical Box
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FarmFriendIntegration
{
    const string GamePath = "Assets/Scenes/Game.unity";
    const string Root = "_Friend_Systems";
    static readonly string[] Bring = { "Canvas", "Electricity Box", "FusePickup" };

    [MenuItem("Tools/Farm Chaos/9. Add Shop + Electrical Box")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        var farm = SceneManager.GetActiveScene();
        if (!farm.path.EndsWith("Farm_Main.unity")) { EditorUtility.DisplayDialog("Farm Chaos", "Open Farm_Main first.", "OK"); return; }
        EditorSceneManager.SaveScene(farm);   // save first so nothing is lost

        // remove a previous run
        foreach (var r in farm.GetRootGameObjects()) if (r.name == Root) Object.DestroyImmediate(r);
        var oldUse = All(farm).FirstOrDefault(t => t.name == "Use_Button"); if (oldUse != null) Object.DestroyImmediate(oldUse.gameObject);
        foreach (var t in All(farm)) { var si = t.GetComponent<ShopInteract>(); if (si != null) Object.DestroyImmediate(si); }
        foreach (var t in All(farm)) { var pi = t.GetComponent<ProximityInteractor>(); if (pi != null) Object.DestroyImmediate(pi); }

        // ---- move the friend's objects across
        var game = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(farm);
        var container = new GameObject(Root);
        var moved = new Dictionary<string, GameObject>();
        foreach (var name in Bring)
        {
            var go = game.GetRootGameObjects().FirstOrDefault(g => g.name == name);
            if (go == null) { Debug.LogWarning("[Farm Chaos] Not found in Game.unity: " + name); continue; }
            SceneManager.MoveGameObjectToScene(go, farm);
            go.transform.SetParent(container.transform, true);
            moved[name] = go;
        }
        // drop links to things that stay behind in Game.unity
        foreach (var pb in container.GetComponentsInChildren<PowerBox>(true)) { pb.farmGate = null; pb.gateAnimator = null; }
        EditorSceneManager.CloseScene(game, true);   // closes WITHOUT saving -> Game.unity unchanged

        var all = All(farm);
        float groundY = 0f;
        var land = all.FirstOrDefault(t => t.name == "land"); if (land != null) groundY = land.GetComponent<Renderer>().bounds.max.y;

        // ---- UI canvas
        GameObject ui = moved.TryGetValue("Canvas", out var c) ? c : null;
        ShopManager shop = null; GameObject shopPanel = null, puzzlePanel = null; TMP_Text hint = null;
        if (ui != null)
        {
            ui.name = "Game_UI";
            var canvas = ui.GetComponent<Canvas>(); if (canvas != null) canvas.sortingOrder = 30;
            var cross = ui.transform.Find("Crosshair"); if (cross != null) cross.gameObject.SetActive(false);   // third-person game: no crosshair
            shop = ui.GetComponentInChildren<ShopManager>(true);
            if (shop != null) { shopPanel = shop.shopPanel != null ? shop.shopPanel : shop.gameObject; shopPanel.SetActive(false); }
            var wp = ui.GetComponentInChildren<WirePuzzle>(true); if (wp != null) puzzlePanel = wp.gameObject;
            var h = ui.transform.Find("HintText"); if (h != null) { hint = h.GetComponent<TMP_Text>(); if (hint != null) hint.text = ""; }

            // coin counter follows the round's wallet
            var coinBg = ui.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("coin background"));
            var coinText = coinBg != null ? coinBg.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "CoinText") : null;
            if (coinBg != null && coinText != null)
            {
                var hud = coinBg.gameObject.AddComponent<CoinHUD>();
                hud.coinText = coinText; hud.bounceTarget = coinBg as RectTransform;
            }
        }

        // ---- power box inside the power station
        if (moved.TryGetValue("Electricity Box", out var box))
        {
            var spot = all.FirstOrDefault(t => t.name == "ElectricalBox_Spot");
            if (spot != null)
            {
                Vector3 intoRoom = -spot.forward;
                box.transform.rotation = Quaternion.LookRotation(intoRoom, Vector3.up);
                box.transform.position = spot.position;
                // push it out so its back touches the wall
                var b = Bounds(box);
                float backGap = Vector3.Dot(spot.position - b.center, intoRoom) + Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(intoRoom.x), Mathf.Abs(intoRoom.y), Mathf.Abs(intoRoom.z))));
                box.transform.position += intoRoom * Mathf.Max(0f, backGap);
                foreach (Transform ch in spot) ch.gameObject.SetActive(false);   // hide the placeholder frame
            }
            else Debug.LogWarning("[Farm Chaos] ElectricalBox_Spot not found - build the Power Station first (step 5). Box left at its old position.");
        }

        // ---- fuse + spawn points
        GameObject fuse = moved.TryGetValue("FusePickup", out var f) ? f : null;
        var spawnRoot = new GameObject("Fuse_Spawn_Points").transform; spawnRoot.SetParent(container.transform, false);
        var spawns = new List<Transform>();
        if (fuse != null)
        {
            var fb = Bounds(fuse);
            float lift = fuse.transform.position.y - fb.min.y + 0.05f;
            Vector3[] pts = { new Vector3(-8f, 0, 26f), new Vector3(40f, 0, 4f), new Vector3(-40f, 0, -30f), new Vector3(-24f, 0, 30f), new Vector3(15f, 0, -40f), new Vector3(10f, 0, 12f) };
            for (int i = 0; i < pts.Length; i++)
            {
                var p = new GameObject("Fuse_Spawn_" + (i + 1)).transform; p.SetParent(spawnRoot, false);
                p.position = new Vector3(pts[i].x, groundY + lift, pts[i].z);
                spawns.Add(p);
            }
            // make it easy to spot at night: a soft glow + small light
            var poolMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Lighting/Mat_light_pool.mat");
            if (poolMat != null)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "Fuse_Glow";
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(fuse.transform, false);
                q.transform.position = new Vector3(fuse.transform.position.x, fb.min.y + 0.08f, fuse.transform.position.z);
                q.transform.rotation = Quaternion.Euler(90, 0, 0);
                q.transform.localScale = Vector3.one * (2.2f / Mathf.Max(0.0001f, fuse.transform.lossyScale.x));
                var r = q.GetComponent<Renderer>(); r.sharedMaterial = poolMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var lg = new GameObject("Fuse_Light"); lg.transform.SetParent(fuse.transform, false);
            lg.transform.position = fb.center + Vector3.up * 0.6f;
            var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = 3.5f; l.intensity = 2f; l.color = new Color(1f, 0.85f, 0.4f); l.shadows = LightShadows.None;
            fuse.SetActive(false);
        }

        // ---- shop opens from the HQ counter
        var counter = all.FirstOrDefault(t => t.name == "Counter" && t.parent != null && t.parent.name == "Interior" && t.parent.parent != null && t.parent.parent.name == "Zoo_HQ_Building");
        if (counter != null && shop != null) counter.gameObject.AddComponent<ShopInteract>().shopManager = shop;
        else Debug.LogWarning("[Farm Chaos] HQ counter or shop not found - build the Zoo HQ first (step 5).");

        // ---- round systems: wallet + power cut + fuse timing
        var round = farm.GetRootGameObjects().FirstOrDefault(g => g.name == "_Round");
        var roundHost = round != null ? round : container;
        foreach (var old in roundHost.GetComponents<CoinWallet>()) Object.DestroyImmediate(old);
        foreach (var old in roundHost.GetComponents<RoundPowerEvents>()) Object.DestroyImmediate(old);
        roundHost.AddComponent<CoinWallet>();
        var rpe = roundHost.AddComponent<RoundPowerEvents>();
        rpe.fuse = fuse; rpe.fuseSpawnPoints = spawns.ToArray();

        // ---- USE button + interactor on the player
        var ball = all.FirstOrDefault(t => t.name == "Player_Sphere");
        var mobile = all.FirstOrDefault(t => t.name == "Mobile_Controls");
        if (ball != null)
        {
            var pi = ball.gameObject.AddComponent<ProximityInteractor>();
            pi.hintText = hint;
            pi.modalPanels = new[] { shopPanel, puzzlePanel }.Where(x => x != null).ToArray();
            if (mobile != null)
            {
                pi.mobileControls = mobile.GetComponent<Canvas>();
                if (mobile.GetComponent<GraphicRaycaster>() == null) mobile.gameObject.AddComponent<GraphicRaycaster>();
                var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                var btnGo = new GameObject("Use_Button", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(mobile, false);
                var rt = btnGo.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(220, 110); rt.anchoredPosition = new Vector2(-230, 430);
                var img = btnGo.GetComponent<Image>(); img.sprite = rounded; img.type = Image.Type.Sliced;
                img.color = new Color32(0x8C, 0xCF, 0xA4, 235);
                var lab = new GameObject("Label", typeof(RectTransform)); lab.transform.SetParent(btnGo.transform, false);
                var t = lab.AddComponent<TextMeshProUGUI>(); t.text = "USE"; t.fontSize = 52; t.fontStyle = FontStyles.Bold;
                t.alignment = TextAlignmentOptions.Center; t.color = Color.white; t.raycastTarget = false;
                var lrt = t.rectTransform; lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
                pi.useButton = btnGo.GetComponent<Button>(); pi.useButtonLabel = t;
            }
        }
        else Debug.LogWarning("[Farm Chaos] Player_Sphere not found - add the sphere player first (step 4).");

        container.transform.SetSiblingIndex(3);
        EditorSceneManager.MarkSceneDirty(farm);
        EditorSceneManager.SaveScene(farm);
        Debug.Log("[Farm Chaos] Shop + electrical box added (scene saved). Game.unity was not changed.");
    }

    static List<Transform> All(Scene s) => s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.5f);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
#endif

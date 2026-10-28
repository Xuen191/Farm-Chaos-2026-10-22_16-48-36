#if UNITY_EDITOR
// Farm Chaos - adds the round clock, on-screen time, sunrise lighting and the end screen.
// Menu: Tools > Farm Chaos > 8. Add Round Clock
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FarmRoundBuilder
{
    const string Root = "_Round";

    [MenuItem("Tools/Farm Chaos/8. Add Round Clock")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        var scene = SceneManager.GetActiveScene();
        foreach (var r in scene.GetRootGameObjects()) if (r.name == Root) Undo.DestroyObjectImmediate(r);

        var root = new GameObject(Root); Undo.RegisterCreatedObjectUndo(root, "Round Clock");
        root.AddComponent<RoundClock>();
        root.AddComponent<DawnLighting>();

        // ---- HUD canvas
        var canvasGo = new GameObject("Round_HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(root.transform, false);
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;

        var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // clock pill, top centre
        var pill = Img("Clock_Pill", canvasGo.transform, rounded, new Color(0.08f, 0.09f, 0.2f, 0.72f), new Vector2(420, 96), new Vector2(0.5f, 1f));
        pill.GetComponent<Image>().type = Image.Type.Sliced;
        pill.anchoredPosition = new Vector2(0, -70);
        var icon = Img("Moon_Sun_Icon", pill, knob, new Color(0.85f, 0.9f, 1f), new Vector2(46, 46), new Vector2(0, 0.5f));
        icon.anchoredPosition = new Vector2(52, 0);
        var time = Text("Time_Text", pill, "11:00 PM", 54, new Vector2(320, 80), new Vector2(0, 0.5f));
        time.rectTransform.anchoredPosition = new Vector2(245, 0);
        time.alignment = TextAlignmentOptions.MidlineLeft;

        var hud = canvasGo.AddComponent<ClockHUD>();
        hud.timeText = time; hud.pill = pill; hud.icon = icon.GetComponent<Image>();

        // ---- end screen
        var end = new GameObject("End_Screen", typeof(RectTransform), typeof(CanvasGroup));
        end.transform.SetParent(canvasGo.transform, false);
        var endRt = end.GetComponent<RectTransform>(); endRt.anchorMin = Vector2.zero; endRt.anchorMax = Vector2.one; endRt.offsetMin = endRt.offsetMax = Vector2.zero;
        var dim = Img("Dim", end.transform, null, new Color(0.98f, 0.62f, 0.45f, 0.45f), Vector2.zero, new Vector2(0.5f, 0.5f));
        dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one; dim.offsetMin = dim.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().raycastTarget = true;
        var card = Img("Card", end.transform, rounded, new Color(1f, 0.95f, 0.88f, 0.97f), new Vector2(820, 520), new Vector2(0.5f, 0.5f));
        card.GetComponent<Image>().type = Image.Type.Sliced;
        var sunIcon = Img("Sun", card, knob, new Color(1f, 0.68f, 0.35f), new Vector2(120, 120), new Vector2(0.5f, 1f));
        sunIcon.anchoredPosition = new Vector2(0, -95);
        var title = Text("Title", card, "SUNRISE!", 96, new Vector2(760, 120), new Vector2(0.5f, 1f));
        title.rectTransform.anchoredPosition = new Vector2(0, -225); title.color = new Color32(0xE0, 0x6C, 0x4C, 255);
        var sub = Text("Subtitle", card, "6:00 AM - the night is over!", 40, new Vector2(760, 60), new Vector2(0.5f, 1f));
        sub.rectTransform.anchoredPosition = new Vector2(0, -305); sub.color = new Color32(0x6B, 0x4A, 0x3A, 255); sub.fontStyle = FontStyles.Normal;
        var btn = Img("Play_Again", card, rounded, new Color32(0x8C, 0xCF, 0xA4, 255), new Vector2(380, 100), new Vector2(0.5f, 0f));
        btn.GetComponent<Image>().type = Image.Type.Sliced; btn.GetComponent<Image>().raycastTarget = true;
        btn.anchoredPosition = new Vector2(0, 80);
        var button = btn.gameObject.AddComponent<Button>();
        var bt = Text("Label", btn, "PLAY AGAIN", 44, new Vector2(360, 90), new Vector2(0.5f, 0.5f)); bt.color = Color.white;

        var es = canvasGo.AddComponent<RoundEndScreen>();
        es.panel = end; es.playAgainButton = button; es.fade = end.GetComponent<CanvasGroup>();
        end.SetActive(false);

        // ---- EventSystem so the button can be tapped
        if (Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
        {
            var esGo = new GameObject("EventSystem", typeof(EventSystem));
            esGo.transform.SetParent(root.transform, false);
            var inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null) esGo.AddComponent(inputModuleType); else esGo.AddComponent<StandaloneInputModule>();
        }

        root.transform.SetSiblingIndex(2);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        Debug.Log("[Farm Chaos] Round clock added (scene saved). Press Play - hold F to fast-forward to sunrise.");
    }

    static RectTransform Img(string name, Transform parent, Sprite sprite, Color color, Vector2 size, Vector2 anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size;
        var img = go.GetComponent<Image>(); img.sprite = sprite; img.color = color; img.raycastTarget = false;
        return rt;
    }

    static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Vector2 box, Vector2 anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = box;
        return t;
    }
}
#endif

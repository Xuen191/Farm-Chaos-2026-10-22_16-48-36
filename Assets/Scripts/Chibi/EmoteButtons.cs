// Farm Chaos - two little emote buttons (HI = wave, YAY = cheer) above the jump button, for phones.
// On a keyboard it's V and C. Online, everyone sees your emote (ChibiMotor -> NetPlayer).
// Nothing to set up: added automatically wherever the on-screen joystick is.
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EmoteButtons : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single); }

    static void OnScene(Scene s, LoadSceneMode m)
    {
        var stick = FindAnyObjectByType<MobileJoystick>();
        if (stick == null || stick.jumpButton == null || stick.GetComponent<EmoteButtons>() != null) return;
        if (Object.FindAnyObjectByType<RoundClock>() != null) return;   // lobby only - no HI / YAY buttons in the actual game
        stick.gameObject.AddComponent<EmoteButtons>();
    }

    RectTransform hi, yay;
    Canvas canvas;
    float hiFlash, yayFlash;

    void Start()
    {
        var jump = GetComponent<MobileJoystick>().jumpButton;
        canvas = jump.GetComponentInParent<Canvas>();
        hi = Make("HI", jump, new Vector2(-150f, 120f));
        yay = Make("YAY", jump, new Vector2(-20f, 175f));
    }

    // this canvas has no UI click system (the joystick reads touches itself), so we check taps ourselves too
    void Update()
    {
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began) Tap(t.position);
            }
        }
        else if (Input.GetMouseButtonDown(0)) Tap(Input.mousePosition);

        hiFlash = Mathf.MoveTowards(hiFlash, 0f, Time.unscaledDeltaTime * 4f);
        yayFlash = Mathf.MoveTowards(yayFlash, 0f, Time.unscaledDeltaTime * 4f);
        if (hi != null) hi.localScale = Vector3.one * (1f - 0.15f * hiFlash);
        if (yay != null) yay.localScale = Vector3.one * (1f - 0.15f * yayFlash);
    }

    void Tap(Vector2 screen)
    {
        if (ProximityInteractor.UIOpen || !hi.gameObject.activeInHierarchy) return;
        Camera c = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (RectTransformUtility.RectangleContainsScreenPoint(hi, screen, c)) { hiFlash = 1f; Emote(false); }
        else if (RectTransformUtility.RectangleContainsScreenPoint(yay, screen, c)) { yayFlash = 1f; Emote(true); }
    }

    RectTransform Make(string label, RectTransform jump, Vector2 offset)
    {
        var go = new GameObject("Emote_" + label, typeof(RectTransform), typeof(Image));
        var r = (RectTransform)go.transform;
        r.SetParent(jump.parent, false);
        r.anchorMin = jump.anchorMin; r.anchorMax = jump.anchorMax; r.pivot = jump.pivot;
        r.sizeDelta = Vector2.one * 96f;
        r.anchoredPosition = jump.anchoredPosition + offset;
        var img = go.GetComponent<Image>();
        var jumpImg = jump.GetComponent<Image>();
        if (jumpImg != null) { img.sprite = jumpImg.sprite; img.type = jumpImg.type; }
        img.color = new Color(1f, 0.95f, 0.88f, 0.85f);
        var t = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(r, false);
        t.rectTransform.sizeDelta = r.sizeDelta; t.text = label; t.fontSize = 30; t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center; t.color = new Color32(0xE0, 0x6C, 0x8C, 255); t.raycastTarget = false;
        img.raycastTarget = false;
        return r;
    }

    static void Emote(bool cheer)
    {
        foreach (var m in FindObjectsByType<ChibiMotor>(FindObjectsSortMode.None))
            if (m.transform.parent != null && m.transform.parent.GetComponent<SpherePlayer>() != null) { m.Emote(cheer); return; }
    }
}

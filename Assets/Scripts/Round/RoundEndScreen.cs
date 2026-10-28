// Farm Chaos - "Sunrise!" screen at 6:00 AM. Stops the players and offers Play Again.
// (Coin results can be added to this panel later.)
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoundEndScreen : MonoBehaviour
{
    public GameObject panel;
    public Button playAgainButton;
    public CanvasGroup fade;

    RoundClock clock;
    float shown = -1f;

    void Start()
    {
        if (panel != null) panel.SetActive(false);
        if (playAgainButton != null) playAgainButton.onClick.AddListener(PlayAgain);
        clock = RoundClock.Instance;
        if (clock != null) clock.RoundEnded += Show;
    }

    void Show()
    {
        if (panel != null) panel.SetActive(true);
        shown = 0f;
        // freeze every sphere player
        foreach (var p in FindObjectsByType<SpherePlayer>(FindObjectsSortMode.None))
        {
            p.enabled = false;
            var rb = p.GetComponent<Rigidbody>();
            if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        }
    }

    void Update()
    {
        if (shown < 0f || fade == null) return;
        shown += Time.unscaledDeltaTime;
        fade.alpha = Mathf.Clamp01(shown / 0.8f);
    }

    public void PlayAgain()
    {
        var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(scene.name);   // the scene must be in Build Profiles' scene list
#endif
    }
}

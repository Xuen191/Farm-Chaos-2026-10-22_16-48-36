using UnityEngine;
using TMPro;
using System.Collections;

public class WolfWarningUI : MonoBehaviour
{
    [Header("UI & Audio")]
    public TextMeshProUGUI warningText;
    public AudioSource audioSource;   
    public AudioClip wolfHowlSound;   
    public Camera mainCamera;

    private bool hasWarnedThisRound = false; // 💡 紀錄這回合是否警告過

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (warningText != null) warningText.gameObject.SetActive(false);
    }

    // 💡 給生成器呼叫：狼出現時只警告一次
    public void ShowWarningOnce()
    {
        if (hasWarnedThisRound) return; 
        hasWarnedThisRound = true;
        StartCoroutine(ShowWarningRoutine());
    }

    // 💡 給狼呼叫：當狼逃回出生點，重置警告狀態
    public void ResetWarning()
    {
        hasWarnedThisRound = false;
    }

    // 💡 顯示「你趕跑了狼」的訊息
    public void ShowVictoryMessage()
    {
        StartCoroutine(ShowVictoryRoutine());
    }

    IEnumerator ShowWarningRoutine()
    {
        warningText.gameObject.SetActive(true);
        warningText.text = "WOLF IN THE FARM!";
        warningText.color = Color.red;

        if (audioSource != null && wolfHowlSound != null)
            audioSource.PlayOneShot(wolfHowlSound);

        // 相機震動
        Vector3 originalPos = mainCamera.transform.localPosition;
        float elapsed = 0f;
        while (elapsed < 0.5f)
        {
            float x = Random.Range(-0.3f, 0.3f);
            float y = Random.Range(-0.3f, 0.3f);
            mainCamera.transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }
        mainCamera.transform.localPosition = originalPos;

        yield return new WaitForSeconds(2.5f);
        warningText.gameObject.SetActive(false);
    }

    IEnumerator ShowVictoryRoutine()
    {
        warningText.gameObject.SetActive(true);
        warningText.text = "YOU CHASED THE WOLF AWAY!";
        warningText.color = Color.green; // 綠色代表勝利

        yield return new WaitForSeconds(3f);
        warningText.gameObject.SetActive(false);
    }
}
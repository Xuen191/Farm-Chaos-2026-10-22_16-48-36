using UnityEngine;
using UnityEngine.EventSystems;

public class UI3DButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public float hoverScale = 1.1f; // 懸停放大倍率
    public float clickScale = 0.9f; // 點擊縮小倍率
    private Vector3 originalScale;

    void Start()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = originalScale * hoverScale; // 滑鼠移入放大
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = originalScale; // 滑鼠移出恢復
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        transform.localScale = originalScale * clickScale; // 點擊縮小
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        transform.localScale = originalScale * hoverScale; // 點擊放開恢復懸停大小
    }
}
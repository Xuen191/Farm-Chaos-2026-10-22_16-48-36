using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform handle;        // 拖曳你的 Handle 圖片到這裡
    public float maxRadius = 80f;       // 把手可以移動的最大半徑
    public Vector2 inputDirection;      // 搖桿輸出的方向 (X, Y)

    private Canvas canvas;

    void Start()
    {
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 pos;
        // 將滑鼠/手指的螢幕座標轉換為 UI 的局部座標
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            GetComponent<RectTransform>(), 
            eventData.position, 
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, 
            out pos);

        // 限制把手在圓形範圍內移動
        Vector2 clampedPos = Vector2.ClampMagnitude(pos, maxRadius);
        handle.anchoredPosition = clampedPos;

        // 計算搖桿輸出的方向 (正規化)
        inputDirection = clampedPos / maxRadius;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 放開時，把手歸位，輸入歸零
        handle.anchoredPosition = Vector2.zero;
        inputDirection = Vector2.zero;
    }
}
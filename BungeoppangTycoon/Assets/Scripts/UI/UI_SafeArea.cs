using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public sealed class UI_SafeArea : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    private void OnEnable()
    {
        rectTransform = GetComponent<RectTransform>();
        ApplyIfChanged(true);
    }

    private void Update()
    {
        ApplyIfChanged(false);
    }

    private void ApplyIfChanged(bool force)
    {
        Rect safeArea = Screen.safeArea;
        Vector2Int screenSize = new(Screen.width, Screen.height);
        if (!force && safeArea == lastSafeArea && screenSize == lastScreenSize)
            return;

        lastSafeArea = safeArea;
        lastScreenSize = screenSize;

        if (screenSize.x <= 0 || screenSize.y <= 0)
            return;

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        anchorMin.x /= screenSize.x;
        anchorMin.y /= screenSize.y;
        anchorMax.x /= screenSize.x;
        anchorMax.y /= screenSize.y;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LTC.DesktopAssistant
{
    /// <summary>
    /// 使用 Input System UI 事件拖曳助理，並把位置限制在 Canvas 可視範圍內。
    /// 點擊輸入框、按鈕或下拉選單時不會誤觸拖曳。
    /// </summary>
    public sealed class AssistantDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] RectTransform dragTarget;
        [SerializeField] RectTransform canvasRect;
        [SerializeField] Canvas canvas;
        [SerializeField, Range(0f, 100f)] float margin = 18f;

        bool dragging;

        void Start()
        {
            if (dragTarget == null) dragTarget = transform as RectTransform;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvasRect == null && canvas != null) canvasRect = canvas.transform as RectTransform;

            if (AssistantPreferences.TryLoadPosition(out Vector2 position))
            {
                dragTarget.anchoredPosition = position;
                ClampToCanvas();
            }
        }

        public void Configure(RectTransform target, RectTransform bounds, Canvas ownerCanvas)
        {
            dragTarget = target;
            canvasRect = bounds;
            canvas = ownerCanvas;
        }

        public void SetMargin(float value)
        {
            margin = Mathf.Max(0f, value);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = !IsInteractiveControl(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || dragTarget == null) return;
            float scale = canvas == null ? 1f : Mathf.Max(0.01f, canvas.scaleFactor);
            dragTarget.anchoredPosition += eventData.delta / scale;
            ClampToCanvas();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging || dragTarget == null) return;
            dragging = false;
            ClampToCanvas();
            AssistantPreferences.SavePosition(dragTarget.anchoredPosition);
        }

        bool IsInteractiveControl(PointerEventData eventData)
        {
            GameObject pressed = eventData.pointerPressRaycast.gameObject;
            return pressed != null && pressed.GetComponentInParent<Selectable>() != null;
        }

        void ClampToCanvas()
        {
            if (dragTarget == null || canvasRect == null) return;
            Rect bounds = canvasRect.rect;
            Rect target = dragTarget.rect;
            Vector2 pivot = dragTarget.pivot;

            float minX = bounds.xMin + margin + target.width * pivot.x;
            float maxX = bounds.xMax - margin - target.width * (1f - pivot.x);
            float minY = bounds.yMin + margin + target.height * pivot.y;
            float maxY = bounds.yMax - margin - target.height * (1f - pivot.y);

            Vector2 localPosition = dragTarget.localPosition;
            localPosition.x = minX > maxX ? 0f : Mathf.Clamp(localPosition.x, minX, maxX);
            localPosition.y = minY > maxY ? 0f : Mathf.Clamp(localPosition.y, minY, maxY);
            dragTarget.localPosition = localPosition;
        }
    }
}

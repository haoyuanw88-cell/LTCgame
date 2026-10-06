using UnityEngine;
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class LTCGopherBoardFitter : MonoBehaviour {
    public Rect contentBounds = new Rect(-1320f,-840f,2520f,1120f);
    public Vector2 padding = new Vector2(30f,105f);
    void OnEnable() { Fit(); }
    void LateUpdate() { Fit(); }
    void Fit() {
        var rect = (RectTransform)transform; var parent = transform.parent as RectTransform;
        if (!parent || parent.rect.width<=0 || parent.rect.height<=0) return;
        float scale = Mathf.Min(Mathf.Max(1,parent.rect.width-2*padding.x)/contentBounds.width, Mathf.Max(1,parent.rect.height-2*padding.y)/contentBounds.height);
        rect.localScale = Vector3.one * scale; rect.anchoredPosition = -contentBounds.center * scale;
    }
}
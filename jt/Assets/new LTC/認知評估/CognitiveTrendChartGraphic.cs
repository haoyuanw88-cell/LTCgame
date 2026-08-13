using System;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class CognitiveTrendChartGraphic : MaskableGraphic
{
    [SerializeField] private Color gridColor = new Color(0.35f, 0.43f, 0.37f, 0.42f);
    [SerializeField] private Color lineColor = new Color(0.29f, 0.63f, 0.49f, 1f);
    [SerializeField] private float gridThickness = 1.5f;
    [SerializeField] private float lineThickness = 4f;
    [SerializeField] private float[] values = new float[30];

public void SetValues(float[] newValues, Color newLineColor)
    {
        int valueCount = newValues == null ? 30 : Mathf.Max(2, newValues.Length);
        values = new float[valueCount];
        for (int index = 0; index < values.Length; index++) values[index] = float.NaN;
        if (newValues != null)
            Array.Copy(newValues, values, Mathf.Min(values.Length, newValues.Length));
        lineColor = newLineColor;
        SetVerticesDirty();
    }

protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float left = r.xMin + 8f;
        float right = r.xMax - 8f;
        float bottom = r.yMin + 8f;
        float top = r.yMax - 8f;

        for (int i = 0; i <= 4; i++)
        {
            float y = Mathf.Lerp(bottom, top, i / 4f);
            AddLine(vh, new Vector2(left, y), new Vector2(right, y), gridThickness, gridColor);
        }
        for (int i = 0; i <= 6; i++)
        {
            float x = Mathf.Lerp(left, right, i / 6f);
            AddLine(vh, new Vector2(x, bottom), new Vector2(x, top), gridThickness, gridColor);
        }

        AddLine(vh, new Vector2(left, bottom), new Vector2(right, bottom), 3f,
            new Color(lineColor.r, lineColor.g, lineColor.b, 0.75f));
        if (values == null || values.Length < 2) return;

        bool hasPrevious = false;
        Vector2 previousPoint = Vector2.zero;
        for (int i = 0; i < values.Length; i++)
        {
            if (float.IsNaN(values[i])) continue;
            Vector2 point = new Vector2(
                Mathf.Lerp(left, right, i / (float)(values.Length - 1)),
                Mathf.Lerp(bottom, top, Mathf.Clamp01(values[i] / 100f)));
            if (hasPrevious) AddLine(vh, previousPoint, point, lineThickness, lineColor);
            AddPoint(vh, point, lineThickness * 1.25f, lineColor);
            previousPoint = point;
            hasPrevious = true;
        }
    }

    private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color color)
    {
        Vector2 direction = (b - a).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * thickness * 0.5f;
        int start = vh.currentVertCount;
        vh.AddVert(a - normal, color, Vector2.zero);
        vh.AddVert(a + normal, color, Vector2.zero);
        vh.AddVert(b + normal, color, Vector2.zero);
        vh.AddVert(b - normal, color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }


private static void AddPoint(VertexHelper vh, Vector2 center, float radius, Color color)
    {
        const int segments = 12;
        int start = vh.currentVertCount;
        vh.AddVert(center, color, Vector2.zero);
        for (int index = 0; index <= segments; index++)
        {
            float angle = index * Mathf.PI * 2f / segments;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            vh.AddVert(center + offset, color, Vector2.zero);
        }
        for (int index = 0; index < segments; index++)
            vh.AddTriangle(start, start + index + 1, start + index + 2);
    }
}

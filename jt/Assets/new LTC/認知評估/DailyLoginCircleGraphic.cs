using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class DailyLoginCircleGraphic : MaskableGraphic
{
    [SerializeField, Range(24, 96)] private int segments = 64;

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect rect = GetPixelAdjustedRect();
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        Vector2 center = rect.center;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = center;
        vertexHelper.AddVert(vertex);

        for (int index = 0; index <= segments; index++)
        {
            float angle = index * Mathf.PI * 2f / segments;
            vertex.position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            vertexHelper.AddVert(vertex);
        }

        for (int index = 1; index <= segments; index++)
            vertexHelper.AddTriangle(0, index, index + 1);
    }
}

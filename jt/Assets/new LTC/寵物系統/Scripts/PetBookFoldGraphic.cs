using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Clips and reflects the actual page texture around a cartoon corner crease.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PetBookFoldGraphic : MaskableGraphic
{
    readonly List<Vector2> points = new List<Vector2>();
    readonly List<Vector2> coordinates = new List<Vector2>();
    Texture pageTexture;
    public bool OutlineOnly;
    public override Texture mainTexture => pageTexture != null ? pageTexture : s_WhiteTexture;

    public void SetPage(Texture texture, List<Vector2> polygon, List<Vector2> uv, Color tint)
    {
        pageTexture = texture;
        points.Clear(); points.AddRange(polygon);
        coordinates.Clear(); coordinates.AddRange(uv);
        color = tint;
        SetVerticesDirty(); SetMaterialDirty();
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect rect = rectTransform.rect;
        if (OutlineOnly)
        {
            for(int i=0;i<points.Count;i++)
            {
                Vector2 a=points[i],b=points[(i+1)%points.Count];
                Vector2 normal=new Vector2(-(b-a).y,(b-a).x).normalized;
                Vector2[] quad={a-normal,b-normal,b+normal,a+normal};int start=helper.currentVertCount;
                foreach(var p in quad)helper.AddVert(new Vector3(rect.xMin+p.x/1280f*rect.width,rect.yMax-p.y/720f*rect.height,0),color,Vector2.zero);
                helper.AddTriangle(start,start+1,start+2);helper.AddTriangle(start,start+2,start+3);
            }
            return;
        }
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 point = points[i];
            helper.AddVert(new Vector3(rect.xMin + point.x / 1280f * rect.width,
                rect.yMax - point.y / 720f * rect.height, 0), color, coordinates[i]);
        }
        for (int i = 1; i < points.Count - 1; i++) helper.AddTriangle(0, i, i + 1);
    }
}

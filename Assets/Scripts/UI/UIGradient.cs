using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// UI 글자(Text)에 위→아래 그라데이션을 입힌다. 글자 하나하나마다 위는 노랑, 아래는 주황/빨강.
// 테두리(Outline)는 이 컴포넌트보다 "아래"에 붙여야 테두리까지 그라데이션이 번지지 않는다.
[RequireComponent(typeof(Graphic))]
public class UIGradient : BaseMeshEffect
{
    [SerializeField] private Color top = new Color(1f, 0.93f, 0.25f);
    [SerializeField] private Color bottom = new Color(0.9f, 0.2f, 0.05f);

    [Tooltip("켜면 글자마다 따로 그라데이션, 끄면 글 전체에 하나의 그라데이션")]
    [SerializeField] private bool perCharacter = true;

    private readonly List<UIVertex> verts = new List<UIVertex>();

    public void SetColors(Color newTop, Color newBottom)
    {
        top = newTop;
        bottom = newBottom;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;

        verts.Clear();
        vh.GetUIVertexStream(verts);

        if (perCharacter)
        {
            // 글자 하나 = 삼각형 2개 = 정점 6개
            for (int i = 0; i + 5 < verts.Count; i += 6)
                Paint(i, 6);
        }
        else
        {
            Paint(0, verts.Count);
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }

    private void Paint(int start, int count)
    {
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = start; i < start + count; i++)
        {
            float y = verts[i].position.y;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        float h = Mathf.Max(0.0001f, maxY - minY);
        for (int i = start; i < start + count; i++)
        {
            UIVertex v = verts[i];
            Color c = Color.Lerp(bottom, top, (v.position.y - minY) / h);
            c.a *= v.color.a / 255f;
            v.color = c;
            verts[i] = v;
        }
    }
}

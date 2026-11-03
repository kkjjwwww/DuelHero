using UnityEngine;
using UnityEngine.UI;

namespace DuelHero.Cards
{
    // UI/WhiteOutline uses the marker to ignore sprite RGB only on outline copies.
    public sealed class UIWhiteOutline : Outline
    {
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            int originalVertices = vh.currentIndexCount;
            base.ModifyMesh(vh);
            var vertex = UIVertex.simpleVert;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.uv0.z = i < vh.currentVertCount - originalVertices ? 1f : 0f;
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}

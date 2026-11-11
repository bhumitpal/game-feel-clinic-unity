using UnityEngine;

namespace GameFeelClinic.DynamicRadar
{

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RadarMapGraphic : UnityEngine.UI.MaskableGraphic
    {
        Vector2 origin;
        float rangeValue = 12, radius = 280;
        public void SetProjection(Vector2 newOrigin, float newRange, float newRadius)
        {
            if (origin == newOrigin && Mathf.Approximately(rangeValue, newRange) && Mathf.Approximately(radius, newRadius)) return;
            origin = newOrigin; rangeValue = newRange; radius = newRadius;
            SetVerticesDirty();
        }
        Vector2 P(Vector2 world) { return (world - origin) * radius / rangeValue; }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            Color route = new Color(0.73f, 0.82f, 0.82f, 0.36f);
            Line(vh, P(new Vector2(-2, -20)), P(new Vector2(-2, 100)), 16 * radius / rangeValue, route);
            Line(vh, P(new Vector2(-25, 8)), P(new Vector2(30, 8)), 5 * radius / rangeValue, route);
            Color plot = new Color(0.13f, 0.42f, 0.35f, 0.8f);
            Quad(vh, P(new Vector2(-16, 15)), P(new Vector2(-7, 15)), P(new Vector2(-7, 27)), P(new Vector2(-16, 27)), plot);
            Quad(vh, P(new Vector2(7, 3)), P(new Vector2(16, 3)), P(new Vector2(16, 13)), P(new Vector2(7, 13)), plot);
            Color building = new Color(0.82f, 0.72f, 0.49f, 0.7f);
            Quad(vh, P(new Vector2(-19, -1)), P(new Vector2(-10, -1)), P(new Vector2(-10, 6)), P(new Vector2(-19, 6)), building);
            for (int i = 1; i <= 3; i++)
            {
                float r = radius * i / 3;
                for (int j = 0; j < 96; j++)
                {
                    float a = j * Mathf.PI * 2 / 96, b = (j + 1) * Mathf.PI * 2 / 96;
                    Line(vh, new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r, new Vector2(Mathf.Sin(b), Mathf.Cos(b)) * r, 2.5f, new Color(0.7f, 0.94f, 0.98f, i == 3 ? 0.65f : 0.28f));
                }
            }
        }
        static void Line(UnityEngine.UI.VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
            Quad(vh, a - n, a + n, b + n, b - n, c);
        }
        static void Quad(UnityEngine.UI.VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}

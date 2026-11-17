using UnityEngine;

namespace GameFeelClinic.Floodgate
{
    [DefaultExecutionOrder(100)]
    public sealed class EdgeWarning : MonoBehaviour
    {
        public Camera view;
        public Transform aimPoint;
        public RectTransform arrow;
        public Vector2 margin = new(80f, 200f);
        public bool useRayIntersection = true;

        Vector2 lastDirection = Vector2.right;

        public Vector2 LastPosition { get; private set; }
        public bool OnScreen { get; private set; }

        void LateUpdate() => Refresh();

        public void Restart() => lastDirection = Vector2.right;

        public static Vector2 EdgePoint(Vector2 direction, Vector2 half)
        {
            float tx = half.x / Mathf.Max(Mathf.Abs(direction.x), 0.00001f);
            float ty = half.y / Mathf.Max(Mathf.Abs(direction.y), 0.00001f);
            return direction * Mathf.Min(tx, ty);
        }

        public void Refresh()
        {
            if (!view || !aimPoint || !arrow) return;
            var area = (RectTransform)arrow.parent;
            Vector2 half = Vector2.Max(Vector2.one, area.rect.size * .5f - margin);

            Vector3 p = view.WorldToViewportPoint(aimPoint.position);
            OnScreen = p.z >= view.nearClipPlane
                && p.x >= 0f && p.x <= 1f
                && p.y >= 0f && p.y <= 1f;

            Vector3 offset = aimPoint.position - view.transform.position;
            Vector2 d = new(Vector3.Dot(offset, view.transform.right),
                            Vector3.Dot(offset, view.transform.up));
            if (d.sqrMagnitude > 0.0001f)
                lastDirection = d.normalized;

            Vector2 facing = lastDirection;
            if (useRayIntersection)
            {
                LastPosition = EdgePoint(lastDirection, half);
            }
            else
            {
                Vector2 projected = new((p.x - .5f) * area.rect.width,
                                        (p.y - .5f) * area.rect.height);
                LastPosition = new(Mathf.Clamp(projected.x, -half.x, half.x),
                                   Mathf.Clamp(projected.y, -half.y, half.y));
                if (projected.sqrMagnitude > .01f)
                    facing = projected.normalized;
            }

            arrow.anchoredPosition = LastPosition;
            arrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
            arrow.gameObject.SetActive(!OnScreen);
        }
    }
}

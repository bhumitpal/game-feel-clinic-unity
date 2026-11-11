using UnityEngine;

namespace GameFeelClinic.CornerCorrection
{

    public sealed class CornerCorrectionController : MonoBehaviour
    {
        public Vector2 ColliderSize = new Vector2(1.125f, 1.5f);
        public float Pixel = .125f;
        public int MarginPixels = 4;
        public LayerMask Solids = 1 << 8;
        public bool CorrectionEnabled = true;
        public Vector2 Velocity;
        public int Corrections;
        public int CeilingHits;
        public int ChosenPixels;
        public Vector2 BeforeCorrection;
        public Vector2 AfterCorrection;
        public float PreservedUpSpeed;
        public float HighestY;
        public Vector2 Position
        {
            get => transform.position;
            set { transform.position = new Vector3(value.x, value.y, 0); Physics2D.SyncTransforms(); }
        }

        public bool IsClear(Vector2 candidate)
        {
            return Physics2D.OverlapBox(candidate, ColliderSize - Vector2.one * .001f, 0, Solids) == null;
        }

        public bool TryCornerCorrection()
        {
            if (!CorrectionEnabled || Velocity.y <= 0) return false;
            if (Velocity.x <= 0)
                for (int pixels = 1; pixels <= MarginPixels; pixels++)
                    if (TryCandidate(-pixels)) return true;
            if (Velocity.x >= 0)
                for (int pixels = 1; pixels <= MarginPixels; pixels++)
                    if (TryCandidate(pixels)) return true;
            return false;
        }

        bool TryCandidate(int pixels)
        {
            Vector2 candidate = Position + new Vector2(pixels * Pixel, Pixel);
            if (!IsClear(candidate)) return false;
            BeforeCorrection = Position;
            Position = candidate;
            AfterCorrection = Position;
            ChosenPixels = pixels;
            PreservedUpSpeed = Velocity.y;
            Corrections++;
            return true;
        }

        public void ResetJump(bool enabled)
        {
            Position = new Vector2(.34375f, .78f);
            Velocity = new Vector2(.02f, 5.8f);
            CorrectionEnabled = enabled;
            Corrections = CeilingHits = ChosenPixels = 0;
            BeforeCorrection = AfterCorrection = Position;
            PreservedUpSpeed = 0;
            HighestY = Position.y;
        }

        public void Step(float dt)
        {
            Velocity.y -= 9.6f * dt;
            MoveHorizontal(Velocity.x * dt);
            MoveVertical(Velocity.y * dt);
            HighestY = Mathf.Max(HighestY, Position.y);
        }

        void MoveHorizontal(float amount)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(amount) / (Pixel * .25f)));
            for (int i = 0; i < steps; i++)
            {
                Vector2 candidate = Position + Vector2.right * (amount / steps);
                if (!IsClear(candidate)) { Velocity.x = 0; return; }
                Position = candidate;
            }
        }

        void MoveVertical(float amount)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(amount) / (Pixel * .25f)));
            for (int i = 0; i < steps; i++)
            {
                Vector2 candidate = Position + Vector2.up * (amount / steps);
                if (IsClear(candidate)) { Position = candidate; continue; }
                if (amount > 0)
                {
                    CeilingHits++;
                    if (TryCornerCorrection()) return;
                }
                Velocity.y = 0;
                return;
            }
        }
    }
}

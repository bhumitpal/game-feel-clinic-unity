using System;
using UnityEngine;

namespace GameFeelClinic.Rainbell
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class LandingFeedback : MonoBehaviour
    {
        [SerializeField] Transform visual;
        [SerializeField, Range(0f, 1f)] float minGroundDot = 0.65f;
        [SerializeField, Min(0f)] float minImpactSpeed = 0.5f;
        [SerializeField, Min(0.1f)] float fullImpactSpeed = 8f;

        Rigidbody body;
        Vector3 incomingVelocity;
        Vector3 restScale;
        float impact;
        float elapsed;

        public event Action<Vector3, float> Landed;
        public int LandingCount { get; private set; }
        public int WallContacts { get; private set; }
        public float LastLandingSpeed { get; private set; }
        public float LastWallSpeed { get; private set; }
        public Transform Visual => visual;
        public bool CaptureMode { get; set; }
        public bool UseGroundGate { get; set; } = true;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (visual != null)
                restScale = visual.localScale;
        }

        void FixedUpdate()
        {
            if (!CaptureMode)
                CacheIncomingVelocity();
        }

        public void CacheIncomingVelocity()
        {
            incomingVelocity = body.linearVelocity;
        }

        void OnCollisionEnter(Collision collision)
        {
            float strongest = 0f;
            Vector3 position = default;
            bool hitWall = false;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                bool isFloor = Vector3.Dot(contact.normal, Vector3.up) >= minGroundDot;
                if (!isFloor)
                {
                    hitWall = true;
                    if (UseGroundGate)
                        continue;
                }

                float speed = Mathf.Max(0f, -Vector3.Dot(incomingVelocity, contact.normal));
                if (!UseGroundGate)
                    speed = incomingVelocity.magnitude;
                if (speed > strongest)
                {
                    strongest = speed;
                    position = contact.point;
                }
            }

            if (hitWall)
            {
                WallContacts++;
                LastWallSpeed = incomingVelocity.magnitude;
            }
            if (strongest < minImpactSpeed || strongest <= 0f)
                return;

            LandingCount++;
            LastLandingSpeed = strongest;
            impact = Mathf.InverseLerp(minImpactSpeed, Mathf.Max(fullImpactSpeed, minImpactSpeed + 0.01f), strongest);
            elapsed = 0f;
            TickVisual(0f);
            Landed?.Invoke(position, impact);
        }

        void Update()
        {
            if (!CaptureMode)
                TickVisual(Time.deltaTime);
        }

        public void TickVisual(float dt)
        {
            if (visual == null)
                return;

            elapsed += dt;
            float spring = Mathf.Cos(elapsed * 14f) * Mathf.Exp(-elapsed * 6.4f);
            float height = 1f - 0.59f * impact * spring;
            float width = 1f / Mathf.Sqrt(height);
            visual.localScale = Vector3.Scale(restScale, new Vector3(width, height, width));
        }

        public void ResetFeedback()
        {
            LandingCount = 0;
            WallContacts = 0;
            LastLandingSpeed = 0f;
            LastWallSpeed = 0f;
            impact = 0f;
            elapsed = 0f;
            if (visual != null)
                visual.localScale = restScale;
        }
    }
}

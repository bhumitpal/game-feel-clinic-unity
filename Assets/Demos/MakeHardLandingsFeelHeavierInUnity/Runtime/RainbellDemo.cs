using System;
using UnityEngine;

namespace GameFeelClinic.Rainbell
{
    public sealed class RainbellDemo : MonoBehaviour
    {
        public Rigidbody courier;
        public LandingFeedback feedback;
        public Transform wall;
        public Camera view;
        public LineRenderer ring;
        public Transform[] sparks;
        public Transform[] weather;
        public Transform[] rainbellPivots = Array.Empty<Transform>();
        public AudioSource contactAudio;

        public int Test { get; private set; }
        public bool CaptureMode { get; private set; }
        public float TestAge { get; private set; }
        public float ImpactAge { get; private set; }
        public float ImpactStrength { get; private set; }
        public int CueCount { get; private set; }
        public float FirstLandingAt { get; private set; }
        public float FirstWallAt { get; private set; }
        Vector3 contactPoint;
        float worldTime;
        int nextTest;
        AudioClip contactClip;
        Quaternion[] rainbellRotations;

        void Awake()
        {
            feedback.Landed += OnLanding;
            ring.enabled = false;
            contactClip = MakeContactSound();
            rainbellRotations = new Quaternion[rainbellPivots.Length];
            for (int i = 0; i < rainbellPivots.Length; i++)
                rainbellRotations[i] = rainbellPivots[i].localRotation;
        }

        void Start()
        {
            Begin(0);
            SetShot(0, 0);
        }

        void OnDestroy()
        {
            if (feedback != null)
                feedback.Landed -= OnLanding;
            if (contactClip != null)
                Destroy(contactClip);
        }

        void Update()
        {
            if (CaptureMode)
                return;

            Advance(Time.deltaTime);
            if (TestAge >= 3f)
            {
                nextTest = (nextTest + 1) % 3;
                Begin(nextTest);
            }
            SetShot(Test == 2 ? 3 : 0, TestAge);
        }

        public void SetCaptureMode(bool enabled)
        {
            CaptureMode = enabled;
            feedback.CaptureMode = enabled;
        }

        public void Begin(int test)
        {
            Test = test;
            TestAge = 0f;
            ImpactAge = 99f;
            ImpactStrength = 0f;
            CueCount = 0;
            FirstLandingAt = -1f;
            FirstWallAt = -1f;
            if (CaptureMode)
                worldTime = 0f;
            wall.gameObject.SetActive(test == 2);
            courier.useGravity = test != 2;
            courier.position = test switch
            {
                0 => new Vector3(-0.07f, 1.14f, 0f),
                1 => new Vector3(-0.07f, 3.68f, 0f),
                _ => new Vector3(-2.15f, 1.28f, 0f)
            };
            courier.rotation = Quaternion.identity;
            courier.angularVelocity = Vector3.zero;
            courier.linearVelocity = test == 2 ? new Vector3(7.7f, 0f, 0f) : Vector3.zero;
            courier.WakeUp();
            Physics.SyncTransforms();
            feedback.ResetFeedback();
            PaintEffects();
        }

        public void Advance(float dt)
        {
            TestAge += dt;
            worldTime += dt;
            ImpactAge += dt;
            if (FirstWallAt < 0f && feedback.WallContacts > 0)
                FirstWallAt = TestAge;
            PaintEffects();
        }

        void OnLanding(Vector3 point, float strength)
        {
            if (FirstLandingAt < 0f)
                FirstLandingAt = TestAge;
            CueCount++;
            contactPoint = point;
            ImpactStrength = strength;
            ImpactAge = 0f;
            if (!CaptureMode && contactAudio != null)
            {
                contactAudio.pitch = Mathf.Lerp(1.28f, .79f, strength);
                contactAudio.PlayOneShot(contactClip, Mathf.Lerp(.19f, .83f, strength));
            }
        }

        static AudioClip MakeContactSound()
        {
            const int rate = 24000;
            var samples = new float[3600];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float attack = Mathf.Min(1f, t * 660f);
                float body = Mathf.Sin(t * 2f * Mathf.PI * (122f - 165f * t)) * Mathf.Exp(-t * 22f);
                float glass = Mathf.Sin(t * 2f * Mathf.PI * 630f) * Mathf.Exp(-t * 61f);
                samples[i] = attack * (.62f * body + .22f * glass);
            }
            var clip = AudioClip.Create("Rainbell contact", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        void PaintEffects()
        {
            for (int i = 0; i < rainbellPivots.Length; i++)
                rainbellPivots[i].localRotation = rainbellRotations[i] * Quaternion.Euler(
                    4f * Mathf.Sin(worldTime * 1.28f + i), 0f, 5f * Mathf.Cos(worldTime + i * 1.9f));

            for (int i = 0; i < weather.Length; i++)
            {
                float phase = i * 1.7f;
                weather[i].localPosition = new Vector3(
                    Mathf.Sin(phase + worldTime * 0.21f) * (2f + (i % 7) * 0.55f),
                    1.4f + Mathf.Repeat(phase + worldTime * 0.22f, 5.5f),
                    -3.5f + (i % 10) * 1.15f + Mathf.Cos(phase + worldTime * 0.48f) * 0.42f);
            }

            float life = 0.43f + ImpactStrength * 0.39f;
            float t = ImpactAge / life;
            ring.enabled = t < 1f;
            if (ring.enabled)
            {
                float radius = 0.23f + t * (0.48f + 1.25f * ImpactStrength);
                ring.widthMultiplier = Mathf.Lerp(0.055f + ImpactStrength * 0.055f, 0f, t);
                for (int i = 0; i < ring.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / ring.positionCount;
                    ring.SetPosition(i, new Vector3(contactPoint.x + radius * Mathf.Cos(angle), 0.125f,
                        contactPoint.z + radius * Mathf.Sin(angle)));
                }
            }

            for (int i = 0; i < sparks.Length; i++)
            {
                bool visible = t < 1f && i < 6 + Mathf.FloorToInt(ImpactStrength * 19f);
                var spark = sparks[i];
                spark.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                float angle = i * 2.399963f;
                float spread = 0.25f + ImpactAge * (0.8f + ImpactStrength * 2.45f) * (0.70f + i % 5 * 0.13f);
                float height = 0.16f + Mathf.Sin(t * Mathf.PI) * (0.24f + ImpactStrength * 0.66f) * (0.65f + (i % 4) * 0.18f);
                spark.position = new Vector3(contactPoint.x + Mathf.Cos(angle) * spread, height, contactPoint.z + Mathf.Sin(angle) * spread);
                spark.rotation = Quaternion.Euler(i * 29f + t * 250f, i * 15f, i * 6f);
                spark.localScale = Vector3.one * Mathf.Lerp(0.07f + 0.055f * ImpactStrength, 0.004f, t);
            }
        }

        public void SetShot(int shot, float time)
        {
            Vector3 eye;
            Vector3 aim;
            float fov;
            switch (shot)
            {
                case 1:
                    eye = Vector3.Lerp(new Vector3(5.4f, 6.3f, -13.4f), new Vector3(3.3f, 4.5f, -11.8f), Mathf.SmoothStep(0f, 1f, time / 4.8f));
                    aim = new Vector3(0f, 2.0f, 2.45f);
                    fov = 52f;
                    break;
                case 2:
                    eye = new Vector3(0.68f, 1.7f, -5.5f);
                    aim = new Vector3(0f, 0.87f, 0.05f);
                    fov = 38f;
                    break;
                case 3:
                    eye = new Vector3(0.35f, 2.75f, -9.1f);
                    aim = new Vector3(-0.25f, 1.05f, 0f);
                    fov = 51f;
                    break;
                default:
                    eye = new Vector3(1.65f, 3.0f, -7.9f);
                    aim = new Vector3(0f, 1.82f, 0f);
                    fov = 38f;
                    break;
            }
            view.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(aim - eye));
            view.fieldOfView = fov;
        }
    }
}

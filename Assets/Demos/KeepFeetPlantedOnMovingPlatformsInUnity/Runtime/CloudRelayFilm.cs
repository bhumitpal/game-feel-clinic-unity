using System;
using UnityEngine;

namespace GameFeelClinic.CloudRelay
{
    [DefaultExecutionOrder(-80)]
    public sealed class CloudRelayFilm : MonoBehaviour
    {
        public const int PlantFrame = 18;
        public const int StartTurnFrame = 22;
        public const int EndTurnFrame = 126;
        public const int LiftFrame = 158;
        public const int EndFrame = 240;
        public const float Fps = 30f;
        public static readonly Vector3 PadLocal = new(.58f, .038f, -.76f);

        [Serializable]
        public sealed class Leg
        {
            public Transform hip;
            public Transform thigh;
            public Transform shin;
            public Transform knee;
            public Transform boot;
        }

        public Transform deck;
        public Transform footGoal;
        public Transform robot;
        public Transform head;
        public DeckFootPlant footPlant;
        public Transform footprint;
        public Renderer[] padLights;
        public Material padIdle;
        public Material padActive;
        public Transform[] clouds;
        public Vector3[] cloudOrigins;
        public Transform antenna;
        public Camera view;
        public Leg[] legs;
        public float LongestReach { get; private set; }
        public bool Capturing { get; set; }
        public int FrameIndex { get; private set; }
        public int PlantCount { get; private set; }
        public int LiftCount { get; private set; }
        public Vector3 SolePosition => legs[0].boot.position;
        public Vector3 PadWorld => deck.TransformPoint(PadLocal);
        public Vector3 SwingGoal { get; private set; }

        double age;

        void Awake()
        {
            Begin();
        }

        void Update()
        {
            if (Capturing)
                return;

            age += Time.deltaTime;
            int desiredFrame = (int)(age * Fps);
            if (desiredFrame >= EndFrame)
            {
                Begin();
                return;
            }
            MoveTo(desiredFrame);
            FrameCamera(1, FrameIndex);
        }

        void LateUpdate()
        {
            if (!Capturing)
                UpdateVisuals();
        }

        public void Begin()
        {
            age = 0;
            FrameIndex = 0;
            PlantCount = 0;
            LiftCount = 0;
            LongestReach = 0f;
            footPlant.Lift();
            PoseFrame(0);
            UpdateVisuals();
        }

        public void MoveTo(int frame)
        {
            while (FrameIndex < frame)
            {
                FrameIndex++;
                PoseFrame(FrameIndex);
            }
        }

        public void ApplyCapturePose()
        {
            footPlant.ApplyPlantedPose();
            UpdateVisuals();
        }

        public void SetEditorPose()
        {
            PoseFrame(0);
            UpdateVisuals();
            FrameCamera(1, 0);
        }

        void PoseFrame(int frame)
        {
            float time = frame / Fps;
            float turn = Ease(Mathf.InverseLerp(StartTurnFrame, EndTurnFrame, frame));
            deck.SetPositionAndRotation(new Vector3(Mathf.Lerp(-.83f, .91f, turn), .72f, Mathf.Lerp(.15f, .47f, turn)),
                Quaternion.Euler(0, Mathf.Lerp(-11f, 29f, turn), 0));
            float rising = Ease(Mathf.InverseLerp(LiftFrame, LiftFrame + 34, frame));
            robot.SetPositionAndRotation(new Vector3(Mathf.Lerp(-.62f, .56f, turn), 1.90f + .025f * Mathf.Sin(time * 2.4f) + rising * .055f, .17f + turn * .24f),
                Quaternion.Euler(0, Mathf.Lerp(-8f, 14f, turn), 0));
            head.localRotation = Quaternion.Euler(2f * Mathf.Sin(time * 2.4f), -10f + 18f * turn, 0);
            if (antenna != null)
                antenna.localRotation = Quaternion.Euler(0, time * 23f, 0);
            for (int i = 0; i < clouds.Length; i++)
                clouds[i].position = cloudOrigins[i] + Vector3.right * (time * (.13f + .018f * (i % 4)));

            if (frame == PlantFrame)
            {
                footGoal.position = PadWorld;
                footPlant.Plant();
                PlantCount++;
            }
            if (frame == LiftFrame)
            {
                footPlant.Lift();
                LiftCount++;
            }

            if (!footPlant.IsPlanted)
            {
                if (frame < PlantFrame)
                {
                    float landing = Ease(frame / (float)PlantFrame);
                    SwingGoal = deck.TransformPoint(PadLocal + new Vector3(.07f * (1f - landing), .33f * (1f - landing), -.24f * (1f - landing)));
                }
                else
                {
                    float swing = Mathf.InverseLerp(LiftFrame, LiftFrame + 35, frame);
                    Vector3 forward = new(-.19f * Ease(swing), .47f * Mathf.Sin(swing * Mathf.PI) + .10f * Ease(swing), -.57f * Ease(swing));
                    SwingGoal = deck.TransformPoint(PadLocal + forward);
                }
                footGoal.position = SwingGoal;
            }
        }

        void UpdateVisuals()
        {
            for (int i = 0; i < padLights.Length; i++)
                padLights[i].sharedMaterial = footPlant.IsPlanted ? padActive : padIdle;

            for (int i = 0; i < legs.Length; i++)
            {
                Vector3 end = i == 0 ? footGoal.position : SupportFoot(i);
                Solve(legs[i], end, i);
            }
        }

        Vector3 SupportFoot(int index)
        {
            Vector3 anchor = index switch
            {
                1 => new Vector3(-.62f, .035f, -.52f),
                2 => new Vector3(.65f, .035f, .61f),
                _ => new Vector3(-.61f, .035f, .66f)
            };
            return deck.TransformPoint(anchor);
        }

        void Solve(Leg leg, Vector3 sole, int index)
        {
            Vector3 hip = leg.hip.position;
            Vector3 ankle = sole + Vector3.up * .185f;
            Vector3 offset = ankle - hip;
            float reach = offset.magnitude;
            const float thighLength = .87f;
            const float shinLength = .88f;
            Vector3 direction = offset.normalized;
            float distance = Mathf.Clamp(reach, .05f, thighLength + shinLength - .001f);
            float along = (thighLength * thighLength - shinLength * shinLength + distance * distance) / (2f * distance);
            Vector3 hint = robot.TransformDirection(new Vector3(index == 0 || index == 2 ? .78f : -.78f, 0, index < 2 ? -.46f : .52f));
            Vector3 bend = Vector3.ProjectOnPlane(hint, direction).normalized;
            Vector3 knee = hip + direction * along + bend * Mathf.Sqrt(thighLength * thighLength - along * along);
            Vector3 solvedAnkle = hip + direction * distance;
            leg.knee.position = knee;
            leg.knee.rotation = robot.rotation;
            PointBetween(leg.thigh, hip, knee);
            PointBetween(leg.shin, knee, solvedAnkle);
            leg.boot.SetPositionAndRotation(solvedAnkle - Vector3.up * .185f, robot.rotation);
            if (index == 0)
                LongestReach = Mathf.Max(LongestReach, reach);
        }

        static void PointBetween(Transform piece, Vector3 first, Vector3 second)
        {
            piece.SetPositionAndRotation((first + second) * .5f, Quaternion.FromToRotation(Vector3.up, second - first));
        }

        public void FrameCamera(int style, int frame)
        {
            float phase = Mathf.Clamp01(frame / (float)EndFrame);
            Vector3 focus;
            Vector3 position;
            switch (style)
            {
                case 0:
                    focus = new Vector3(.15f, .95f, .48f);
                    position = new Vector3(8.3f - .53f * phase, 7.04f - .37f * phase, -14.2f + .6f * phase);
                    view.fieldOfView = 48f;
                    break;
                case 2:
                    focus = new Vector3(.18f, 1.06f, -.49f);
                    position = new Vector3(2.7f, 2.68f, -4.57f);
                    view.fieldOfView = 36f;
                    break;
                case 3:
                    focus = new Vector3(.34f, 1.06f, -.48f);
                    position = new Vector3(2.62f, 2.24f, -4.06f);
                    view.fieldOfView = 38f;
                    break;
                case 4:
                    focus = new Vector3(.07f, 1.72f, -.28f);
                    position = new Vector3(3.44f - .22f * phase, 3.44f, -5.18f);
                    view.fieldOfView = 39f;
                    break;
                case 5:
                    focus = new Vector3(1.16f, 2.17f, 2.45f);
                    position = new Vector3(4.02f - .23f * phase, 3.7f, -3.90f + .18f * phase);
                    view.fieldOfView = 46f;
                    break;
                default:
                    focus = new Vector3(.18f, 1.26f, -.24f);
                    position = new Vector3(3.94f - .16f * phase, 3.4f - .08f * phase, -5.91f);
                    view.fieldOfView = 40f;
                    break;
            }
            view.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position));
        }

        static float Ease(float value) => value * value * (3f - 2f * value);
    }
}

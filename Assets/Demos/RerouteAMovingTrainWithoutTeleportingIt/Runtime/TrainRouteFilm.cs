using System;
using System.Linq;
using UnityEngine;

namespace GameFeelClinic.TrainRoute
{
    public sealed class TrainRouteFilm : MonoBehaviour
    {
        public TrainRouteHandoff train;
        public Transform start;
        public Transform junction;
        public Transform initialExit;
        public Transform rerouteExit;
        public Transform trainModel;
        public Transform[] wheels = Array.Empty<Transform>();
        public Transform signalAmber;
        public Transform signalGreen;
        public Transform requestRipple;
        public Transform arrivalRipple;
        public Transform[] waterStreaks = Array.Empty<Transform>();
        public Transform[] gulls = Array.Empty<Transform>();
        public Camera view;

        public const float Speed = 2.55f;
        public const float RequestTime = 2.1f;
        public const float LoopTime = 9.0f;
        public const float HalfGauge = 0.56f;
        public const float TurnoutBlend = 1.18f;

        public float Age { get; private set; }
        public float JumpMetres { get; private set; }
        public float JunctionTime { get; private set; }
        public bool Capturing { get; set; }
        public bool HoldAtJunction { get; private set; }

        float editClock;
        float rollMetres;
        Transform lastExit;
        bool requested;
        bool reached;
        Quaternion modelRestRotation;
        Quaternion[] wheelRestRotations;
        Vector3[] wheelRestPositions;
        Vector3[] waterRest;

        void Awake()
        {
            Quaternion inverseTrainRotation = Quaternion.Inverse(train.transform.rotation);
            modelRestRotation = inverseTrainRotation * trainModel.rotation;
            wheelRestRotations = wheels.Select(wheel => inverseTrainRotation * wheel.rotation).ToArray();
            wheelRestPositions = wheels.Select(wheel => train.transform.InverseTransformPoint(wheel.position)).ToArray();
            waterRest = waterStreaks.Select(streak => streak.localPosition).ToArray();
        }

        void Start()
        {
            Begin(train.CommitAtJunction);
            FrameCamera(1, 0);
        }

        void Update()
        {
            if (Capturing)
                return;
            Advance(Mathf.Min(Time.deltaTime, 0.05f));
            FrameCamera(1, Age);
            if (Age >= LoopTime)
                Begin(train.CommitAtJunction);
        }

        public void Begin(bool hold)
        {
            Age = editClock = rollMetres = 0;
            lastExit = initialExit;
            JumpMetres = 0;
            JunctionTime = -1;
            requested = reached = false;
            HoldAtJunction = hold;
            train.Configure(junction, initialExit, rerouteExit, hold);
            train.ResetJourney(start);
            ShowSignals();
            ApplyPresentation();
        }

        public void Advance(float dt)
        {
            float next = Age + dt;
            if (!requested && next >= RequestTime - 0.0001f)
            {
                Move(Mathf.Max(0, RequestTime - Age));
                Vector3 before = train.transform.position;
                train.RequestRoute(rerouteExit);
                JumpMetres = Vector3.Distance(before, train.transform.position);
                editClock = RequestTime;
                Age = RequestTime;
                requested = true;
                if (!Capturing)
                    Debug.Log($"[Azure Junction] route edit at {Age:0.000}s, snap {JumpMetres:0.000} m, pending {train.PendingExit != null}");
            }

            Move(Mathf.Max(0, next - Age));
            Age = next;
            ShowSignals();
            ApplyPresentation();
        }

        void Move(float dt)
        {
            Transform target = train.Target;
            Vector3 previous = train.transform.position;
            float approach = Vector3.Distance(previous, junction.position);
            train.Step(Speed * dt);
            Vector3 displacement = train.transform.position - previous;
            rollMetres += displacement.magnitude;
            if (!reached && target == junction && train.Target != junction)
            {
                reached = true;
                JunctionTime = Age + approach / Speed;
                if (!Capturing)
                    Debug.Log($"[Azure Junction] reached junction at {JunctionTime:0.000}s and switched to {train.Target.name}");
            }
        }

        void ShowSignals()
        {
            bool waiting = requested && train.Target == junction;
            signalAmber.gameObject.SetActive(waiting);
            signalGreen.gameObject.SetActive(requested && !waiting);
            float ageSinceEdit = Age - editClock;
            Ripple(requestRipple, requested, ageSinceEdit, 0.7f);
            Ripple(arrivalRipple, reached, Age - JunctionTime, 0.9f);
        }

        static void Ripple(Transform ripple, bool trigger, float time, float duration)
        {
            bool visible = trigger && time >= 0 && time <= duration;
            ripple.gameObject.SetActive(visible);
            if (visible)
            {
                float radius = Mathf.Lerp(0.35f, 1.55f, time / duration);
                ripple.localScale = new Vector3(radius, 1, radius);
            }
        }

        public static Vector3 SampleTrack(Vector3 junction, Vector3 inbound, Vector3 outbound, float distance, out Vector3 tangent)
        {
            if (distance <= -TurnoutBlend)
            {
                tangent = inbound;
                return junction + inbound * distance;
            }

            if (distance >= TurnoutBlend)
            {
                tangent = outbound;
                return junction + outbound * distance;
            }

            float t = (distance + TurnoutBlend) / (2 * TurnoutBlend);
            float u = 1 - t;
            tangent = (inbound * u + outbound * t).normalized;
            return junction + TurnoutBlend * (outbound * (t * t) - inbound * (u * u));
        }

        void PoseRailcar()
        {
            Vector3 origin = junction.position;
            Vector3 inbound = (origin - start.position).normalized;
            bool onApproach = train.Target == junction;
            if (!onApproach && train.Target != null)
                lastExit = train.Target;

            Transform exit = onApproach ? train.PendingExit ?? initialExit : lastExit;
            Vector3 outbound = (exit.position - origin).normalized;
            float distance = Vector3.Dot(train.transform.position - origin, onApproach ? inbound : outbound);
            Vector3 frontBogie = SampleTrack(origin, inbound, outbound, distance + .76f, out _);
            Vector3 rearBogie = SampleTrack(origin, inbound, outbound, distance - .76f, out _);
            Quaternion carRotation = Quaternion.LookRotation(frontBogie - rearBogie, Vector3.up);
            trainModel.SetPositionAndRotation((frontBogie + rearBogie) * .5f, carRotation * modelRestRotation);
            for (int i = 0; i < wheels.Length; i++)
            {
                Vector3 rest = wheelRestPositions[i];
                Vector3 centre = SampleTrack(origin, inbound, outbound, distance + rest.z, out Vector3 tangent);
                Vector3 right = Vector3.Cross(Vector3.up, tangent);
                Quaternion wheelHeading = Quaternion.LookRotation(tangent, Vector3.up);
                Quaternion spin = Quaternion.AngleAxis(rollMetres * 330f, Vector3.forward);
                wheels[i].SetPositionAndRotation(centre + right * rest.x + Vector3.up * rest.y,
                    wheelHeading * wheelRestRotations[i] * spin);
            }
        }

        void ApplyPresentation()
        {
            PoseRailcar();
            if (waterRest != null)
                for (int i = 0; i < waterStreaks.Length; i++)
                    waterStreaks[i].localPosition = waterRest[i] + new Vector3(0.21f * Mathf.Sin(Age * 1.7f + i), 0, 0.32f * Mathf.Sin(Age * 0.6f + i * 0.77f));
            for (int i = 0; i < gulls.Length; i++)
            {
                float phase = Age * (0.32f + i * 0.045f) + i * 1.6f;
                gulls[i].position = new Vector3(9f + 3.4f * Mathf.Cos(phase), 2.15f + i * 0.28f + Mathf.Sin(phase * 3) * 0.18f, -5f + 4f * Mathf.Sin(phase));
                gulls[i].rotation = Quaternion.Euler(Mathf.Sin(phase * 4) * 9, -phase * Mathf.Rad2Deg, Mathf.Sin(phase) * 12);
            }
        }

        public void FrameCamera(int shot, float time)
        {
            Vector3 focus;
            Vector3 axis;
            float size;
            switch (shot)
            {
                case 0:
                    focus = new Vector3(1.68f, -0.2f, -0.85f);
                    axis = new Vector3(4.8f - time * 0.18f, 21, -24);
                    size = 14.95f - time * 0.06f;
                    break;
                case 2:
                    focus = new Vector3(0.1f, 0.1f, -0.15f);
                    axis = new Vector3(10.9f - time * 0.22f, 11.6f, -14);
                    size = 7.55f;
                    break;
                case 3:
                    focus = trainModel.position + new Vector3(0, 0.25f, 1.05f);
                    axis = new Vector3(7.4f, 5.1f + time * 0.16f, -7.5f);
                    size = 5.05f + time * 0.2f;
                    break;
                default:
                    focus = new Vector3(0.2f, 0.05f, -4.45f);
                    axis = new Vector3(4.1f, 17.5f, -17.5f);
                    size = 10.15f;
                    break;
            }
            view.transform.SetPositionAndRotation(focus + axis, Quaternion.LookRotation(-axis, Vector3.up));
            view.orthographicSize = size;
        }
    }
}

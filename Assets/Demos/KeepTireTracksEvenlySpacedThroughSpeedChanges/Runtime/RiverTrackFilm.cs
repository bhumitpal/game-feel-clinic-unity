using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameFeelClinic.DistanceTracks
{
    public sealed class RiverTrackFilm : MonoBehaviour
    {
        public DistanceStampSampler sampler;
        public Transform rover;
        public Transform[] wheels = Array.Empty<Transform>();
        public Transform[] rearWheels = Array.Empty<Transform>();
        public Transform[] dust = Array.Empty<Transform>();
        public Transform[] water = Array.Empty<Transform>();
        public Camera view;
        public Mesh stampMesh;
        public Material stampMaterial;

        public const float SlowSpeed = 1.25f;
        public const float FastSpeed = 7.8f;
        public const float ShiftAt = 2.4f;
        public const float HitchStart = 3.2f;
        public const float HitchEnd = 3.75f;
        public const float TeleportAt = 6f;
        public const float SidewaysJump = 8f;
        public const float SimulationEnd = 12.35f;
        public const float RepeatAfter = 8.6f;
        const float StartingZ = -11f;
        const float StampY = 0.016f;

        public readonly List<Vector3> Stamps = new();
        public readonly List<int> StampCounts = new();
        public readonly List<Vector3> Observations = new();
        public readonly List<float> ObservationTimes = new();
        readonly List<GameObject> stampObjects = new();

        public float Age { get; private set; }
        public bool Capturing { get; set; }

        Quaternion[] wheelRest;
        Vector3[] waterRest;
        float nextTick;
        float hitchFired;
        bool teleported;
        bool showReposition;
        float laneX;
        float wheelRadius;
        float leftTrack;
        float rightTrack;

        void Awake()
        {
            wheelRest = wheels.Select(wheel => wheel.localRotation).ToArray();
            waterRest = water.Select(streak => streak.localPosition).ToArray();
            wheelRadius = wheels[0].position.y - rover.position.y;
            leftTrack = rearWheels.Min(wheel => wheel.position.x - rover.position.x);
            rightTrack = rearWheels.Max(wheel => wheel.position.x - rover.position.x);
        }

        void Start()
        {
            Begin(sampler.SampleByDistance);
            Frame(1, 0);
        }

        void Update()
        {
            if (Capturing)
                return;
            Advance(Mathf.Min(Time.deltaTime, 0.05f));
            Frame(1, Age);
            if (Age >= RepeatAfter)
                Begin(sampler.SampleByDistance);
        }

        public void Begin(bool fixedSampling, bool reposition = true)
        {
            Age = 0;
            nextTick = 0.2f;
            hitchFired = 0;
            teleported = false;
            showReposition = reposition;
            laneX = 0;
            Stamps.Clear();
            StampCounts.Clear();
            Observations.Clear();
            ObservationTimes.Clear();
            foreach (GameObject mark in stampObjects)
                mark.SetActive(false);
            sampler.Configure(sampler.Spacing, sampler.ResetDistance, fixedSampling);
            PoseRover(0);
            sampler.Reset(RearContact());
            Animate();
        }

        public static float ForwardTravel(float at)
        {
            float slow = SlowSpeed * Mathf.Min(at, ShiftAt);
            float fast = FastSpeed * Mathf.Max(0f, Mathf.Min(at, TeleportAt) - ShiftAt);
            float afterReset = SlowSpeed * Mathf.Max(0f, at - TeleportAt);
            return slow + fast + afterReset;
        }

        public void Advance(float dt)
        {
            float end = Mathf.Min(Age + dt, SimulationEnd);
            while (nextTick <= end + .00005f)
            {
                Age = nextTick;
                if (showReposition && !teleported && nextTick >= TeleportAt - .00005f)
                {
                    laneX = SidewaysJump;
                    teleported = true;
                }
                PoseRover(Age);
                Observe();
                if (Age >= HitchStart - .00005f && hitchFired == 0)
                {
                    hitchFired = 1;
                    nextTick = HitchEnd;
                }
                else if (Mathf.Abs(Age - HitchEnd) <= .00005f)
                {
                    nextTick = 3.8f;
                }
                else
                {
                    nextTick = Mathf.Round((nextTick + .2f) * 100f) / 100f;
                }
            }
            Age = end;
            PoseRover(Age);
            Animate();
        }

        void PoseRover(float at)
        {
            float distance = ForwardTravel(at);
            rover.position = new Vector3(laneX, rover.position.y, StartingZ + distance);
            float spinDegrees = distance / wheelRadius * Mathf.Rad2Deg;
            for (int i = 0; i < wheels.Length; i++)
                wheels[i].localRotation = wheelRest[i] * Quaternion.AngleAxis(spinDegrees, Vector3.forward);
        }

        Vector3 RearContact()
        {
            Vector3 centre = (rearWheels[0].position + rearWheels[1].position) * .5f;
            return new Vector3(centre.x, 0, centre.z);
        }

        void Observe()
        {
            Vector3 contact = RearContact();
            int before = Stamps.Count;
            sampler.Observe(contact, Stamp);
            StampCounts.Add(Stamps.Count - before);
            Observations.Add(contact);
            ObservationTimes.Add(Age);
            if (!Capturing && (Mathf.Abs(Age - HitchEnd) < .001f || Mathf.Abs(Age - TeleportAt) < .001f))
                Debug.Log($"[Bracken Ford] t={Age:0.00}s, x={contact.x:0.00}m, z={contact.z:0.00}m, {Stamps.Count - before} new stamp pairs.");
        }

        void Stamp(Vector3 anchor)
        {
            int i = Stamps.Count * 2;
            PlacePrint(i, anchor, leftTrack);
            PlacePrint(i + 1, anchor, rightTrack);
            Stamps.Add(anchor);
        }

        void PlacePrint(int i, Vector3 anchor, float trackOffset)
        {
            if (i == stampObjects.Count)
            {
                GameObject mark = new("Tire tread impression");
                mark.transform.SetParent(transform, false);
                mark.AddComponent<MeshFilter>().sharedMesh = stampMesh;
                MeshRenderer renderer = mark.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = stampMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                stampObjects.Add(mark);
            }
            GameObject print = stampObjects[i];
            print.SetActive(true);
            print.transform.position = anchor + new Vector3(trackOffset, StampY + i * .000009f, 0);
        }

        void Animate()
        {
            float distance = ForwardTravel(Age);
            float dustScale = Age >= ShiftAt && Age < TeleportAt ? 1.0f : 0.54f;
            for (int i = 0; i < dust.Length; i++)
            {
                float t = Mathf.Repeat(Age * 2.2f + i * .236f, 1);
                float x = i % 2 == 0 ? leftTrack : rightTrack;
                dust[i].position = new Vector3(laneX + x + Mathf.Sin(i * 2.1f) * t * .65f, .08f + t * .46f, StartingZ + distance - 1.2f - t * 1.15f);
                dust[i].localScale = Vector3.one * Mathf.Sin(t * Mathf.PI) * (.16f + t * .41f) * dustScale;
            }
            for (int i = 0; i < water.Length; i++)
                water[i].localPosition = waterRest[i] + new Vector3(.1f * Mathf.Sin(i + Age * .45f), 0, Age * .06f + Mathf.Sin(i + Age) * .25f);
        }

        public void Frame(int shot, float time)
        {
            Vector3 focus;
            Vector3 axis;
            float size;
            switch (shot)
            {
                case 0:
                    focus = new Vector3(-2.6f + time * .25f, -.3f, 3.3f);
                    axis = new Vector3(-11f + time * .46f, 22, -29);
                    size = 20.2f - time * .12f;
                    break;
                case 2:
                    focus = new Vector3(.25f, 0, Mathf.Max(-.28f, rover.position.z - 10.9f));
                    axis = new Vector3(4.5f, 25.1f, -11.2f);
                    size = 11.8f;
                    break;
                case 3:
                    focus = new Vector3(4.03f, 0, 19.7f);
                    axis = new Vector3(0.05f, 25.5f, -7.5f);
                    size = 12.1f;
                    break;
                case 4:
                    focus = rover.position + new Vector3(.18f, .15f, -1.3f);
                    axis = new Vector3(5.7f, 5.4f, -8.1f);
                    size = 5.75f + time * .16f;
                    break;
                default:
                    focus = new Vector3(3.4f, 0, 4.95f);
                    axis = new Vector3(0, 24, -11);
                    size = 17.3f;
                    break;
            }
            view.transform.SetPositionAndRotation(focus + axis, Quaternion.LookRotation(-axis, Vector3.up));
            view.orthographicSize = size;
        }
    }
}

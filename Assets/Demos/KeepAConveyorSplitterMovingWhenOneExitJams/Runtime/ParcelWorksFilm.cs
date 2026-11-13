using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFeelClinic.Splitter
{
    public sealed class ParcelWorksFilm : MonoBehaviour
    {
        public SplitterRouter router;
        public Transform[] parcels = Array.Empty<Transform>();
        public Transform[] gateArms = Array.Empty<Transform>();
        public Renderer[] gateLamps = Array.Empty<Renderer>();
        public Renderer[] scannerLamps = Array.Empty<Renderer>();
        public Transform[] rollers = Array.Empty<Transform>();
        public Transform[] clouds = Array.Empty<Transform>();
        public Camera view;
        public Material blocked;
        public Material ready;
        public Material scannerIdle;
        public Material scannerOn;

        public const float FirstAttempt = 1.8f;
        public const float Reopen = 4.0f;
        public const float ParcelHeight = .775f;
        public const float LaneOffset = 2.38f;

        const float ApproachTime = 1.4f;
        const float StageSpeed = 2.8f;
        const float ShipSpeed = 3.45f;
        const float ShipAt = 12.4f;
        const float LoopTime = 8.4f;
        const float Epsilon = .00005f;
        public static readonly Vector3 SplitPoint = new(0, ParcelHeight, -.24f);

        public struct Attempt
        {
            public float time;
            public int head;
            public int acceptedLane;
            public int pendingCount;
            public int deliveredCount;
            public bool readyA;
            public bool readyB;
        }

        sealed class ParcelMotion
        {
            public int id;
            public int lane;
            public float depart;
        }

        readonly Queue<int> pending = new();
        readonly List<ParcelMotion> active = new();
        readonly HashSet<int> delivered = new();
        readonly float[] availableAt = new float[2];
        readonly bool[] blockedLane = new bool[2];
        readonly Vector3[] gateClosed = new Vector3[2];
        Quaternion[] restRoller;
        Vector3[] spinAxes;
        Vector3[] cloudRest;

        public readonly List<Attempt> Attempts = new();
        public float Age { get; private set; }
        public bool Capturing { get; set; }
        public int PendingCount => pending.Count;
        public int InFlightCount => active.Count;
        public int DeliveredCount => delivered.Count;
        public float LastAccept { get; private set; }
        public bool BothBlockedCase { get; private set; }

        float nextAttempt;
        bool reopened;
        int lastLane;



        void Awake()
        {
            for (int lane = 0; lane < gateArms.Length; lane++)
                gateClosed[lane] = gateArms[lane].localPosition;
            restRoller = Array.ConvertAll(rollers, roller => roller.localRotation);
            spinAxes = Array.ConvertAll(rollers, SpinAxis);
            cloudRest = Array.ConvertAll(clouds, cloud => cloud.localPosition);
        }

        static Vector3 SpinAxis(Transform roller)
        {
            Mesh mesh = roller.GetComponent<MeshFilter>().sharedMesh;
            Vector3 size = mesh.bounds.size;
            if (roller.name == "Moving roller")
                return Vector3.up;
            if (size.x > size.y && size.x > size.z)
                return Vector3.right;
            return size.y > size.z ? Vector3.up : Vector3.forward;
        }

        void Start()
        {
            Begin(router.AllowFallback);
            Frame(1, 0);
        }

        void Update()
        {
            if (Capturing)
                return;
            Advance(Mathf.Min(Time.deltaTime, .05f));
            Frame(1, Age);
            if (Age >= LoopTime)
                Begin(router.AllowFallback);
        }

        public void Begin(bool allowFallback, bool bothBlocked = false)
        {
            Age = 0;
            LastAccept = -20;
            lastLane = -1;
            nextAttempt = FirstAttempt;
            BothBlockedCase = bothBlocked;
            reopened = false;
            router.Configure(allowFallback);
            router.ResetRouting(0);
            pending.Clear();
            active.Clear();
            delivered.Clear();
            Attempts.Clear();
            availableAt[0] = availableAt[1] = 0;
            blockedLane[0] = true;
            blockedLane[1] = bothBlocked;


            for (int id = 0; id < parcels.Length; id++)
            {
                pending.Enqueue(id);
                parcels[id].gameObject.SetActive(true);
                parcels[id].position = SplitPoint + Vector3.back * (id * 1.22f + StageSpeed * ApproachTime);
                parcels[id].rotation = Quaternion.identity;
            }
            Present();
        }

        public void Advance(float elapsed)
        {
            float end = Age + elapsed;
            while (nextAttempt <= end + Epsilon || BothBlockedCase && !reopened && Reopen <= end + Epsilon)
            {
                if (BothBlockedCase && !reopened && Reopen <= nextAttempt && Reopen <= end + Epsilon)
                {
                    Age = Reopen;
                    blockedLane[1] = false;
                    reopened = true;
                    if (!Capturing)
                        Debug.Log($"[Sunrise Parcel Works] opened Output B at {Age:0.00}s.");
                    continue;
                }
                Age = nextAttempt;
                CollectArrivals();
                DispatchHead();
                nextAttempt += 1f;
            }

            Age = end;
            CollectArrivals();
            Present();
        }

        bool HasRoom(int lane) => !blockedLane[lane] && Age + Epsilon >= availableAt[lane];

        bool TryAccept(int lane, int parcel)
        {
            if (!HasRoom(lane))
                return false;

            availableAt[lane] = Age + .78f;
            active.Add(new ParcelMotion { id = parcel, lane = lane, depart = Age });
            return true;
        }

        void DispatchHead()
        {
            if (pending.Count == 0)
                return;

            int parcel = pending.Peek();
            bool readyA = HasRoom(0);
            bool readyB = HasRoom(1);
            bool accepted = router.TryDispatch(
                () => TryAccept(0, parcel),
                () => TryAccept(1, parcel),
                out int acceptedLane);
            if (accepted)
            {
                pending.Dequeue();
                LastAccept = Age;
                lastLane = acceptedLane;
            }

            Attempts.Add(new Attempt
            {
                time = Age, head = parcel, acceptedLane = acceptedLane, pendingCount = pending.Count,
                deliveredCount = delivered.Count, readyA = readyA, readyB = readyB
            });
            if (!Capturing && (Attempts.Count == 1 || accepted && BothBlockedCase))
                Debug.Log($"[Sunrise Parcel Works] t={Age:0.00}s parcel={parcel} A={readyA} B={readyB} accepted={acceptedLane} pending={pending.Count}.");
        }

        void CollectArrivals()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                ParcelMotion motion = active[i];
                if (ParcelPosition(motion, Age).z < ShipAt)
                    continue;
                if (!delivered.Add(motion.id))
                    throw new InvalidOperationException("A parcel was delivered more than once.");
                parcels[motion.id].gameObject.SetActive(false);
                active.RemoveAt(i);
            }
        }

        Vector3 ParcelPosition(ParcelMotion motion, float at)
        {
            float elapsed = at - motion.depart;
            float z = SplitPoint.z + elapsed * ShipSpeed;
            float x = (motion.lane == 0 ? -LaneOffset : LaneOffset) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((z + .24f) / 4.05f));
            return new Vector3(x, ParcelHeight, z);
        }

        void Present()
        {
            int waiting = 0;
            foreach (int id in pending)
            {
                int originalSlot = id;
                float approach = StageSpeed * Mathf.Max(0, ApproachTime - Age);
                float pendingTravel = Mathf.Clamp((Age - LastAccept) * StageSpeed, 0, 1.22f);
                float shift = lastLane < 0 ? 0 : (originalSlot - waiting - 1) * 1.22f + pendingTravel;
                Vector3 pose = SplitPoint + Vector3.back * (originalSlot * 1.22f + approach - shift);
                parcels[id].position = pose;
                parcels[id].rotation = Quaternion.identity;
                waiting++;
            }
            foreach (ParcelMotion motion in active)
            {
                Vector3 position = ParcelPosition(motion, Age);
                Vector3 tangent = ParcelPosition(motion, Age + .012f) - position;
                parcels[motion.id].SetPositionAndRotation(position, Quaternion.LookRotation(tangent.normalized, Vector3.up));
            }
            for (int lane = 0; lane < gateArms.Length; lane++)
            {
                float progress = blockedLane[lane] ? 0 : BothBlockedCase && lane == 1 ? Mathf.Clamp01((Age - Reopen) * 3.6f) : 1;
                gateArms[lane].localPosition = gateClosed[lane] + Vector3.up * (Mathf.SmoothStep(0, 1, progress) * 1.1f);
                gateLamps[lane].sharedMaterial = blockedLane[lane] ? blocked : ready;
            }
            if (scannerLamps.Length != 0)
            {
                bool flash = Age - LastAccept < .26f;
                foreach (Renderer lamp in scannerLamps)
                    lamp.sharedMaterial = flash ? scannerOn : scannerIdle;
            }
            for (int i = 0; i < rollers.Length; i++)
                rollers[i].localRotation = restRoller[i] * Quaternion.AngleAxis(Age * (250 + i % 4 * 15), spinAxes[i]);
            for (int i = 0; i < clouds.Length; i++)
                clouds[i].localPosition = cloudRest[i] + new Vector3(Age * .11f, Mathf.Sin(Age * .6f + i) * .12f, 0);


        }

        public bool AllParcelsAccounted()
        {
            HashSet<int> ids = new(pending);
            foreach (ParcelMotion motion in active)
                if (!ids.Add(motion.id))
                    return false;
            foreach (int id in delivered)
                if (!ids.Add(id))
                    return false;
            return ids.Count == parcels.Length && ids.Count == pending.Count + active.Count + delivered.Count;
        }

        public void Frame(int shot, float time)
        {
            Vector3 focus;
            Vector3 axis;
            float size;
            switch (shot)
            {
                case 0:
                    focus = new Vector3(.25f, .1f, 2.1f);
                    axis = new Vector3(10f - time * .36f, 12f, -16f);
                    size = 13.7f - time * .22f;
                    break;
                case 2:
                    focus = new Vector3(.45f, .4f, 2.7f);
                    axis = new Vector3(8.6f - time * .19f, 11.2f, -13.5f);
                    size = 8.4f - time * .06f;
                    break;
                case 3:
                    focus = new Vector3(.22f, .45f, 1.22f);
                    axis = new Vector3(5.9f, 6.2f, -10.5f);
                    size = 6.8f - time * .14f;
                    break;
                case 4:
                    focus = new Vector3(.1f, 1, 1.4f);
                    axis = new Vector3(2.2f, 16f, -12f);
                    size = 9.25f;
                    break;
                default:
                    focus = new Vector3(0, .36f, 1.6f);
                    axis = new Vector3(0, 17f, -9.3f);
                    size = 8.8f;
                    break;
            }
            view.transform.SetPositionAndRotation(focus + axis, Quaternion.LookRotation(-axis, Vector3.up));
            view.orthographicSize = size;
        }
    }
}

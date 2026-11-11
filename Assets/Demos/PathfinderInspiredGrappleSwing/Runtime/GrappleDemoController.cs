using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GameFeelClinic.Grapple
{

    public sealed class GrappleDemoController : MonoBehaviour
    {
        public const string Revision = "grapple-2026-11-09-r6";
        public Transform CourierRoot, CourierVisual, Anchor, CameraProxy, Tram, PredictionGhost;
        public Camera OutputCamera;
        public CinemachineBrain Brain;
        public CinemachineCamera VCam;
        public CinemachineThirdPersonFollow Follow;
        public LineRenderer Cable, VelocityArrow, OutwardArrow, TangentArrow;
        public Transform[] JointPivots, Flags;
        public Quaternion[] JointBinds, FlagBinds;
        public ParticleSystem AttachParticles, ReleaseParticles, LandingParticles;
        public Light AnchorLight;
        public ReflectionProbe EnvironmentProbe;
        public bool AutoRun = true;
        public string Mode = "swing", View = "wide";
        public float LoopSeconds = 4.5f, ReleaseTime = 1.35f;
        public Vector3 StartLocal = new Vector3(-6f, 5f, -1.5f);
        public Vector3 StartVelocity = new Vector3(5f, -7.875f, 1f);
        public Vector3 AttachmentLocal = new Vector3(0f, 1.1f, -.28f);
        public LayerMask ObstacleMask = 1 << 28;
        public float SimulationTime, RopeLength;
        public bool Attached, Grounded;
        public GrappleState State;
        public Vector3 ReleaseBefore, ReleaseAfter;

        const float StepSeconds = 1f / 120f;
        GrappleState reference;
        int tick, cameraFrame;
        float epoch;
        string initializedMode;
        bool released, landed, haveTramBase;
        Vector3 tramBase;

        void Start() { ResetPreview(); }
        void Update()
        {
            if (!AutoRun) return;
            float time = Mathf.Repeat(Time.time - epoch, LoopSeconds);
            AdvanceTo(time);
        }

        public void ResetPreview()
        {
            epoch = Time.time;
            ResetSimulation(Mode);
            AutoRun = true;
        }

        public void ResetSimulation(string mode)
        {
            if (Tram != null && !haveTramBase) { tramBase = Tram.localPosition; haveTramBase = true; }
            Mode = initializedMode = mode; tick = 0; SimulationTime = 0f;
            released = landed = Grounded = false; Attached = true;
            State = new GrappleState { Position = transform.position + StartLocal, Velocity = StartVelocity };
            reference = State;
            RopeLength = Vector3.Distance(State.Position, Anchor.position);
            if (mode == "slack") State.Velocity = (Anchor.position - State.Position).normalized * 6f;
            if (mode == "vectors" || mode == "limit")
            {
                State.Position = Anchor.position - Vector3.up * (RopeLength + (mode == "limit" ? 1.3f : 0f));
                State.Velocity = mode == "limit" ? Vector3.down * 3f : new Vector3(8f, -3f, 0f);
                Vector3 point = State.Position, velocity = State.Velocity;
                State.Taut = GrappleMotor.Constrain(ref point, ref velocity, Anchor.position, RopeLength);
                State.Position = point; State.Velocity = velocity;
            }
            ReleaseBefore = ReleaseAfter = Vector3.zero;
            foreach (var particles in new[] { AttachParticles, ReleaseParticles, LandingParticles })
                if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (AttachParticles != null) { AttachParticles.transform.position = Anchor.position; AttachParticles.Emit(20); }
            if (VCam != null) VCam.PreviousStateIsValid = false;
            ApplyVisuals(0f, StepSeconds);
        }

        public void AdvanceTo(float time)
        {
            if (time < 0f || float.IsNaN(time) || float.IsInfinity(time)) throw new ArgumentOutOfRangeException(nameof(time));
            if (initializedMode != Mode || time + 1e-5f < SimulationTime) ResetSimulation(Mode);
            int target = Mathf.FloorToInt(time / StepSeconds + 1e-4f);
            while (tick < target)
            {
                float next = (tick + 1) * StepSeconds;
                bool attach = next <= (Mode == "blocked" ? .7f : ReleaseTime);
                Integrate(ref reference, StepSeconds, attach, false);
                if (Mode != "vectors" && Mode != "limit")
                {
                    if (!attach && !released)
                    {
                        ReleaseBefore = State.Velocity;

                        Attached = false; released = true; ReleaseAfter = State.Velocity;
                        if (ReleaseParticles != null) { ReleaseParticles.transform.position = State.Position; ReleaseParticles.Emit(24); }
                    }
                    Attached = attach;
                    bool hit = Integrate(ref State, StepSeconds, attach, Mode == "bad");
                    Grounded = hit;
                    if (hit && !landed)
                    {
                        landed = true;
                        if (LandingParticles != null) { LandingParticles.transform.position = State.Position - Vector3.up * 1.05f; LandingParticles.Emit(28); }
                    }
                }
                foreach (var particles in new[] { AttachParticles, ReleaseParticles, LandingParticles })
                    if (particles != null) particles.Simulate(StepSeconds, false, false, false);
                tick++;
                SimulationTime = tick * StepSeconds;
                ApplyVisuals(SimulationTime, StepSeconds);
            }
            SimulationTime = tick * StepSeconds;
        }

        bool Integrate(ref GrappleState value, float dt, bool attached, bool bad)
        {
            Vector3 previous = value.Position;
            if (bad && attached)
            {

                value.Velocity = (Anchor.position - value.Position).normalized * 7f;
                value.Position += value.Velocity * dt; value.Taut = false;
            }
            else GrappleMotor.Step(ref value, dt, Physics.gravity, Anchor.position, RopeLength, attached);
            Vector3 delta = value.Position - previous;
            Vector3 flat = Vector3.ProjectOnPlane(value.Velocity, Vector3.up);
            Quaternion heading = flat.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(flat, Vector3.up) : Quaternion.identity;
            Vector3 body = previous - heading * AttachmentLocal + Vector3.up;
            bool grounded = false;
            if (delta.sqrMagnitude > 1e-10f && Physics.CapsuleCast(body - Vector3.up * .55f, body + Vector3.up * .55f, .45f,
                delta.normalized, out RaycastHit hit, delta.magnitude + .006f, ObstacleMask, QueryTriggerInteraction.Ignore))
            {
                value.Position = previous + delta.normalized * Mathf.Max(0f, hit.distance - .005f);
                float into = Vector3.Dot(value.Velocity, hit.normal);
                if (into < 0f) value.Velocity -= hit.normal * into;
                grounded = hit.normal.y > .7f;
                if (attached && !bad) value.Taut = GrappleMotor.Constrain(ref value.Position, ref value.Velocity, Anchor.position, RopeLength);
            }
            return grounded;
        }

        void ApplyVisuals(float time, float dt)
        {
            Vector3 flat = Vector3.ProjectOnPlane(State.Velocity, Vector3.up);
            Quaternion heading = flat.sqrMagnitude > .01f ? Quaternion.LookRotation(flat, Vector3.up) : Quaternion.Euler(0, 90, 0);
            CourierRoot.rotation = heading;
            CourierRoot.position = State.Position - heading * AttachmentLocal;
            if (JointPivots != null) for (int i = 0; i < JointPivots.Length; i++)
            {
                Transform joint = JointPivots[i]; if (joint == null) continue;
                Quaternion bind = JointBinds != null && i < JointBinds.Length ? JointBinds[i] : Quaternion.identity;
                float gait = Mathf.Sin(time * 10f), tuck = Attached ? 1f : Mathf.Clamp01(1f - (time - ReleaseTime) * 2f);
                float angle = 0f;
                if (joint.name.Contains("Shoulder_R")) angle = -105f * tuck + gait * 18f * (1f - tuck);
                else if (joint.name.Contains("Shoulder_L")) angle = -65f * tuck - gait * 22f * (1f - tuck);
                else if (joint.name.Contains("Elbow")) angle = -35f * tuck;
                else if (joint.name.Contains("Hip_L")) angle = 30f * tuck + gait * 20f * (1f - tuck);
                else if (joint.name.Contains("Hip_R")) angle = -24f * tuck - gait * 20f * (1f - tuck);
                else if (joint.name.Contains("Knee")) angle = 45f * tuck + Mathf.Max(0f, gait) * 20f * (1f - tuck);
                joint.localRotation = bind * Quaternion.Euler(angle, 0, 0);
            }
            if (Flags != null) for (int i = 0; i < Flags.Length; i++)
                if (Flags[i] != null) Flags[i].localRotation = FlagBinds[i] * Quaternion.Euler(0f, 5f * Mathf.Sin(time * 2.4f + i), 3f * Mathf.Sin(time * 4.2f + i * .7f));
            if (Tram != null) Tram.localPosition = new Vector3(-22f + Mathf.Repeat(time * 3.8f, 50f), tramBase.y, tramBase.z);
            if (AnchorLight != null) AnchorLight.intensity = Attached ? 2.1f + .2f * Mathf.Sin(time * 6f) : .7f;
            Cable.enabled = Attached;
            if (Attached)
            {
                float slack = Mathf.Max(0f, RopeLength - Vector3.Distance(State.Position, Anchor.position));
                Cable.positionCount = 33;
                for (int i = 0; i < 33; i++) { float u = i / 32f; Cable.SetPosition(i, Vector3.Lerp(State.Position, Anchor.position, u) - Vector3.up * (Mathf.Sin(u * Mathf.PI) * slack * .28f)); }
            }
            bool diagram = Mode == "vectors", limit = Mode == "limit";
            Vector3 origin = State.Position + Vector3.up * .15f;
            Arrow(VelocityArrow, origin, new Vector3(8f, -3f, 0f) * .28f, diagram && View != "vectors-after");
            Arrow(OutwardArrow, origin, Vector3.down * .84f, diagram && View != "vectors-after");
            Arrow(TangentArrow, origin, Vector3.right * 2.24f, diagram);
            if (PredictionGhost != null)
            {
                PredictionGhost.gameObject.SetActive(limit);
                if (limit) PredictionGhost.position = Anchor.position - Vector3.up * (RopeLength + 1.3f);
            }
            if (limit) Arrow(OutwardArrow, Anchor.position - Vector3.up * (RopeLength + 1.3f), Vector3.up * 1.3f, true);
            Vector3 framePoint = Mode == "bad" ? reference.Position : State.Position;
            float releaseAt = Mode == "blocked" ? .7f : ReleaseTime;
            float blend = Attached || diagram || limit ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - releaseAt) / .45f));
            Vector3 tiedFrame = framePoint + Vector3.Scale(Anchor.position - framePoint, new Vector3(.5f, .32f, .5f));
            CameraProxy.position = Vector3.Lerp(tiedFrame, framePoint + Vector3.up * .7f, blend) + Vector3.right * .4f;
            CameraProxy.rotation = Quaternion.Euler(11f, -20f, 0f);
            Follow.CameraDistance = View == "close" ? 10.5f : Mathf.Lerp(15.5f, 10.5f, blend);
            if (diagram || limit)
            {
                CameraProxy.position = State.Position + Vector3.up * .65f + Vector3.right * .65f;
                CameraProxy.rotation = Quaternion.Euler(5f, -5f, 0f);
                Follow.CameraDistance = 9.5f;
            }
            if (Mode == "blocked")
            {
                CameraProxy.position = State.Position + Vector3.up * .9f;
                CameraProxy.rotation = Quaternion.Euler(8f, 78f, 0f);
                Follow.CameraDistance = 11.5f;
            }
            float oldTime = CinemachineCore.CurrentTimeOverride, oldDelta = CinemachineCore.UniformDeltaTimeOverride;
            try { CinemachineCore.CurrentTimeOverride = time; Brain.ManualUpdate(++cameraFrame, dt); }
            finally { CinemachineCore.CurrentTimeOverride = oldTime; CinemachineCore.UniformDeltaTimeOverride = oldDelta; }
        }

        static void Arrow(LineRenderer line, Vector3 start, Vector3 vector, bool visible)
        {
            if (line == null) return; line.enabled = visible; if (!visible) return;
            Vector3 end = start + vector, back = vector.normalized * .22f, side = Vector3.Cross(vector.normalized, Vector3.forward) * .13f;
            line.positionCount = 5; line.SetPositions(new[] { start, end, end - back + side, end, end - back - side });
        }

        public Telemetry Snapshot()
        {
            return new Telemetry
            {
                runtimeRevision = Revision,
                mode = Mode,
                view = View,
                time = SimulationTime,
                position = State.Position,
                velocity = State.Velocity,
                anchor = Anchor.position,
                ropeLength = RopeLength,
                distance = Vector3.Distance(State.Position, Anchor.position),
                attached = Attached,
                taut = State.Taut,
                grounded = Grounded,
                speed = State.Velocity.magnitude,
                fov = OutputCamera.fieldOfView,
                effectiveReleaseTime = Mode == "blocked" ? .7f : ReleaseTime,
                camera = OutputCamera.transform.position,
                cameraRotation = OutputCamera.transform.rotation,
                courierViewport = OutputCamera.WorldToViewportPoint(State.Position),
                anchorViewport = OutputCamera.WorldToViewportPoint(Anchor.position),
                releaseBefore = ReleaseBefore,
                releaseAfter = ReleaseAfter,
                referencePosition = reference.Position,
                vectorStartViewport = OutputCamera.WorldToViewportPoint(State.Position + Vector3.up * .15f),
                outwardEndViewport = OutputCamera.WorldToViewportPoint(State.Position + Vector3.up * .15f - Vector3.up * .84f),
                tangentEndViewport = OutputCamera.WorldToViewportPoint(State.Position + Vector3.up * .15f + Vector3.right * 2.24f),
                predictionViewport = OutputCamera.WorldToViewportPoint(Anchor.position - Vector3.up * (RopeLength + 1.3f))
            };
        }

        [Serializable]
        public class Telemetry
        {
            public string runtimeRevision, mode, view; public float time, ropeLength, distance, speed, fov, effectiveReleaseTime; public bool attached, taut, grounded;
            public Vector3 position, velocity, anchor, camera, courierViewport, anchorViewport, releaseBefore, releaseAfter, referencePosition;
            public Quaternion cameraRotation;
            public Vector3 vectorStartViewport, outwardEndViewport, tangentEndViewport, predictionViewport;
        }
    }
}

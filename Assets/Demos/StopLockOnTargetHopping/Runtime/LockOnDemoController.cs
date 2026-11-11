using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GameFeelClinic.LockOn
{

    public sealed class LockOnDemoController : MonoBehaviour
    {
        public StableLockSelector Selector;
        public Transform PlayerRoot, PlayerVisual, CameraProxy, IceObstacle;
        public Transform[] EnemyRoots, EnemyVisuals;
        public Quaternion PlayerBindRotation = Quaternion.identity;
        public Quaternion[] EnemyBindRotations;
        public Transform[] ControlSurfaces, ExhaustMarkers, ContrailMarkers, MuzzleMarkers;
        public Transform[] EngineFlames;
        public Quaternion[] ControlSurfaceBindRotations;
        public ParticleSystem[] JetSystems;
        public ParticleSystem SnowParticles, ImpactSparks;
        public Light ImpactLight;
        public LineRenderer Reticle;
        public LineRenderer[] ContrailLines, TracerLines;
        public Camera DemoCamera;
        public CinemachineCamera ChaseCamera;
        public CinemachineBrain Brain;
        public CinemachineThirdPersonFollow FollowRig;
        public ReflectionProbe EnvironmentProbe;
        public bool AutoRun = true;
        public string PreviewMode = "stable";
        public float LoopSeconds = 18f, FlightSpeed = 6f, CameraPitch = 12f;
        public float SimulationTime;
        public int Switches, ShotsFired, Impacts;

        const float SimulationStep = 1f / 60f;
        float previousTime = -1f, startedAt, nextShotTime, lastImpactTime = -10f;
        int cameraFrame, shotCursor, lastLaunchTarget = -1;
        string previousMode;
        Shot[] shots;
        readonly List<List<Vector3>> trails = new List<List<Vector3>>();

        [Serializable]
        sealed class Shot
        {
            public bool active;
            public int target;
            public float launched;
            public Vector3 start, previous;
        }

        void Start() { if (EnvironmentProbe != null && EnvironmentProbe.mode == ReflectionProbeMode.Realtime && QualitySettings.realtimeReflectionProbes) EnvironmentProbe.RenderProbe(); ResetPreview(); }
        void Update()
        {
            if (!AutoRun) return;
            float t = Mathf.Repeat(Time.time - startedAt, LoopSeconds);
            if (t < previousTime || previousMode != PreviewMode) ResetSimulation(PreviewMode);
            AdvanceTo(t);
        }

        void EnsurePools()
        {
            if (shots == null || shots.Length != TracerLines.Length)
            {
                shots = new Shot[TracerLines.Length];
                for (int i = 0; i < shots.Length; i++) shots[i] = new Shot();
            }
            if (trails.Count != ContrailLines.Length)
            {
                trails.Clear();
                for (int i = 0; i < ContrailLines.Length; i++) trails.Add(new List<Vector3>(40));
            }
        }

        public void ResetPreview()
        {
            startedAt = Time.time;
            ResetSimulation(PreviewMode);
            AutoRun = true;
        }

        public void ResetSimulation(string mode)
        {
            EnsurePools();
            PreviewMode = previousMode = mode;
            previousTime = -1f;
            Selector.CurrentIndex = -1;
            Switches = ShotsFired = Impacts = shotCursor = 0;
            lastLaunchTarget = -1;
            nextShotTime = .4f;
            lastImpactTime = -10f;
            foreach (Shot shot in shots) shot.active = false;
            foreach (LineRenderer line in TracerLines) line.enabled = false;
            foreach (List<Vector3> trail in trails) trail.Clear();
            foreach (ParticleSystem jet in JetSystems) jet.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SnowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ImpactSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ChaseCamera.PreviousStateIsValid = false;
            ApplyAt(0f, 0f);
            foreach (ParticleSystem jet in JetSystems) jet.Simulate(.18f, false, true, false);
            SnowParticles.Simulate(.8f, false, true, false);
            for (int i = 0; i < trails.Count; i++)
                for (int p = 0; p < 30; p++) trails[i].Add(ContrailMarkers[i].position - Vector3.forward * (29 - p) * FlightSpeed * SimulationStep);
            UpdateContrailRenderers();
        }

        public void AdvanceTo(float time)
        {
            if (time < 0f) throw new ArgumentOutOfRangeException(nameof(time));
            if (time < previousTime - 1e-6f || previousMode != PreviewMode) ResetSimulation(PreviewMode);
            while (previousTime + SimulationStep < time - 1e-6f) ApplyAt(previousTime + SimulationStep, SimulationStep);
            if (time > previousTime + 1e-6f) ApplyAt(time, time - previousTime);
        }

        public static Vector3 PointAt(Vector3 origin, float degrees, float distance)
        {
            float a = degrees * Mathf.Deg2Rad;
            return origin + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * distance;
        }

        void ApplyAt(float time, float dt)
        {
            SimulationTime = time;
            Vector3 flight = transform.position + new Vector3(.45f * Mathf.Sin(time * .65f), 8f + .18f * Mathf.Sin(time * .8f), time * FlightSpeed);
            PlayerRoot.position = flight;
            PlayerRoot.rotation = Quaternion.identity;
            PlayerVisual.localRotation = Quaternion.Euler(2f * Mathf.Sin(time * 1.7f), 3f * Mathf.Sin(time * .8f), -11f * Mathf.Sin(time * .65f)) * PlayerBindRotation;
            float jitter = 1.1f * Mathf.Sin(time * 5f);
            float angleA = 16f + jitter;
            float angleB = -16f + jitter;
            if (PreviewMode != "occlusion") angleB = Mathf.Lerp(angleB, -3f, Mathf.SmoothStep(0f, 1f, (time - 8f) / 1.1f));
            Vector3 origin = Selector.AimOrigin.position;
            for (int i = 0; i < EnemyRoots.Length; i++)
            {
                bool active = !(PreviewMode == "none" && time >= 3f) && !(PreviewMode == "disabled" && i == 0 && time >= 3f);
                EnemyRoots[i].gameObject.SetActive(active);
                EnemyRoots[i].position = PointAt(origin, i == 0 ? angleA : angleB, 8f) + Vector3.up * (.11f * Mathf.Sin(time * 1.9f + i * 1.3f));
                EnemyRoots[i].rotation = Quaternion.identity;
                EnemyVisuals[i].localRotation = Quaternion.Euler(5f * Mathf.Sin(time * 2f + i), 8f * Mathf.Sin(time * 1.4f + i), 21f * Mathf.Sin(time * 2.4f + i)) * EnemyBindRotations[i];
            }
            for (int i = 0; i < ControlSurfaces.Length; i++)
                ControlSurfaces[i].localRotation = ControlSurfaceBindRotations[i] * Quaternion.Euler(11f * Mathf.Sin(time * 3.1f + i), 0f, 0f);

            IceObstacle.gameObject.SetActive(time >= 12f && time < 16f);
            IceObstacle.position = Vector3.Lerp(origin, EnemyRoots[PreviewMode == "occlusion" ? 0 : 1].position, .55f);
            IceObstacle.rotation = Quaternion.Euler(0f, 5f * Mathf.Sin(time), 0f);
            Selector.SwitchMargin = PreviewMode == "sticky" ? .2f : .025f;
            Physics.SyncTransforms();
            int old = Selector.CurrentIndex;
            Selector.Tick(PreviewMode != "stateless");
            if (old >= 0 && Selector.CurrentIndex >= 0 && old != Selector.CurrentIndex) Switches++;
            if (dt > 0f)
            {
                while (time >= nextShotTime)
                {
                    if (Selector.CurrentIndex >= 0) Launch(time, Selector.CurrentIndex);
                    nextShotTime += .36f;
                }
                UpdateShots(time);
            }

            for (int i = 0; i < JetSystems.Length; i++)
            {
                bool visible = ExhaustMarkers[i].gameObject.activeInHierarchy;
                if (JetSystems[i].gameObject.activeSelf != visible) { JetSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); JetSystems[i].gameObject.SetActive(visible); }
                if (!visible) continue;
                JetSystems[i].transform.position = ExhaustMarkers[i].position;
                JetSystems[i].transform.rotation = Quaternion.LookRotation(-ExhaustMarkers[i].forward, Vector3.up);
                if (dt > 0f) JetSystems[i].Simulate(dt, false, false, false);
                if (EngineFlames != null && i < EngineFlames.Length && EngineFlames[i] != null)
                {
                    float pulse = 1f + .07f * Mathf.Sin(time * 31f + i * 1.7f);
                    EngineFlames[i].localScale = new Vector3(1f + .035f * Mathf.Sin(time * 19f + i), 1f, pulse);
                }
            }
            SnowParticles.transform.position = flight + new Vector3(0f, 5f, 10f);
            if (dt > 0f) { SnowParticles.Simulate(dt, false, false, false); ImpactSparks.Simulate(dt, false, false, false); }
            ImpactLight.intensity = 2.5f * Mathf.Clamp01(1f - (time - lastImpactTime) / .16f);
            for (int i = 0; i < trails.Count; i++)
            {
                trails[i].Add(ContrailMarkers[i].position);
                if (trails[i].Count > 36) trails[i].RemoveAt(0);
            }
            UpdateContrailRenderers();
            CameraProxy.position = flight;
            CameraProxy.rotation = Quaternion.Euler(CameraPitch, 0f, 0f);
            UpdateCamera(time, dt > 0f ? dt : SimulationStep);
            Reticle.enabled = Selector.Current != null;
            if (Reticle.enabled) { Reticle.transform.position = Selector.Current.position; Reticle.transform.rotation = DemoCamera.transform.rotation; }
            previousTime = time;
        }

        void UpdateCamera(float time, float dt)
        {

            float oldTime = CinemachineCore.CurrentTimeOverride;
            float oldDelta = CinemachineCore.UniformDeltaTimeOverride;
            try
            {
                CinemachineCore.CurrentTimeOverride = time;
                Brain.ManualUpdate(++cameraFrame, dt);
            }
            finally
            {
                CinemachineCore.CurrentTimeOverride = oldTime;
                CinemachineCore.UniformDeltaTimeOverride = oldDelta;
            }
        }

        void UpdateContrailRenderers()
        {
            for (int i = 0; i < trails.Count; i++)
            {
                ContrailLines[i].positionCount = trails[i].Count;
                for (int p = 0; p < trails[i].Count; p++) ContrailLines[i].SetPosition(p, trails[i][p]);
                ContrailLines[i].enabled = ContrailMarkers[i].gameObject.activeInHierarchy;
            }
        }

        void Launch(float time, int targetIndex)
        {
            if (shots.Length == 0) return;
            int slot = shotCursor++ % shots.Length;
            Shot shot = shots[slot];
            shot.active = true; shot.target = targetIndex; shot.launched = time;
            shot.start = shot.previous = MuzzleMarkers[ShotsFired % MuzzleMarkers.Length].position;
            TracerLines[slot].enabled = true;
            ShotsFired++; lastLaunchTarget = targetIndex;
        }

        void UpdateShots(float time)
        {
            for (int i = 0; i < shots.Length; i++)
            {
                Shot shot = shots[i];
                if (!shot.active) continue;
                Transform target = Selector.Registry[shot.target];
                if (target == null || !target.gameObject.activeInHierarchy) { shot.active = false; TracerLines[i].enabled = false; continue; }
                float age = time - shot.launched;
                Vector3 point = Vector3.Lerp(shot.start, target.position, Mathf.Clamp01(age / .32f));
                bool blocked = Physics.Linecast(shot.previous, point, out RaycastHit hit, Selector.ObstacleMask, QueryTriggerInteraction.Ignore);
                if (blocked || age >= .32f)
                {
                    Vector3 impact = blocked ? hit.point : target.position;
                    ImpactLight.transform.position = impact;
                    lastImpactTime = time;
                    var emit = new ParticleSystem.EmitParams { position = impact, applyShapeToPosition = false };
                    ImpactSparks.Emit(emit, 14);
                    Impacts++;
                    shot.active = false; TracerLines[i].enabled = false;
                }
                else
                {
                    Vector3 direction = (point - shot.previous).normalized;
                    TracerLines[i].SetPosition(0, point - direction * .65f);
                    TracerLines[i].SetPosition(1, point);
                    shot.previous = point;
                }
            }
        }

        public void SetPortrait(bool portrait)
        {
            DemoCamera.aspect = portrait ? 9f / 16f : 16f / 9f;
            ChaseCamera.Lens.FieldOfView = 60f;
        }

    }
}

using System;
using UnityEngine;

namespace GameFeelClinic.SurfaceImpact
{
    [Serializable]
    public struct ImpactEpisodeState
    {
        public float time, phase, offset, alignmentDot;
        public bool hit, wrong, corner, normalVisible;
        public Vector3 rayOrigin, rayDirection, hitPoint, normal, markPosition, markForward;
        public Vector3 pointViewport, normalTipViewport, toolViewport, panelViewport;
    }

    public sealed class ImpactMarkDemoController : MonoBehaviour
    {
        public Camera captureCamera;
        public Transform tool, toolTip, upperArm, forearm, panel, mark, normalArrow, hitDot, pulse, fan;
        public BoxCollider panelCollider;
        public LineRenderer shotTrace;
        public Transform[] rain = new Transform[0];
        public Transform[] cornerOccluders = new Transform[0];
        public Renderer[] heroRenderers = new Renderer[0];
        public Vector3[] rainOrigins = new Vector3[0];
        public Vector3 faceLocalNormal, faceLocalRight;
        public Quaternion panelRestRotation, upperRestRotation, forearmRestRotation;
        public Vector3 cameraPosition = new Vector3(-5.2f, 3f, -3.8f);
        public Vector3 cameraTarget = new Vector3(0.35f, 1.65f, 0.7f);
        public Vector3 wideCameraPosition = new Vector3(-6.2f, 3.6f, -6.7f);
        public bool lookDevelopment, autoplay = true;
        public float duration = 56f, playbackTime, surfaceOffset = 0.003f;
        public ImpactEpisodeState State { get; private set; }
        float startClock;

        void OnEnable()
        {
            startClock = Time.time - playbackTime;
#if UNITY_EDITOR

            if (captureCamera && captureCamera.overrideSceneCullingMask != 0 && gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.SetSceneCullingMask(gameObject.scene, captureCamera.overrideSceneCullingMask);
#endif
        }
        void Update()
        {
            if (autoplay) EvaluateAt(Mathf.Repeat(Time.time - startClock, duration));
        }

        public ImpactEpisodeState EvaluateAt(float seconds)
        {
            if (!captureCamera || !toolTip || !panelCollider || !mark)
                throw new InvalidOperationException("Assign the camera, tool tip, panel collider and impact mark.");
            seconds = Mathf.Clamp(seconds, 0f, duration); playbackTime = seconds;
            bool corner = seconds >= 47f && seconds < 52f;
            foreach (var occluder in cornerOccluders) if (occluder) occluder.gameObject.SetActive(!corner);
            float phase = PhaseAt(seconds);
            panel.localRotation = panelRestRotation;

            if (seconds >= 42f && seconds < 47f)
                panel.localRotation = panelRestRotation * Quaternion.Euler(0f, 14f, 0f);
            float recoil = phase >= 0.38f ? 4f * Mathf.Exp(-(phase - 0.38f) * 14f) : 0f;
            if (upperArm) upperArm.localRotation = upperRestRotation * Quaternion.Euler(0f, Mathf.Sin(phase * 1.570796f) * 4.5f, 0f);
            if (forearm) forearm.localRotation = forearmRestRotation * Quaternion.Euler(0f, Mathf.Sin(phase * 1.570796f + 0.5f) * 6.5f - recoil, 0f);
            captureCamera.fieldOfView = 44f;
            float cameraBlend = seconds < 8f ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 8f, seconds)) : 1f;
            captureCamera.transform.position = Vector3.Lerp(wideCameraPosition, cameraPosition, cameraBlend);
            captureCamera.transform.LookAt(cameraTarget);
            if (seconds < 6f || (seconds >= 42f && seconds < 47f) || seconds >= 52f)
                ApplyHeroCamera(seconds);
            else if (seconds < 8f)
            {
                Vector3 closePosition = captureCamera.transform.position; Quaternion closeRotation = captureCamera.transform.rotation;
                ApplyHeroCamera(seconds);
                float push = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(6f, 8f, seconds));
                captureCamera.transform.SetPositionAndRotation(Vector3.Lerp(captureCamera.transform.position, closePosition, push),
                    Quaternion.Slerp(captureCamera.transform.rotation, closeRotation, push));
                captureCamera.fieldOfView = Mathf.Lerp(48f, 44f, push);
            }

            Vector3 half = panelCollider.size * 0.5f;
            Vector3 localTarget = panelCollider.center + Vector3.Scale(half, faceLocalNormal);
            if (corner)
            {
                float edge = Vector3.Dot(half, Abs(faceLocalRight));
                localTarget += faceLocalRight * (edge - 0.045f);
            }
            Vector3 target = panelCollider.transform.TransformPoint(localTarget);
            Vector3 origin = toolTip.position;
            Vector3 direction = (target - origin).normalized;
            Physics.SyncTransforms();
            RaycastHit contact;
            bool hit = Physics.Raycast(origin, direction, out contact, 100f, 1 << 30, QueryTriggerInteraction.Ignore)
                && contact.collider == panelCollider;
            bool wrong = seconds < 27.5f;
            float corrected = seconds < 27.5f ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(27.5f, 30.2f, seconds));

            float offset = seconds >= 31.5f && seconds < 38f
                ? Mathf.Lerp(0f, surfaceOffset, Mathf.Clamp01((seconds - 31.5f) / 1.8f)) : surfaceOffset;
            bool fired = phase >= 0.38f || (seconds >= 21.5f && seconds < 38f);
            mark.gameObject.SetActive(hit && fired);
            bool showNormal = hit && seconds >= 21.5f && seconds < 31.5f;
            if (normalArrow) normalArrow.gameObject.SetActive(showNormal);
            if (hitDot) hitDot.gameObject.SetActive(hit && seconds >= 8f && seconds < 31.5f);
            var state = new ImpactEpisodeState
            {
                time = seconds,
                phase = phase,
                offset = offset,
                hit = hit,
                wrong = wrong,
                corner = corner,
                normalVisible = showNormal,
                rayOrigin = origin,
                rayDirection = direction
            };
            if (hit)
            {
                Pose pose = ImpactPlacement.BuildPose(contact, offset, Vector3.up);
                Quaternion toolFacing = Quaternion.LookRotation((origin - contact.point).normalized, Vector3.up);
                mark.SetPositionAndRotation(pose.position, Quaternion.Slerp(toolFacing, pose.rotation, corrected));
                if (normalArrow) normalArrow.SetPositionAndRotation(contact.point + contact.normal * 0.025f, pose.rotation);
                if (hitDot) hitDot.position = contact.point + contact.normal * 0.012f;
                if (corner) ApplyCornerCamera(contact.point, contact.normal, panelCollider.transform.TransformDirection(faceLocalRight).normalized);
                state.hitPoint = contact.point; state.normal = contact.normal;
                state.markPosition = mark.position; state.markForward = mark.forward;
                state.alignmentDot = Vector3.Dot(mark.forward, contact.normal);
                state.pointViewport = captureCamera.WorldToViewportPoint(contact.point);
                state.normalTipViewport = captureCamera.WorldToViewportPoint(normalArrow ? normalArrow.TransformPoint(new Vector3(0, 0, 0.73f)) : contact.point + contact.normal * 0.755f);
            }
            state.toolViewport = captureCamera.WorldToViewportPoint(toolTip.position);
            state.panelViewport = captureCamera.WorldToViewportPoint(panelCollider.bounds.center);
            float flash = Mathf.Clamp01(1f - Mathf.Abs(phase - 0.38f) / 0.12f);
            if (pulse) { pulse.position = origin; pulse.localScale = Vector3.one * (0.045f + flash * 0.12f); pulse.gameObject.SetActive(flash > 0.01f); }
            if (shotTrace)
            {
                shotTrace.enabled = hit && flash > 0.01f;
                shotTrace.SetPosition(0, origin); shotTrace.SetPosition(1, hit ? contact.point : target);
            }
            if (fan) fan.localRotation = Quaternion.Euler(0f, 0f, seconds * 72f);
            for (int i = 0; i < rain.Length && i < rainOrigins.Length; i++)
                if (rain[i]) rain[i].position = rainOrigins[i] + Vector3.down * Mathf.Repeat(seconds * 2.1f + i * 0.381f, 4f);
            State = state;
            return state;
        }

        void ApplyCornerCamera(Vector3 point, Vector3 normal, Vector3 edgeDirection)
        {
            captureCamera.fieldOfView = 44f;

            captureCamera.transform.position = point + normal * 3.0f + edgeDirection * 2.2f + Vector3.up * 0.45f;
            captureCamera.transform.LookAt(point);
            float depth = Vector3.Distance(captureCamera.transform.position, point);
            float height = 2f * depth * Mathf.Tan(captureCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            captureCamera.transform.position -= captureCamera.transform.right * (height * captureCamera.aspect * 0.06f)
                + captureCamera.transform.up * (height * 0.055f);
        }

        void ApplyHeroCamera(float time)
        {
            if (heroRenderers == null || heroRenderers.Length == 0) return;

            Bounds bounds = new Bounds(tool.position, Vector3.zero);
            bool first = true;
            foreach (var renderer in heroRenderers)
                if (renderer && renderer.transform.IsChildOf(tool))
                { if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds); }
            Vector3 surface = panelCollider.transform.TransformPoint(panelCollider.center + Vector3.Scale(panelCollider.size * 0.5f, faceLocalNormal));
            bounds.Encapsulate(new Bounds(surface, new Vector3(0.9f, 1f, 0.3f)));
            Vector3 view = Quaternion.Euler(0f, Mathf.Sin(time * 0.4f) * 3.5f, 0f) * new Vector3(-0.85f, 0.8f, -0.65f).normalized;
            Quaternion rotation = Quaternion.LookRotation(-view, Vector3.up);
            Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up;
            captureCamera.fieldOfView = 48f;
            float tanY = Mathf.Tan(24f * Mathf.Deg2Rad), tanX = tanY * captureCamera.aspect;
            float distance = 1f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                float along = Vector3.Dot(corner, view);
                distance = Mathf.Max(distance, along + Mathf.Abs(Vector3.Dot(corner, right)) / (tanX * 0.86f),
                    along + Mathf.Abs(Vector3.Dot(corner, up)) / (tanY * 0.72f));
            }
            captureCamera.transform.SetPositionAndRotation(bounds.center + view * (distance + 0.12f), rotation);
        }

        static float PhaseAt(float t)
        {
            if (t >= 21.5f && t < 38f) return 1.6f;
            if (t >= 47f && t < 52f) return 1.6f;
            if (t >= 38f && t < 42f) return Mathf.Repeat(t - 38f, 4f);
            if (t >= 42f && t < 47f) return Mathf.Repeat(t - 42f, 4f);
            if (t >= 52f) return Mathf.Repeat(t - 52f, 4f);
            return Mathf.Repeat(t - 8f, 4f);
        }
        static Vector3 Abs(Vector3 value) { return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z)); }
    }
}

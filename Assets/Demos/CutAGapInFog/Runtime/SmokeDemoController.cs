using System;
using UnityEngine;
using NumericsVector = System.Numerics.Vector3;

namespace GameFeelClinic.ResponsiveSmoke
{
    public enum SmokePass { Intact, WholeFade, LocalCut, ShallowCut, ExtendedCut, ExtendDepth, ShowLocalCut }

    [Serializable]
    public struct SmokeFrameState
    {
        public string pass;
        public float seconds, fogTime, burstStrength, wholeOpacity, cutStrength, cutRadius, cutSoftness;
        public float nearCenterDensity, farCenterDensity, leftDensity, rightDensity;
        public Vector3 cutA, cutB, cameraPosition, cartPosition, centerViewport, leftViewport, rightViewport;
        public Quaternion cameraRotation;
        public float fieldOfView;
    }

    public sealed class SmokeDemoController : MonoBehaviour
    {
        public const float BurstAt = 1f;
        public const float HoldSeconds = 1.2f;
        public const float RecoverSeconds = 3f;
        public const float PreviewLength = 6.5f;

        public Camera captureCamera;
        public Renderer fogVolume;
        public Transform centerAnchor, leftAnchor, rightAnchor, cart, cartPulse, fan;
        public Transform[] snow = Array.Empty<Transform>();
        public Vector3[] snowBases = Array.Empty<Vector3>();
        public Vector3 cartBase;
        public SmokePass previewPass = SmokePass.LocalCut;
        public float playbackSeconds;
        public bool autoplay = true;
        public bool customBeaconImported;

        [Min(.1f)] public float cutRadius = .98f;
        [Min(.01f)] public float cutSoftness = .24f;
        public Vector3 cutStart = new Vector3(0f, 1.6f, -2.2f);
        public Vector3 shallowEnd = new Vector3(0f, 1.6f, -1.32f);
        public Vector3 extendedEnd = new Vector3(0f, 1.6f, 2.2f);

        public SmokeFrameState State { get; private set; }
        public bool HasState { get; private set; }
        MaterialPropertyBlock properties;
        float startTime;

        static readonly int FogTime = Shader.PropertyToID("_FogTime");
        static readonly int WholeOpacity = Shader.PropertyToID("_WholeOpacity");
        static readonly int CutStrength = Shader.PropertyToID("_CutStrength");
        static readonly int CutA = Shader.PropertyToID("_CutA");
        static readonly int CutB = Shader.PropertyToID("_CutB");
        static readonly int CutRadius = Shader.PropertyToID("_CutRadius");
        static readonly int CutSoftness = Shader.PropertyToID("_CutSoftness");

        void OnEnable()
        {
            startTime = Time.time - playbackSeconds;
#if UNITY_EDITOR
            if (captureCamera && captureCamera.overrideSceneCullingMask != 0 && gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.SetSceneCullingMask(
                    gameObject.scene, captureCamera.overrideSceneCullingMask);
#endif
        }

        void Update()
        {
            if (autoplay && captureCamera && fogVolume)
                Evaluate(previewPass, Mathf.Repeat(Time.time - startTime, PreviewLength));
        }

        public SmokeFrameState EvaluateAt(float seconds) => Evaluate(previewPass, seconds);

        public SmokeFrameState Evaluate(SmokePass pass, float seconds, bool holdCut = false)
        {
            if (!captureCamera || !fogVolume || !centerAnchor || !leftAnchor || !rightAnchor)
                throw new InvalidOperationException("The owned smoke demonstration needs its camera, fog and three beacon anchors.");
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds));

            float strength = seconds < BurstAt || pass == SmokePass.Intact
                ? 0f
                : SmokeFieldMath.RecoveryStrength(seconds - BurstAt, HoldSeconds, RecoverSeconds);
            if (holdCut && pass != SmokePass.Intact) strength = 1f;
            float eventAt = BurstAt;
            if (pass == SmokePass.ShowLocalCut)
            {
                eventAt = 2.45f;
                strength = Mathf.SmoothStep(0f, 1f, (seconds - eventAt) / .20f);
            }
            float whole = pass == SmokePass.WholeFade ? Mathf.Lerp(1f, .025f, strength) : 1f;
            float cut = pass == SmokePass.WholeFade || pass == SmokePass.Intact ? 0f : strength;
            Vector3 end = pass == SmokePass.ShallowCut ? shallowEnd : extendedEnd;
            if (pass == SmokePass.ExtendDepth)
                end = Vector3.Lerp(shallowEnd, extendedEnd, Mathf.SmoothStep(0f, 1f, (seconds - 1.3f) / 1.1f));

            properties ??= new MaterialPropertyBlock();
            fogVolume.GetPropertyBlock(properties);
            properties.SetFloat(FogTime, seconds);
            properties.SetFloat(WholeOpacity, whole);
            properties.SetFloat(CutStrength, cut);
            properties.SetFloat(CutRadius, cutRadius);
            properties.SetFloat(CutSoftness, cutSoftness);
            properties.SetVector(CutA, new Vector4(cutStart.x, cutStart.y, cutStart.z, 0f));
            properties.SetVector(CutB, new Vector4(end.x, end.y, end.z, 0f));
            fogVolume.SetPropertyBlock(properties);

            if (cart) cart.position = cartBase + Vector3.right * (.32f * Mathf.Sin(seconds * 1.1f));
            if (cartPulse)
            {
                float kick = Mathf.Clamp01(1f - Mathf.Abs(seconds - eventAt) / .22f);
                cartPulse.localScale = Vector3.one * (.13f + .055f * Mathf.Sin(seconds * 7f) + .12f * kick);
            }
            if (fan) fan.localRotation = Quaternion.Euler(0f, 0f, seconds * 95f);
            for (int i = 0; i < snow.Length && i < snowBases.Length; i++)
            {
                if (!snow[i]) continue;
                Vector3 seed = snowBases[i];
                float fall = Mathf.Repeat(seed.y - 3.35f - seconds * (.28f + .025f * (i % 5)), 1.75f);
                snow[i].position = new Vector3(seed.x + .095f * Mathf.Sin(seconds * .8f + i), 3.35f + fall, seed.z);
            }

            playbackSeconds = seconds;
            State = new SmokeFrameState
            {
                pass = pass.ToString(),
                seconds = seconds,
                fogTime = seconds,
                burstStrength = strength,
                wholeOpacity = whole,
                cutStrength = cut,
                cutRadius = cutRadius,
                cutSoftness = cutSoftness,
                cutA = cutStart,
                cutB = end,
                nearCenterDensity = Sample(new Vector3(0f, 1.6f, -1.52f), end, cut, whole),
                farCenterDensity = Sample(new Vector3(0f, 1.6f, 1.52f), end, cut, whole),
                leftDensity = Sample(new Vector3(-1.78f, 1.6f, 0f), end, cut, whole),
                rightDensity = Sample(new Vector3(1.78f, 1.6f, 0f), end, cut, whole),
                cameraPosition = captureCamera.transform.position,
                cameraRotation = captureCamera.transform.rotation,
                fieldOfView = captureCamera.fieldOfView,
                cartPosition = cart ? cart.position : Vector3.zero,
                centerViewport = captureCamera.WorldToViewportPoint(centerAnchor.position),
                leftViewport = captureCamera.WorldToViewportPoint(leftAnchor.position),
                rightViewport = captureCamera.WorldToViewportPoint(rightAnchor.position)
            };
            HasState = true;
            return State;
        }

        float Sample(Vector3 point, Vector3 end, float strength, float whole)
        {
            return SmokeFieldMath.DensityMultiplier(ToNumerics(point), ToNumerics(cutStart),
                ToNumerics(end), cutRadius, cutSoftness, strength, whole);
        }

        static NumericsVector ToNumerics(Vector3 value) => new NumericsVector(value.x, value.y, value.z);
    }
}

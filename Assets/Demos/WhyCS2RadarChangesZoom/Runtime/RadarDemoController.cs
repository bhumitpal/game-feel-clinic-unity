using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFeelClinic.DynamicRadar
{
    [Serializable]
    public struct RadarEpisodeState
    {
        public float time, scenarioTime, displayedRange, requiredRange, hold;
        public Vector3 player, scout, rover;
        public bool scoutKnown, baseline, maximum;
    }

    public sealed class RadarDemoController : MonoBehaviour
    {
        public Camera captureCamera;
        public Transform player, scout, rover;
        public Transform[] playerRotors, scoutRotors, roverWheels, windVanes;
        public RectTransform radarRoot, radarMap, playerIcon, scoutIcon, roverIcon, scoutEdge;
        public RectTransform playerLabel, scoutLabel, roverLabel;
        public TextMeshProUGUI modeLabel, rangeLabel, paddingLabel;
        public CanvasGroup labelsGroup;
        public RadarMapGraphic mapGraphic;
        public ParticleSystem downwash;
        public float duration = 61.27f;
        public float baselineStart = 13.67f, baselineEnd = 21.37f, revisedStart = 35.37f;
        public float paddingStart = 26.77f, shrinkStart = 30.87f, detailEnd = 43.37f;
        public float unknownStart = 48.17f, unknownEnd = 51.87f, limitEnd = 56.87f;
        public bool lookDevelopment;
        public bool autoplay = true;
        public float playbackTime;
        public Color playerColor = new Color(0.08f, 0.91f, 1f);
        public Color scoutColor = new Color(1f, 0.68f, 0.12f);
        public Color roverColor = new Color(0.70f, 0.42f, 1f);
        public RadarEpisodeState State { get; private set; }
        public event Action<RadarEpisodeState> SnapshotChanged;

        const double Step = 1.0 / 120;
        readonly RadarMarker[] markers = new RadarMarker[2];
        RadarRangeModel range;
        int simulatedSteps = -1;
        double simulationTime;
        float clockStart;
        int previousSegment = -1;
        Vector3 baseCameraPosition;
        Quaternion baseCameraRotation;
        bool initialized;

        void Awake() { Initialize(); }
        void OnEnable() { clockStart = Time.time - playbackTime; }
        void Initialize()
        {

            if (initialized && range != null) return;
            range = new RadarRangeModel();
            simulatedSteps = -1; previousSegment = -1; simulationTime = 0;
            baseCameraPosition = captureCamera.transform.position;
            baseCameraRotation = captureCamera.transform.rotation;
            initialized = true;
        }
        void Update()
        {
            if (autoplay)
            {
                playbackTime = Mathf.Repeat(Time.time - clockStart, duration);
                EvaluateAt(playbackTime);
            }
        }

        public RadarEpisodeState EvaluateAt(float seconds)
        {
            Initialize();
            seconds = Mathf.Clamp(seconds, 0, duration);
            int targetSteps = (int)Math.Floor(seconds / Step + 0.00001);
            if (targetSteps < simulatedSteps || simulatedSteps < 0)
            {
                range.Reset();
                simulatedSteps = 0;
                simulationTime = 0;
                previousSegment = -1;
                UpdateRange(0, 0);
            }
            while (simulatedSteps < targetSteps)
            {
                simulatedSteps++;
                simulationTime = simulatedSteps * Step;
                UpdateRange((float)simulationTime, Step);
            }
            RadarEpisodeState state = SampleScenario(seconds);

            state.requiredRange = (float)range.Required;
            state.displayedRange = state.baseline ? state.requiredRange : (float)range.Current;
            state.hold = (float)range.HoldRemaining;
            state.maximum = range.MaximumReached;
            State = state;
            ApplyWorld(state);
            ApplyHUD(state);
            SnapshotChanged?.Invoke(state);
            return state;
        }

        void UpdateRange(float t, double dt)
        {
            var s = SampleScenario(t);
            markers[0] = new RadarMarker("rover", s.rover.x - s.player.x, s.rover.z - s.player.z, true);
            markers[1] = new RadarMarker("scout", s.scout.x - s.player.x, s.scout.z - s.player.z, s.scoutKnown);

            int segment = SegmentAt(t);
            if (segment != previousSegment && (segment == 1 || segment == 2 || segment == 5)) range.Reset();
            previousSegment = segment;
            range.Update(markers, dt);
        }

        int SegmentAt(float t)
        {
            if (lookDevelopment) return 0;
            if (t < baselineStart) return 0;
            if (t < baselineEnd) return 1;
            if (t < paddingStart) return 2;
            if (t < shrinkStart) return 3;
            if (t < revisedStart) return 4;
            if (t < detailEnd) return 5;
            if (t < unknownStart) return 6;
            if (t < unknownEnd) return 7;
            if (t < limitEnd) return 8;
            return 9;
        }

        public RadarEpisodeState SampleScenario(float t)
        {
            bool baseline = !lookDevelopment && t >= baselineStart && t < baselineEnd;
            float phase;
            float distance;
            if (lookDevelopment)
            {
                phase = Mathf.Repeat(t, 3);
                if (phase < 0.5f) distance = Mathf.Lerp(12, 40, Smooth(phase / 0.5f));
                else if (phase < 1) distance = 40;
                else if (phase < 1.4f) distance = Mathf.Lerp(40, 12, Smooth((phase - 1) / 0.4f));
                else distance = 12;
            }
            else if (t < baselineStart) { phase = Mathf.Clamp(t - 10.47f, 0, 3.2f); distance = 12 + 8 * Mathf.Sin(Mathf.PI * phase / 3.2f); }
            else if (t < baselineEnd) { phase = t - baselineStart; distance = RouteDistance(phase); }
            else if (t < paddingStart) { phase = t - baselineEnd; distance = Mathf.Lerp(12, 40, Smooth(Mathf.Clamp01(phase / 3.5f))); }
            else if (t < shrinkStart) { phase = t - paddingStart; distance = 40; }
            else if (t < revisedStart) { phase = t - shrinkStart; distance = Mathf.Lerp(40, 12, Smooth(Mathf.Clamp01(phase / 1.2f))); }
            else if (t < detailEnd) { phase = t - revisedStart; distance = RouteDistance(phase); }
            else if (t < unknownStart) { phase = t - detailEnd; distance = 28; }
            else if (t < unknownEnd) { phase = t - unknownStart; distance = 40; }
            else if (t < limitEnd) { phase = t - unknownEnd; distance = Mathf.Lerp(40, 90, Smooth(Mathf.Clamp01(phase / 3))); }
            else { phase = t - limitEnd; distance = 20; }

            Vector3 playerGround = new Vector3(0, 0, 0);
            Vector3 direction = new Vector3(0.12f, 0, 0.992774f).normalized;
            return new RadarEpisodeState
            {
                time = t,
                scenarioTime = phase,
                baseline = baseline,
                player = playerGround,
                scout = playerGround + direction * distance,
                rover = new Vector3(-3.5f, 0, 8),
                scoutKnown = lookDevelopment || !(t >= unknownStart && t < unknownEnd)
            };
        }
        static float RouteDistance(float phase)
        {
            float p = Mathf.Repeat(phase, 8);
            if (p < 2) return Mathf.Lerp(12, 40, Smooth(p / 2));
            if (p < 2.8f) return 40;
            if (p < 4.6f) return Mathf.Lerp(40, 12, Smooth((p - 2.8f) / 1.8f));
            if (p < 5.4f) return Mathf.Lerp(12, 40, Smooth((p - 4.6f) / 0.8f));
            return Mathf.Lerp(40, 12, Smooth((p - 5.4f) / 2.6f));
        }
        static float Smooth(float value) { return value * value * (3 - 2 * value); }

        void ApplyWorld(RadarEpisodeState s)
        {
            float visualTime = s.baseline || (s.time >= revisedStart && s.time < detailEnd) ? s.scenarioTime : s.time;
            player.position = s.player + Vector3.up * (1.8f + 0.12f * Mathf.Sin(visualTime * 2.4f));
            player.rotation = Quaternion.Euler(2 * Mathf.Sin(visualTime), 12 * Mathf.Sin(visualTime * 0.5f), 3 * Mathf.Sin(visualTime * 1.7f));
            scout.position = s.scout + Vector3.up * (3.4f + 0.18f * Mathf.Sin(visualTime * 3.1f));
            float aheadDistance = RouteDistance(s.scenarioTime + 0.05f) - RouteDistance(s.scenarioTime);
            scout.rotation = Quaternion.Euler(aheadDistance * 10, 25, -aheadDistance * 8);
            rover.position = s.rover;
            if (downwash)
            {
                downwash.transform.position = s.player + Vector3.up * 0.08f;
                downwash.Simulate(visualTime, true, true, true);
            }
            SetSpin(playerRotors, visualTime * 1600);
            SetSpin(scoutRotors, -visualTime * 2300);
            if (windVanes != null)
                for (int i = 0; i < windVanes.Length; i++)
                    if (windVanes[i]) windVanes[i].localRotation = Quaternion.Euler(0, s.time * 20 + i * 43, 0);

            captureCamera.transform.position = baseCameraPosition + new Vector3(0.35f * Mathf.Sin(visualTime * 0.18f), 0.12f * Mathf.Sin(visualTime * 0.23f), 0);
            captureCamera.transform.rotation = baseCameraRotation;
        }
        static void SetSpin(Transform[] transforms, float angle)
        {
            if (transforms == null) return;
            for (int i = 0; i < transforms.Length; i++)
                if (transforms[i]) transforms[i].localRotation = Quaternion.Euler(0, 0, angle + i * 90);
        }

        void ApplyHUD(RadarEpisodeState s)
        {
            float takeover = lookDevelopment ? 0 : Smooth(Mathf.Clamp01((s.time - baselineStart + 0.7f) / 0.7f)) * (1 - Smooth(Mathf.Clamp01((s.time - detailEnd) / 0.7f)));
            radarRoot.anchorMin = radarRoot.anchorMax = new Vector2(0.5f, Mathf.Lerp(1, 0.5f, takeover));
            radarRoot.pivot = new Vector2(0.5f, 0.5f);
            radarRoot.anchoredPosition = new Vector2(Mathf.Lerp(-190, 0, takeover), Mathf.Lerp(-450, 230, takeover));
            float diameter = Mathf.Lerp(520, 830, takeover);
            radarRoot.sizeDelta = Vector2.one * diameter;
            var background = radarRoot.GetComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.008f, 0.025f, 0.035f, Mathf.Lerp(0.9f, 1, takeover));
            float radius = diameter * 0.46f;
            float displayed = Mathf.Max(0.001f, s.displayedRange);
            playerIcon.anchoredPosition = Vector2.zero;
            SetMarker(roverIcon, s.rover - s.player, displayed, radius, true);
            Vector2 scoutOffset = new Vector2(s.scout.x - s.player.x, s.scout.z - s.player.z);
            bool edge = s.scoutKnown && scoutOffset.magnitude > displayed;
            SetMarker(scoutIcon, s.scout - s.player, displayed, radius, s.scoutKnown && !edge);
            scoutEdge.gameObject.SetActive(edge);
            if (edge)
            {
                scoutEdge.anchoredPosition = scoutOffset.normalized * (radius - 18);
                scoutEdge.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(scoutOffset.x, scoutOffset.y) * Mathf.Rad2Deg);
            }
            mapGraphic.SetProjection(new Vector2(s.player.x, s.player.z), displayed, radius);
            rangeLabel.text = "Range " + Mathf.RoundToInt(displayed) + " m";
            if (paddingLabel)
            {
                bool showMargin = !lookDevelopment && s.time >= paddingStart && s.time < shrinkStart && s.scoutKnown &&
                    Mathf.Abs(s.requiredRange - (scoutOffset.magnitude + (float)range.Padding)) < 0.05f;
                paddingLabel.gameObject.SetActive(showMargin);
                if (showMargin) paddingLabel.text = Mathf.RoundToInt(scoutOffset.magnitude) + " m + " + Mathf.RoundToInt((float)range.Padding) + " m = " + Mathf.RoundToInt(displayed) + " m";
            }
            modeLabel.text = s.baseline ? "Instant fit" : "Quick out, slow in";
            modeLabel.gameObject.SetActive(takeover > 0.95f);

            labelsGroup.alpha = lookDevelopment || (s.time >= 10.47f && s.time < baselineStart) ? 1 : 0;
            PlaceWorldLabel(playerLabel, player.position + Vector3.up * 0.9f);
            PlaceWorldLabel(scoutLabel, scout.position + Vector3.up * 0.9f);
            PlaceWorldLabel(roverLabel, rover.position + Vector3.up * 2f);
            SetIdentity(playerIcon, labelsGroup.alpha > 0);
            SetIdentity(scoutIcon, labelsGroup.alpha > 0);
            SetIdentity(roverIcon, labelsGroup.alpha > 0);
        }
        static void SetIdentity(RectTransform icon, bool visible)
        {
            var identity = icon.Find("Identity"); if (identity) identity.gameObject.SetActive(visible);
        }
        public void ResetCameraReference()
        {
            baseCameraPosition = captureCamera.transform.position;
            baseCameraRotation = captureCamera.transform.rotation;
        }
        static void SetMarker(RectTransform icon, Vector3 offset, float rangeValue, float radius, bool visible)
        {
            icon.gameObject.SetActive(visible);
            icon.anchoredPosition = new Vector2(offset.x, offset.z) * (radius / rangeValue);
        }
        void PlaceWorldLabel(RectTransform label, Vector3 world)
        {
            Vector3 viewport = captureCamera.WorldToViewportPoint(world);
            label.gameObject.SetActive(viewport.z > 0 && viewport.x > 0.04f && viewport.x < 0.92f && viewport.y > 0.2f && viewport.y < 0.7f);
            label.anchorMin = label.anchorMax = new Vector2(viewport.x, viewport.y);
            label.anchoredPosition = Vector2.zero;
        }
    }

}

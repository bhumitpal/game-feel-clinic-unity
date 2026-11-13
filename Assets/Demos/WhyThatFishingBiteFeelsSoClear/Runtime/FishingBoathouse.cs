using UnityEngine;

namespace GameFeelClinic.Fishing
{
    public struct FishingCueLatch
    {
        public bool Fired { get; private set; }
        public float ContactTime { get; private set; }

        public bool Step(float previous, float current, bool committed)
        {
            if (Fired || !FishingBiteModel.TryCrossing(previous, current, committed, out float contact))
                return false;
            Fired = true;
            ContactTime = contact;
            return true;
        }
    }

    [System.Serializable]
    public struct FishingFrame
    {
        public float seconds, simulationTime, waterY, floatY, cueAge;
        public Vector3 floatViewport;
        public bool biteCommitted, reelOpen, cueTriggered, caught;
    }

    public sealed class FishingBoathouse : MonoBehaviour
    {
        public Camera shotCamera;
        public Renderer water;
        public Transform actor, boat, bobber, catchFish, lantern;
        public Transform rodHandle, reelHandle, lineTip;
        public Transform actorUpper, actorArm, actorRod, actorRodMid, actorRodTip;
        public Quaternion upperRest, armRest, rodRest, midRest;
        public Transform[] reeds, droplets;
        public LineRenderer rod, fishingLine;
        public LineRenderer[] rings;
        public AudioSource plop;
        public AudioClip plopClip;
        public bool autoplay = true;

        static readonly int FlowTime = Shader.PropertyToID("_FlowTime");
        MaterialPropertyBlock waterProperties;
        FishingCueLatch liveCue;
        float startedAt, previous;

        public static float Crossing { get; } = FishingBiteModel.FirstCrossing(120);

        void OnEnable()
        {
            startedAt = Time.unscaledTime;
            previous = 2.04f;
            liveCue = default;
            if (Application.isPlaying && shotCamera && !FindAnyObjectByType<AudioListener>())
                shotCamera.gameObject.AddComponent<AudioListener>();
#if UNITY_EDITOR
            if (shotCamera && shotCamera.overrideSceneCullingMask != 0)
                UnityEditor.SceneManagement.EditorSceneManager.SetSceneCullingMask(gameObject.scene, shotCamera.overrideSceneCullingMask);
#endif
        }

        void Update()
        {
            if (!autoplay) return;
            const float start = 2.04f;
            float time = start + Mathf.Repeat(Time.unscaledTime - startedAt, FishingBiteModel.PreviewLength - start);
            if (time < previous) { previous = start; liveCue = default; }
            if (liveCue.Step(previous, time, true) && plop && plopClip)
            {
                Vector2 point = FishingBiteModel.FloatPosition;
                plop.transform.position = new Vector3(point.x, FishingBiteModel.WaterHeight(point, liveCue.ContactTime), point.y);
                plop.PlayOneShot(plopClip);
                Debug.Log("Fishing bite crossed the surface at " + liveCue.ContactTime.ToString("F4") + " s");
            }
            Apply(time, time, "proof", liveCue.Fired ? liveCue.ContactTime : -1f);
            previous = time;
        }

        static float Segment(float t, float a, float b, float c, float d) =>
            Mathf.Lerp(c, d, Mathf.InverseLerp(a, b, t));

        public static float SimulationTime(string shot, float t)
        {
            switch (shot)
            {
                case "baseline":
                    return t <= 5.3f ? Segment(t, 0, 5.3f, 0, FishingBiteModel.FalseCueAt)
                        : Segment(t, 5.3f, 8, FishingBiteModel.FalseCueAt, 3.65f);
                case "fix":
                    if (t <= 3.2f) return Segment(t, 0, 3.2f, 1.65f, FishingBiteModel.CommitAt);
                    if (t <= 8.6f) return Segment(t, 3.2f, 8.6f, FishingBiteModel.CommitAt, Crossing);
                    return Segment(t, 8.6f, 11, Crossing, 3.65f);
                case "proof":
                    if (t <= 3.7f) return Segment(t, 0, 3.7f, 0, Crossing);
                    if (t <= 6.4f) return Segment(t, 3.7f, 6.4f, Crossing, FishingBiteModel.ReelAt);
                    return Segment(t, 6.4f, 11, FishingBiteModel.ReelAt, FishingBiteModel.PreviewLength);
                case "payoff":
                    return Segment(t, 0, 8.8f, 3.5f, FishingBiteModel.PreviewLength);
                default: return t;
            }
        }

        public FishingFrame Evaluate(string shot, float seconds)
        {
            float time = SimulationTime(shot, seconds);
            float cue = shot == "baseline" ? FishingBiteModel.FalseCueAt : Crossing;
            return Apply(time, seconds, shot, time >= cue ? cue : -1f);
        }

        FishingFrame Apply(float time, float elapsed, string shot, float cue)
        {
            if (waterProperties == null) waterProperties = new MaterialPropertyBlock();
            waterProperties.SetFloat(FlowTime, time);
            water.SetPropertyBlock(waterProperties);
            Vector2 location = FishingBiteModel.FloatPosition;
            float waterY = FishingBiteModel.WaterHeight(location, time);
            float pull = FishingBiteModel.Pull(time);
            float reel = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(FishingBiteModel.ReelAt, 5.15f, time));
            float swing = Mathf.Sin(reel * Mathf.PI);
            Vector3 surfacePoint = new Vector3(location.x, waterY, location.y);
            Vector3 baseFloat = surfacePoint + Vector3.up * FishingBiteModel.RelativeHeight(time, true);
            Vector3 floatPosition = Vector3.Lerp(baseFloat, new Vector3(-.48f, .88f, .36f), reel) + Vector3.up * (swing * .87f);
            bobber.position = floatPosition;
            bobber.rotation = Quaternion.Euler(14f * pull + 5f * Mathf.Sin(time * 2.3f), 0, -22f * pull + 8f * Mathf.Sin(time * 2.7f));
            if (actor && !actorUpper)
                actor.localRotation = Quaternion.Euler(-3f + 7.5f * pull - 8f * swing, 0, 2.5f * Mathf.Sin(time * 1.5f) - 4f * pull);
            if (boat)
            {
                const float boatX = .95f;
                const float boatZ = 3.03f;
                boat.localPosition = new Vector3(boatX, FishingBiteModel.WaterHeight(new Vector2(boatX, boatZ), time) - .035f, boatZ);
                boat.localRotation = Quaternion.Euler(2.5f * Mathf.Sin(time * 1.3f), -26, 4.5f * Mathf.Sin(time * 1.53f));
            }
            for (int i = 0; i < reeds.Length; i++)
                reeds[i].localRotation = Quaternion.Euler(3f * Mathf.Sin(time * 1.7f + i), i * 41f, 6f * Mathf.Sin(time * 2 + i * .6f));
            if (lantern) lantern.localRotation = Quaternion.Euler(0, 0, 4.5f * Mathf.Sin(time * 1.47f));

            Vector3 grip = new Vector3(-1.1f, 1.47f + .14f * swing - .048f * pull, .58f);
            Vector3 forward = new Vector3(1.04f + .16f * swing, .65f + .4f * swing, .32f);
            Vector3 tip;
            if (actorUpper && actorArm && actorRod && actorRodMid && actorRodTip)
            {
                actorUpper.localRotation = upperRest;
                actorArm.localRotation = armRest;
                actorRod.localRotation = rodRest;
                actorRodMid.localRotation = midRest;
                Vector3 hinge = Vector3.Cross(actorRodTip.position - actorRod.position, Vector3.down).normalized;
                actorUpper.Rotate(hinge, 1.8f * Mathf.Sin(time * 1.6f) + 4f * pull - 7f * swing, Space.World);
                actorArm.Rotate(hinge, 9.5f * pull - 29f * swing, Space.World);
                actorRodMid.Rotate(hinge, 29f * pull - 4f * swing, Space.World);
                tip = actorRodTip.position;
            }
            else
            {
                if (rodHandle) { rodHandle.position = grip; rodHandle.up = forward.normalized; }
                if (reelHandle) reelHandle.localRotation = Quaternion.Euler(0, reel * 1200f, 90);
                int rodPoints = rod.positionCount;
                for (int i = 0; i < rodPoints; i++)
                {
                    float p = i / (float)(rodPoints - 1);
                    Vector3 point = grip + forward * p + Vector3.down * (p * p * (.05f + .66f * pull));
                    rod.SetPosition(i, point);
                }
                tip = rod.GetPosition(rodPoints - 1);
            }
            if (lineTip) lineTip.position = tip;
            for (int i = 0; i < fishingLine.positionCount; i++)
            {
                float p = i / (float)(fishingLine.positionCount - 1);
                fishingLine.SetPosition(i, Vector3.Lerp(tip, floatPosition + .11f * Vector3.up, p) + Vector3.down * (Mathf.Sin(p * Mathf.PI) * .15f * (1f - pull)));
            }

            bool caught = time >= FishingBiteModel.ReelAt && FishingBiteModel.CanReel(FishingBiteModel.ReelAt, true);
            if (catchFish)
            {
                catchFish.gameObject.SetActive(caught);
                catchFish.position = floatPosition - new Vector3(.07f, .24f, .03f);
                catchFish.rotation = Quaternion.Euler(0, 15f + 25f * Mathf.Sin(time * 12), 20f + 32f * Mathf.Sin(time * 9));
            }

            float age = cue < 0 ? -1 : time - cue;
            for (int i = 0; i < rings.Length; i++)
            {
                LineRenderer ring = rings[i];
                float ringAge = age - i * .13f;
                bool visible = ringAge >= 0 && ringAge < 1.08f;
                ring.enabled = visible;
                if (!visible) continue;
                float radius = .07f + ringAge * (1.12f - .14f * i);
                Color ink = shot == "baseline" ? new Color(1f, .59f, .28f) : new Color(.79f, 1f, .92f);
                ink.a = (1f - ringAge / 1.08f) * .93f;
                ring.startColor = ink;
                ring.endColor = ink;
                ring.widthMultiplier = Mathf.Lerp(.036f, .008f, ringAge / 1.08f);
                for (int p = 0; p < ring.positionCount; p++)
                {
                    float angle = p * 2f * Mathf.PI / ring.positionCount;
                    Vector2 point = location + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    ring.SetPosition(p, new Vector3(point.x, FishingBiteModel.WaterHeight(point, time) + .015f, point.y));
                }
            }
            for (int i = 0; i < droplets.Length; i++)
            {
                bool visible = age >= 0 && age <= .46f;
                Transform drop = droplets[i];
                drop.gameObject.SetActive(visible);
                if (!visible) continue;
                float angle = i * 2.39996f;
                float speed = 1f + (i % 5) * .19f;
                float up = age * (2.35f + (i % 4) * .43f) - age * age * 5.7f;
                Vector3 aim = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (age * speed);
                drop.position = surfacePoint + aim + Vector3.up * Mathf.Max(.025f, up);
                drop.localScale = Vector3.one * (.042f - .028f * age / .46f);
            }

            PositionCamera(shot, elapsed);
            return new FishingFrame
            {
                seconds = elapsed, simulationTime = time, waterY = waterY, floatY = floatPosition.y, cueAge = age,
                floatViewport = shotCamera.WorldToViewportPoint(floatPosition),
                biteCommitted = time >= FishingBiteModel.CommitAt, reelOpen = FishingBiteModel.CanReel(time, true),
                cueTriggered = cue >= 0, caught = caught
            };
        }

        void PositionCamera(string shot, float elapsed)
        {
            Vector3 target = new Vector3(-.32f, .49f, .96f);
            Vector3 point = new Vector3(2.20f, 2.03f, -5.89f);
            float fov = 44;
            if (shot == "fix")
            {
                float zoom = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.8f, 4.7f, elapsed));
                target = Vector3.Lerp(target, new Vector3(.32f, .21f, .91f), zoom);
                point = Vector3.Lerp(point, new Vector3(2.50f, 1.35f, -3.58f), zoom);
                fov = Mathf.Lerp(fov, 32, zoom);
            }
            else if (shot == "payoff")
            {
                float caught = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.16f, 3.2f, elapsed));
                point = Vector3.Lerp(new Vector3(2.23f, 1.95f, -5.62f), new Vector3(1.86f, 2.05f, -4.92f), caught);
                target = Vector3.Lerp(new Vector3(-.14f, .61f, .96f), new Vector3(-.67f, 1.03f, .53f), caught);
                fov = Mathf.Lerp(43, 40, caught);
            }
            shotCamera.transform.SetPositionAndRotation(point, Quaternion.LookRotation(target - point, Vector3.up));
            shotCamera.fieldOfView = fov;
        }
    }
}

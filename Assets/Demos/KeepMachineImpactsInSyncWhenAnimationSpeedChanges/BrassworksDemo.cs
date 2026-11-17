using UnityEngine;

namespace GameFeelClinic.Brassworks
{
    [DefaultExecutionOrder(100)]
    public sealed class BrassworksDemo : MonoBehaviour
    {
        public PressImpactAudio impactAudio;
        public Camera view;
        public Transform sparkOrigin;
        public Transform[] sparks;
        public Light contactFlash;
        public Renderer soundLamp;
        public Material soundLit;
        public Material soundDark;
        public Transform[] flywheels;
        public Transform[] castSlugs;
        public bool Manual { get; set; }
        public float Age { get; private set; }
        public bool ContactThisFrame { get; private set; }
        public bool SoundThisFrame { get; private set; }
        public float Phase => impactAudio.NormalizedTime;

        float priorContact;
        float contactAge;
        float soundAge;
        float lastLiveAge;
        int loopId;

        public static float SpeedAt(float elapsed) => elapsed < 2.55f ? 1f : elapsed < 5.60f ? 1.75f : .75f;

        public static float PhaseAt(float elapsed)
        {
            float slow = Mathf.Min(elapsed, 2.55f);
            float fast = Mathf.Clamp(elapsed - 2.55f, 0, 3.05f) * 1.75f;
            float final = Mathf.Max(elapsed - 5.60f, 0) * .75f;
            return (slow + fast + final) / 2f;
        }

        void Start()
        {
            ResetDemo();
            Frame(0, 0);
        }

        void Update()
        {
            if (Manual) return;
            Age += Time.deltaTime;
            if (Age >= 9.30f) ResetDemo();
            impactAudio.Animator.speed = SpeedAt(Age);
        }

        void LateUpdate()
        {
            if (Manual) return;
            Evaluate(Age, impactAudio.Mode, true);
            Frame(0, Age);
        }

        public void ResetDemo()
        {
            Age = 0;
            lastLiveAge = 0;
            priorContact = 0;
            contactAge = 2;
            soundAge = 2;
            loopId = 0;
            impactAudio.ResetTiming();
            impactAudio.Animator.speed = Manual ? 0f : 1f;
            impactAudio.Animator.Play(impactAudio.LoopHash, 0, 0);
            impactAudio.Animator.Update(0);
            Evaluate(0, impactAudio.Mode, false);
        }

        public void Sample(float elapsed, PressImpactAudio.TimingMode mode)
        {
            Age = elapsed;
            impactAudio.Animator.Play(impactAudio.LoopHash, 0, PhaseAt(elapsed));
            impactAudio.Animator.Update(0);
            Evaluate(elapsed, mode, false);
        }

        void Evaluate(float elapsed, PressImpactAudio.TimingMode mode, bool playAudio)
        {
            float dt = Mathf.Max(0, elapsed - lastLiveAge);
            lastLiveAge = elapsed;
            float phase = impactAudio.NormalizedTime;
            float touch = .60f;
            ContactThisFrame = Mathf.FloorToInt(phase - touch) > Mathf.FloorToInt(priorContact - touch);
            SoundThisFrame = impactAudio.Tick(elapsed, mode, playAudio);
            priorContact = phase;
            if (ContactThisFrame) loopId++;
            contactAge = ContactThisFrame ? 0 : contactAge + dt;
            soundAge = SoundThisFrame ? 0 : soundAge + dt;
            contactFlash.intensity = 18f * Mathf.Clamp01(1f - contactAge / .16f);
            soundLamp.sharedMaterial = soundAge < .145f ? soundLit : soundDark;

            for (int i = 0; i < sparks.Length; i++)
            {
                var spark = sparks[i];
                float t = contactAge;
                float duration = .24f + (i % 7) * .032f;
                spark.gameObject.SetActive(t < duration);
                if (t >= duration) continue;
                float angle = i * 2.399963f + loopId * .4f;
                float speed = 1.6f + (i % 11) * .23f;
                var vertical = 1.3f + (i % 6) * .28f;
                var v = new Vector3(Mathf.Cos(angle) * speed, vertical, Mathf.Sin(angle) * speed);
                spark.position = sparkOrigin.position + v * t + Vector3.down * (2.4f * t * t);
                spark.rotation = Quaternion.FromToRotation(Vector3.up, v + Vector3.down * 4.8f * t);
                spark.localScale = new Vector3(.025f, .10f, .025f) * Mathf.Lerp(1, .15f, t / duration);
            }
            foreach (Transform wheel in flywheels)
                wheel.localRotation = Quaternion.Euler(0, 0, phase * 360f);
            for (int i = 0; i < castSlugs.Length; i++)
                castSlugs[i].localPosition = new Vector3(.14f, .93f, -.85f - Mathf.Repeat(i * .67f + phase * .41f, 2.4f));
        }

        public void Frame(int framing, float time)
        {
            float glide = .12f * Mathf.Sin(time * .50f);
            Vector3 at;
            if (framing == 0)
            {
                at = new Vector3(0, 2.27f, -.26f);
                view.transform.position = new Vector3(5.0f + glide, 4.27f, -9.25f);
                view.orthographicSize = 4.15f - Mathf.Min(.25f, time * .055f);
            }
            else if (framing == 1)
            {
                at = new Vector3(.02f, 1.80f, -.13f);
                view.transform.position = new Vector3(4.20f, 3.38f, -7.65f);
                view.orthographicSize = 3.06f;
            }
            else
            {
                at = new Vector3(.06f, 1.17f, -.34f);
                view.transform.position = new Vector3(4.0f + glide, 2.28f, -5.32f);
                view.orthographicSize = 2.25f - Mathf.Min(.13f, time * .027f);
            }
            view.transform.rotation = Quaternion.LookRotation(at - view.transform.position, Vector3.up);
        }
    }
}

using UnityEngine;

namespace GameFeelClinic.Brassworks
{
    [DisallowMultipleComponent]
    public sealed class PressImpactAudio : MonoBehaviour
    {
        public enum TimingMode { FixedTimer, AnimationPhase }

        [SerializeField] Animator animator;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip impact;
        [SerializeField, Range(0f, .999f)] float contactPhase = .60f;
        [SerializeField] TimingMode timing = TimingMode.AnimationPhase;
        [SerializeField] string loopState = "Base Layer.Hammer Cycle";

        static readonly int HammerCycle = Animator.StringToHash("Base Layer.Hammer Cycle");
        const float ClipSeconds = 2f;
        bool armed;
        float previous;
        int previousState;
        int previousTimerHit;

        public TimingMode Mode => timing;
        public float ContactPhase => contactPhase;
        public Animator Animator => animator;
        public int LoopHash => string.IsNullOrEmpty(loopState) ? HammerCycle : Animator.StringToHash(loopState);
        public bool Valid => animator && audioSource && impact;
        public bool InTransition => animator.IsInTransition(0);
        public float NormalizedTime => animator.GetCurrentAnimatorStateInfo(0).normalizedTime;

        public void ResetTiming()
        {
            armed = false;
            previousTimerHit = -1;
        }

        public bool Tick(float elapsed, TimingMode mode, bool playAudio)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool crossedPhase = Crossed(state.fullPathHash, animator.IsInTransition(0), state.normalizedTime);
            int timerHit = Mathf.FloorToInt(elapsed / ClipSeconds - contactPhase + 0.000002f);
            bool crossedTimer = timerHit > previousTimerHit;
            previousTimerHit = timerHit;
            bool hit = mode == TimingMode.AnimationPhase ? crossedPhase : crossedTimer;
            if (hit && playAudio && impact && audioSource)
                audioSource.PlayOneShot(impact);
            return hit;
        }

        public bool Crossed(int state, bool transitioning, float now)
        {
            if (transitioning || state != LoopHash)
            {
                armed = false;
                return false;
            }

            if (!armed || state != previousState || now < previous)
            {
                armed = true;
                previousState = state;
                previous = now;
                return false;
            }

            int oldHit = Mathf.FloorToInt(previous - contactPhase);
            int newHit = Mathf.FloorToInt(now - contactPhase);
            previous = now;
            return newHit > oldHit;
        }
    }
}

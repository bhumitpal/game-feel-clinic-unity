using System;
using UnityEngine;

namespace GameFeelClinic.Splitter
{
    [DisallowMultipleComponent]
    public sealed class SplitterRouter : MonoBehaviour
    {
        [SerializeField] private bool allowFallback = true;

        private int preferredLane;

        public bool AllowFallback => allowFallback;

        public void Configure(bool allowFallback)
        {
            this.allowFallback = allowFallback;
        }

        public void ResetRouting(int preferredLane = 0)
        {
            if (preferredLane != 0 && preferredLane != 1)
                throw new ArgumentOutOfRangeException(nameof(preferredLane));

            this.preferredLane = preferredLane;
        }

        public bool TryDispatch(Func<bool> tryAcceptA, Func<bool> tryAcceptB, out int acceptedLane)
        {
            Func<bool> preferred = preferredLane == 0 ? tryAcceptA : tryAcceptB;
            Func<bool> alternate = preferredLane == 0 ? tryAcceptB : tryAcceptA;

            if (preferred())
                acceptedLane = preferredLane;
            else if (allowFallback && alternate())
                acceptedLane = 1 - preferredLane;
            else
            {
                acceptedLane = -1;
                return false;
            }

            preferredLane = 1 - acceptedLane;
            return true;
        }
    }
}
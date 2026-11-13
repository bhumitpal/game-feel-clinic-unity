using UnityEngine;

namespace GameFeelClinic.TrainRoute
{
    [DisallowMultipleComponent]
    public sealed class TrainRouteHandoff : MonoBehaviour
    {
        [SerializeField] private Transform junction;
        [SerializeField] private Transform initialExit;
        [SerializeField] private Transform rerouteExit;
        [SerializeField] private bool commitAtJunction = true;

        private Transform target;
        private Transform pendingExit;

        public Transform Target => target;
        public Transform PendingExit => pendingExit;
        public Transform RerouteExit => rerouteExit;
        public bool CommitAtJunction => commitAtJunction;

        public void Configure(Transform junction, Transform initialExit, Transform rerouteExit, bool commitAtJunction)
        {
            this.junction = junction;
            this.initialExit = initialExit;
            this.rerouteExit = rerouteExit;
            this.commitAtJunction = commitAtJunction;
        }

        public void ResetJourney(Transform origin)
        {
            if (origin == null || junction == null || initialExit == null)
                return;

            transform.position = origin.position;
            target = junction;
            pendingExit = null;
        }

        public void RequestRoute(Transform exit)
        {
            if (target == null || target != junction || exit == null
                || (exit != initialExit && exit != rerouteExit))
                return;

            if (commitAtJunction)
            {
                pendingExit = exit;
                return;
            }

            BeginExit(exit);
        }

        public void Step(float metres)
        {
            if (!(metres > 0f))
                return;

            while (target != null)
            {
                float distance = Vector3.Distance(transform.position, target.position);
                if (distance > metres)
                {
                    transform.position = Vector3.MoveTowards(transform.position, target.position, metres);
                    return;
                }

                transform.position = target.position;
                metres -= distance;
                if (target == junction)
                    BeginExit(pendingExit != null ? pendingExit : initialExit);
                else
                    target = null;
            }
        }

        private void BeginExit(Transform exit)
        {
            transform.position = junction.position;
            target = exit;
            pendingExit = null;
        }
    }
}

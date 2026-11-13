using UnityEngine;

namespace GameFeelClinic.MarbleCamera
{
    [DisallowMultipleComponent]
    public sealed class BankedFollowMarble : MonoBehaviour
    {
        [SerializeField] private Transform ball;
        [SerializeField] private Transform trackFrame;
        [SerializeField] private Vector3 offset = new Vector3(-6f, 2f, -0.55f);
        [SerializeField] private bool useTrackUp = true;

        public void Configure(Transform target, Transform frame)
        {
            ball = target;
            trackFrame = frame;
        }

        public bool UseTrackUp { get => useTrackUp; set => useTrackUp = value; }

        private void LateUpdate() => UpdateView();

        public void UpdateView()
        {
            if (ball == null) return;

            transform.position = ball.position + offset;
            Vector3 up = useTrackUp && trackFrame != null ? trackFrame.up : Vector3.up;
            transform.LookAt(ball, up);
        }
    }
}

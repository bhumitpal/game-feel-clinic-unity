using UnityEngine;

namespace GameFeelClinic.MarbleCamera
{
    public sealed class BankedTrackDemo : MonoBehaviour
    {
        public Transform trackFrame;
        public Transform ball;
        public BankedFollowMarble follow;

        private void Update() => Pose(Mathf.Repeat(Time.timeSinceLevelLoad, 8f));

        public void Pose(float seconds)
        {
            float tilt = Mathf.SmoothStep(0, 52f, (seconds - 0.25f) / 2.0f);
            tilt -= Mathf.SmoothStep(0, 84f, (seconds - 3.65f) / 2.25f);
            tilt += Mathf.SmoothStep(0, 32f, (seconds - 6.55f) / 1.45f);
            trackFrame.localRotation = Quaternion.AngleAxis(tilt, Vector3.right);

            float distance = 0.72f * seconds;
            ball.localPosition = new Vector3(-3.8f + distance, 0.93f, 0f);
            ball.localRotation = Quaternion.AngleAxis(-distance / 0.525f * Mathf.Rad2Deg, Vector3.forward);
        }
    }
}

using UnityEngine;

namespace GameFeelClinic.Floodgate
{
    [ExecuteAlways]
    public sealed class FloodgateDemo : MonoBehaviour
    {
        public Camera view;
        public Transform drone;
        public Transform rover;
        public EdgeWarning warning;
        public Transform[] propellers;
        public Transform beacon;
        public bool playRoute = true;

        void OnEnable() { if (view && drone && rover) Pose(0f, 0); }

        void Update()
        {
            if (Application.isPlaying && playRoute)
                Pose(Mathf.Repeat((float)Time.timeSinceLevelLoadAsDouble, 12f), 0);
        }

        public void Pose(float time, int shot)
        {
            float t = Mathf.Repeat(time, 12f);
            float yaw = t < 4f ? Mathf.SmoothStep(-16f, 15f, t / 4f)
                : Mathf.SmoothStep(15f, -8f, (t - 4f) / 8f);
            Quaternion heading = Quaternion.Euler(0, yaw, 0);
            Vector3 center = new(0, 2.3f, -8.6f);
            Vector3 local = t < 2.7f
                ? Vector3.Lerp(new(12f, 5.6f, 5.4f), new(3.1f, 1.55f, -2.7f), t / 2.7f)
                : t < 5.2f
                    ? Vector3.Lerp(new(3.1f, 1.55f, -2.7f), new(0, 0, -3.4f), Mathf.Clamp01((t - 2.7f) / 2.25f))
                    : t < 8.3f
                        ? Vector3.Lerp(new(0, 0, -3.4f), new(-5.6f, 2.1f, 4.1f), (t - 5.2f) / 3.1f)
                        : Vector3.Lerp(new(-5.6f, 2.1f, 4.1f), new(1.8f, 1.8f, 8.8f), (t - 8.3f) / 3.7f);
            drone.position = center + heading * local;
            drone.rotation = Quaternion.Euler(0, 95 + 28f * Mathf.Sin(t * .64f), 7f * Mathf.Cos(t));
            rover.position = new(0.42f * Mathf.Sin(t * .8f), .2f, -1.1f + .19f * t);
            rover.rotation = Quaternion.Euler(0, 13f, 0);
            for (int index = 0; index < propellers.Length; index++)
                propellers[index].localRotation = Quaternion.Euler(0, (index % 2 == 0 ? 1 : -1) * t * 1260f, 0);
            if (beacon) beacon.localRotation = Quaternion.Euler(0, t * 75f, 0);

            if (shot == 0)
            {
                view.fieldOfView = 48f;
                view.transform.SetPositionAndRotation(center, heading);
            }
            else if (shot == 1)
            {
                view.fieldOfView = 52f;
                view.transform.position = new Vector3(2.4f + .35f * t, 9.8f, -21.5f + .24f * t);
                view.transform.LookAt(new Vector3(0, 2.4f, 2.2f));
            }
            else if (shot == 2)
            {
                view.fieldOfView = 46f;
                view.transform.position = drone.position + new Vector3(4.4f, 2f, -4.6f);
                view.transform.LookAt(drone.position + Vector3.down * .34f);
            }
            else
            {
                view.fieldOfView = 45f;
                view.transform.position = new Vector3(2.5f, 3.1f, -7f + t * .24f);
                view.transform.LookAt(new Vector3(0, 1.45f, 2.7f));
            }
            warning.Refresh();
        }
    }
}

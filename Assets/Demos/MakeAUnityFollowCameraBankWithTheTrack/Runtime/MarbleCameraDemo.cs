using UnityEngine;

namespace GameFeelClinic.MarbleCamera
{
    public sealed class MarbleCameraDemo : MonoBehaviour
    {
        public Camera shotCamera;
        public Transform marble;
        public Transform cameraMarker;
        public Transform worldUpMarker;
        public LineRenderer sightLine;
        public Transform[] ornaments;
        public Transform checkpointLight;
        public bool autoplay = true;

        private const float Radius = .525f;
        private const float StartX = -4.6f;
        private const float RollSpeed = .42f;
        private static readonly Vector3 FollowOffset = new Vector3(0, .7f, -6.7f);

        private void Start() => Pose("wide", 0);

        private void Update()
        {
            if (autoplay) Pose("wide", Mathf.Repeat(Time.timeSinceLevelLoad, 7));
        }

        public void Pose(string shot, float t)
        {
            float x = StartX + RollSpeed * t;
            if (shot == "wide") x = -5.6f + t * 1.15f;
            if (shot == "arch") x = 1.45f + t * .90f;
            if (shot == "payoff") x = 1.9f + t * .88f;

            marble.position = new Vector3(x, .93f, 0);
            marble.rotation = Quaternion.AngleAxis(-(x - StartX) / Radius * Mathf.Rad2Deg, Vector3.forward);
            cameraMarker.gameObject.SetActive(shot == "fix");
            worldUpMarker.gameObject.SetActive(shot == "fix");
            sightLine.gameObject.SetActive(shot == "fix");
            for (int i = 0; i < ornaments.Length; i++)
                ornaments[i].localRotation = Quaternion.Euler(0, i * 33 + t * 22, 0);
            checkpointLight.localScale = Vector3.one * (1 + .10f * Mathf.Sin(t * 4));

            Transform rig = shotCamera.transform;
            if (shot == "attached")
            {
                if (rig.parent != marble) rig.SetParent(marble, false);
                rig.localPosition = FollowOffset;
                rig.localRotation = Quaternion.LookRotation(-FollowOffset, Vector3.up);
                shotCamera.fieldOfView = 45;
                return;
            }

            if (rig.parent != null) rig.SetParent(null, true);
            switch (shot)
            {
                case "independent":
                    rig.position = marble.position + FollowOffset;
                    rig.rotation = Quaternion.LookRotation(marble.position - rig.position, Vector3.up);
                    shotCamera.fieldOfView = 45;
                    break;
                case "marble":
                    Aim(marble.position + new Vector3(1.6f, 1.05f, -2.6f), marble.position + Vector3.up * .08f, 43);
                    break;
                case "fix":
                    Vector3 marker = marble.position + new Vector3(0, 1.05f, -3.1f);
                    cameraMarker.SetPositionAndRotation(marker, Quaternion.LookRotation(marble.position - marker, Vector3.up));
                    worldUpMarker.position = marker + new Vector3(.72f, 0, 0);
                    sightLine.SetPosition(0, marker + Vector3.forward * .45f);
                    sightLine.SetPosition(1, marble.position - Vector3.forward * .55f);
                    Aim(marble.position + new Vector3(3.2f, 5.35f, -5.7f), marble.position + new Vector3(0, .25f, -1.58f), 54);
                    break;
                case "arch":
                    Aim(new Vector3(9.1f - t * .08f, 3.25f, -6.25f), new Vector3(4.75f, 1.85f, 0), 47);
                    break;
                case "payoff":
                    Aim(new Vector3(-22.0f - t * .32f, 13.0f, -8.7f), new Vector3(-.35f, .75f, .05f), 59);
                    break;
                default:
                    Aim(new Vector3(23f - t * .16f, 13.2f - t * .21f, -8.3f - t * .08f), new Vector3(.42f, .85f, .05f), 59);
                    break;
            }
        }

        private void Aim(Vector3 position, Vector3 target, float fieldOfView)
        {
            shotCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
            shotCamera.fieldOfView = fieldOfView;
        }
    }
}

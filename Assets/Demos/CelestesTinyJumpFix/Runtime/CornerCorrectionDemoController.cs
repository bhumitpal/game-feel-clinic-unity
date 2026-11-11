using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

namespace GameFeelClinic.CornerCorrection
{
    public sealed class CornerCorrectionDemoController : MonoBehaviour
    {
        public Texture2D Backdrop;
        public string Shot = "keep";
        public bool AutoPreview = true;
        public CornerCorrectionController Controller;
        [SerializeField] Transform cameraAndLightingRoot;
        [SerializeField] Transform environmentRoot;

        Camera filmCamera;
        CinemachineCamera rig;
        Transform focus;
        Transform explorer;
        Transform scarf;
        Transform head;
        Transform probe;
        Transform contact;
        Transform ceiling;
        Transform trail;
        Transform activeBounds;
        Transform backdropQuad;
        RenderTexture target;
        Material unlit;
        Material lineMat;
        readonly List<Transform> dust = new List<Transform>();
        int previewFrame;
        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static readonly Color Ink = C("#142936"), Rock = C("#405B6B"), Edge = C("#6D8D9C"), Snow = C("#E7F3F5"), Red = C("#EF664F"), Green = C("#61D5AF"), Bad = C("#FF8B7F");

        void Awake()
        {
            BuildWorld();
            ResetShot("keep");
        }

        public void BuildWorld()
        {
            unlit = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            unlit.name = "Alpine palette unlit";
            lineMat = new Material(Shader.Find("Sprites/Default"));
            lineMat.name = "Alpine debug lines";
            var camGO = CreateObject("Portrait Camera", cameraAndLightingRoot);
            filmCamera = camGO.AddComponent<Camera>();
            filmCamera.orthographic = true; filmCamera.orthographicSize = 4.8f;
            filmCamera.backgroundColor = C("#213443"); filmCamera.clearFlags = CameraClearFlags.SolidColor;
            filmCamera.transform.position = new Vector3(.3f, 2, -10);
            filmCamera.aspect = 9f / 16f;
            camGO.AddComponent<CinemachineBrain>();
            var previewCamera = CreateObject("Game View Camera", cameraAndLightingRoot).AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor; previewCamera.backgroundColor = Ink; previewCamera.cullingMask = 0;
            target = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32) { name = "Portrait 1080x1920" };
            target.Create(); filmCamera.targetTexture = target;
            focus = CreateObject("Camera Focus", cameraAndLightingRoot).transform;
            focus.position = new Vector3(.3f, 2, 0);
            rig = CreateObject("Cinemachine Rig", cameraAndLightingRoot).AddComponent<CinemachineCamera>();
            rig.Follow = focus;
            rig.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            rig.Lens.OrthographicSize = 4.8f;
            var composer = rig.gameObject.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = 10; composer.Damping = Vector3.zero;
            composer.Composition.DeadZone.Size = new Vector2(.02f, .02f);

            if (Backdrop != null)
            {
                var bg = Quad("Original alpine atmosphere", Vector2.zero, Vector2.one, Color.white, 9);
                var mat = new Material(unlit); mat.SetTexture("_BaseMap", Backdrop); mat.SetColor("_BaseColor", Color.white);
                bg.GetComponent<MeshRenderer>().sharedMaterial = mat;
                backdropQuad = bg.transform; backdropQuad.SetParent(filmCamera.transform, false); backdropQuad.localPosition = new Vector3(0, 0, 19);
            }
            RockMass("Snow floor", new Vector2(-7, -4), new Vector2(14, 4), true, 0);
            ceiling = RockMass("Ceiling corner", new Vector2(-5, 2), new Vector2(5, 5.5f), false, 0);

            Polygon("Left foreground slate", new[] { new Vector2(-7, -4), new Vector2(-3.2f, -4), new Vector2(-3.7f, -2.1f), new Vector2(-4.4f, -1.5f), new Vector2(-4.8f, .1f), new Vector2(-6, .8f) }, C("#243B4A"), -1);
            Polygon("Right foreground slate", new[] { new Vector2(3, -4), new Vector2(7, -4), new Vector2(7, 1.5f), new Vector2(5.3f, 1.0f), new Vector2(4.8f, -.4f), new Vector2(3.7f, -1.3f) }, C("#263E4C"), -1);

            var body = CreateObject("Explorer", environmentRoot);
            Controller = body.AddComponent<CornerCorrectionController>();
            var collider = body.AddComponent<BoxCollider2D>(); collider.size = Controller.ColliderSize; collider.isTrigger = true;
            explorer = body.transform;

            Shape("Backpack", -.39f, -.07f, .32f, .62f, C("#354756"), -.05f);
            Shape("Backpack edge", -.47f, -.03f, .06f, .45f, C("#8CA6B5"), -.07f);
            Shape("Left boot", -.23f, -.62f, .30f, .23f, Ink, -.12f);
            Shape("Right boot", .22f, -.62f, .34f, .23f, Ink, -.14f);
            Shape("Trouser left", -.21f, -.40f, .22f, .43f, C("#2B4255"), -.12f);
            Shape("Trouser right", .19f, -.39f, .22f, .44f, C("#314D61"), -.14f);
            Shape("Jacket outline", -.01f, -.09f, .77f, .75f, Ink, -.17f);
            Shape("Red climbing jacket", -.01f, -.06f, .67f, .65f, Red, -.19f);
            Shape("Jacket light", .20f, -.02f, .16f, .47f, C("#F48265"), -.21f);
            Shape("Jacket zipper", .02f, -.12f, .035f, .52f, C("#773F42"), -.24f);
            Shape("Glove", .40f, -.05f, .22f, .24f, Ink, -.26f).localRotation = Quaternion.Euler(0, 0, 18);
            head = new GameObject("Head pose").transform; head.SetParent(explorer, false);
            head.localScale = Vector3.one * .94f;
            var skull = Shape("Hair silhouette", .02f, .44f, .74f, .61f, Ink, -.25f); skull.SetParent(head, false);
            var face = Shape("Face", .10f, .40f, .57f, .43f, C("#F5CEAC"), -.28f); face.SetParent(head, false);
            var hair = Shape("Dark windswept hair", -.05f, .66f, .70f, .17f, C("#533B43"), -.30f); hair.SetParent(head, false);
            var fringe = Shape("Hair fringe", -.22f, .51f, .19f, .31f, C("#533B43"), -.32f); fringe.SetParent(head, false);
            var nose = Shape("Nose", .40f, .37f, .12f, .10f, C("#E5A98E"), -.31f); nose.SetParent(head, false);
            var eye = Shape("Eye", .26f, .46f, .055f, .08f, Ink, -.34f); eye.SetParent(head, false);
            scarf = Shape("Scarf tail", -.42f, .14f, .58f, .16f, C("#F8C679"), -.35f);
            Shape("Scarf knot", .01f, .16f, .69f, .14f, C("#F8C679"), -.37f);

            contact = CreateObject("Contact spark", environmentRoot).transform;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                var ray = Quad("contact ray", Vector2.zero, new Vector2(.065f, .20f), Bad, -.6f);
                ray.transform.SetParent(contact, false); ray.transform.localPosition = new Vector3(Mathf.Cos(a) * .23f, Mathf.Sin(a) * .23f, 0);
                ray.transform.localRotation = Quaternion.Euler(0, 0, -i * 45);
            }
            probe = CreateObject("Full collider candidate", environmentRoot).transform;
            trail = CreateObject("Preserved ascent trail", environmentRoot).transform;
            activeBounds = CreateObject("Active full collider", environmentRoot).transform;
            WireBox(probe, Controller.ColliderSize, Green, .024f);
            WireBox(trail, Controller.ColliderSize, Red, .014f);
            WireBox(activeBounds, Controller.ColliderSize, new Color(Snow.r, Snow.g, Snow.b, .62f), .012f);
            for (int i = 0; i < 38; i++)
            {
                var q = Quad("snow mote", new Vector2(-5 + (i * 1.571f) % 10, -2 + (i * .937f) % 10), Vector2.one * (.018f + (i % 3) * .008f), C("#BDD6DF"), 2);
                dust.Add(q.transform);
            }
            RenderSettings.skybox = null;
        }

        static GameObject CreateObject(string name, Transform parent)
        {
            var result = new GameObject(name);
            result.transform.SetParent(parent, false);
            return result;
        }

        Transform Shape(string name, float x, float y, float w, float h, Color c, float z)
        {
            var q = Quad(name, Vector2.zero, new Vector2(w, h), c, z).transform;
            q.SetParent(explorer, false); q.localPosition = new Vector3(x, y, z); return q;
        }
        GameObject Quad(string name, Vector2 center, Vector2 size, Color color, float z)
        {
            var g = CreateObject(name, environmentRoot); var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-size.x / 2, -size.y / 2), new Vector3(size.x / 2, -size.y / 2), new Vector3(size.x / 2, size.y / 2), new Vector3(-size.x / 2, size.y / 2) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }; mesh.RecalculateNormals();
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mat = new Material(unlit); mat.SetColor("_BaseColor", color); mat.name = "Alpine " + name;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat; g.transform.position = new Vector3(center.x, center.y, z); return g;
        }
        void Polygon(string name, Vector2[] points, Color c, float z)
        {
            var g = CreateObject(name, environmentRoot); var m = new Mesh(); var v = new Vector3[points.Length];
            for (int i = 0; i < v.Length; i++) v[i] = points[i]; var t = new List<int>(); for (int i = 1; i < v.Length - 1; i++) { t.Add(0); t.Add(i + 1); t.Add(i); }
            m.vertices = v; m.triangles = t.ToArray(); m.RecalculateNormals();
            g.AddComponent<MeshFilter>().sharedMesh = m; var mat = new Material(unlit); mat.SetColor("_BaseColor", c); mat.name = "Alpine " + name; g.AddComponent<MeshRenderer>().sharedMaterial = mat; g.transform.position = new Vector3(0, 0, z);
        }
        Transform RockMass(string name, Vector2 min, Vector2 size, bool topSnow, float z)
        {
            var root = CreateObject(name, environmentRoot); root.layer = 8;
            var col = root.AddComponent<BoxCollider2D>(); col.offset = min + size / 2; col.size = size;
            var main = Quad(name + " outline", min + size / 2, size, Ink, z); main.transform.SetParent(root.transform, true);
            var inside = Quad(name + " slate", min + size / 2 + new Vector2(-.025f, .035f), size - new Vector2(.07f, .07f), Rock, z - .01f); inside.transform.SetParent(root.transform, true);
            for (int i = 0; i < 25; i++)
            {
                float x = min.x + .2f + (i * .791f) % (size.x - .4f), y = min.y + .15f + (i * .413f) % (size.y - .3f);
                float w = .24f + (i % 5) * .17f;
                var seam = Quad("slate stratum", new Vector2(x, y), new Vector2(w, .027f), i % 2 == 0 ? Edge : C("#314C5C"), z - .025f); seam.transform.SetParent(root.transform, true);
            }
            float edgeY = topSnow ? min.y + size.y : min.y;
            var trim = Quad(topSnow ? "snow shelf" : "corner rim", new Vector2(min.x + size.x / 2, edgeY + .045f), new Vector2(size.x, .09f), topSnow ? Snow : Edge, z - .04f); trim.transform.SetParent(root.transform, true);
            if (topSnow) for (int i = 0; i < 12; i++) { var snow = Quad("broken snow lip", new Vector2(min.x + i * size.x / 12, edgeY - .04f), new Vector2(.37f, .08f + (i % 3) * .025f), Snow, z - .05f); snow.transform.SetParent(root.transform, true); }
            return root.transform;
        }
        void WireBox(Transform parent, Vector2 size, Color c, float width)
        {
            var g = new GameObject("collider boundary"); g.transform.SetParent(parent, false);
            var line = g.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.loop = true; line.positionCount = 4; line.widthMultiplier = width; line.sharedMaterial = lineMat; line.startColor = line.endColor = c;
            line.SetPositions(new[] { new Vector3(-size.x / 2, -size.y / 2, -.7f), new Vector3(-size.x / 2, size.y / 2, -.7f), new Vector3(size.x / 2, size.y / 2, -.7f), new Vector3(size.x / 2, -size.y / 2, -.7f) });
        }

        public void ResetShot(string shot)
        {
            Shot = shot; previewFrame = 0; Controller.ResetJump(shot != "problem");
            ceiling.localScale = new Vector3(shot == "limit" ? 1.6f : 1, 1, 1);
            if (shot == "limit") ceiling.position = new Vector3(2, 0, 0); else ceiling.position = Vector3.zero;
            Physics2D.SyncTransforms();
            probe.gameObject.SetActive(false); trail.gameObject.SetActive(false); contact.gameObject.SetActive(false); activeBounds.gameObject.SetActive(false);
        }

        void Update()
        {
            if (AutoPreview) { ApplyFrame(previewFrame++); if (previewFrame >= 180) ResetShot(Shot); }
        }
        public void ApplyFrame(int frame)
        {
            float t = frame / 30f;
            if (Shot == "probe")
            {
                Controller.Position = new Vector2(.347f, 1.238f); Controller.Velocity = new Vector2(.02f, 4.8f);
                int px = frame < 90 ? 1 : 2; Vector2 candidate = Controller.Position + new Vector2(px * Controller.Pixel, Controller.Pixel);
                probe.position = candidate; probe.gameObject.SetActive(frame > 30);
                var line = probe.GetComponentInChildren<LineRenderer>(); line.startColor = line.endColor = Controller.IsClear(candidate) ? Green : Bad;
            }
            else
            {
                int end = (Shot == "keep" || Shot == "payoff") ? 84 : 132;
                if (frame >= 30 && frame <= end) Controller.Step(1f / 90f);
            }
            bool hit = Controller.CeilingHits > 0;
            float contactAlpha = hit && frame < 91 ? 1 : 0;
            contact.gameObject.SetActive(contactAlpha > 0 && Controller.Corrections == 0);
            contact.position = new Vector3(0, 2, -.7f); contact.localScale = Vector3.one * (.8f + .15f * Mathf.Sin(t * 20));

            trail.gameObject.SetActive(false);
            activeBounds.position = Controller.Position;
            activeBounds.gameObject.SetActive(Shot == "probe" || (Shot == "problem" && frame >= 35 && frame <= 52));
            float bob = Mathf.Sin(t * 4) * .025f;
            head.localRotation = Quaternion.Euler(0, 0, Controller.Velocity.y > 0 ? -4 : 2);
            scarf.localRotation = Quaternion.Euler(0, 0, -12 + Mathf.Sin(t * 6) * 8);
            float blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .8f) / 1.0f));
            float size = Shot == "probe" ? 2.15f : Mathf.Lerp(4.8f, 3.15f, blend);
            if (Shot == "payoff") size = Mathf.Lerp(3.8f, 4.3f, Mathf.Clamp01(t / 6));
            rig.Lens.OrthographicSize = size;
            focus.position = new Vector3(Shot == "probe" ? .35f : .35f, Shot == "probe" ? 1.8f : Mathf.Lerp(2, 2.05f, blend), 0);
            if (backdropQuad != null)
            {
                float h = size * 2, w = h * 9f / 16f, ar = (float)Backdrop.width / Backdrop.height;
                if (ar > 9f / 16f) w = h * ar; else h = w / ar;
                backdropQuad.localScale = new Vector3(w, h, 1);
            }
            for (int i = 0; i < dust.Count; i++) dust[i].position = new Vector3(-5 + (i * 1.571f + t * .04f) % 10, -2 + (i * .937f - t * .08f + 20) % 10, 2);
        }

        void OnGUI()
        {
            if (target == null) return;
            float h = Screen.height, w = h * 9f / 16f;
            if (w > Screen.width) { w = Screen.width; h = w * 16f / 9f; }
            GUI.DrawTexture(new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h), target, ScaleMode.StretchToFill, false);
        }
        [ContextMenu("Preview correction off")]
        public void PreviewOff() { AutoPreview = true; ResetShot("problem"); }
        [ContextMenu("Preview correction on")]
        public void PreviewOn() { AutoPreview = true; ResetShot("keep"); }
        void OnDestroy() { if (target != null) { target.Release(); Destroy(target); } }
    }
}

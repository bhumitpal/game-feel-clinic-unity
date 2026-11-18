using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameFeelClinic.PropDrop
{
    /// <summary>
    /// Drops the selected props with physics in the Editor, without Play mode.
    /// Steps the physics scene by hand, moves only the selection and supports Undo.
    /// </summary>
    public static class PropDropper
    {
        const int MinSteps = 20, MaxSteps = 1200, StepsPerEditorUpdate = 2;

        public static bool ProtectOthers = true;
        public static bool IsRunning => session != null;
        public static int StepCount => session?.steps ?? 0;

        sealed class Session
        {
            public SimulationMode oldMode;
            public readonly List<Rigidbody> selected = new List<Rigidbody>();
            public readonly List<Object> temporary = new List<Object>();
            public readonly List<Rigidbody> frozen = new List<Rigidbody>();
            public int steps;
        }

        static Session session;

        [MenuItem("Tools/Drop Selected With Physics")]
        public static void DropSelected()
        {
            if (Selection.gameObjects.Length == 0 || IsRunning) return;
            Begin(Selection.gameObjects, ProtectOthers);
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (Step(StepsPerEditorUpdate)) { SceneView.RepaintAll(); return; }
            EditorApplication.update -= Tick;
            End();
        }

        public static void Begin(GameObject[] props, bool protectOthers)
        {
            if (IsRunning) End();
            var s = new Session { oldMode = Physics.simulationMode };
            var chosen = new HashSet<GameObject>(props);
            var others = new List<Rigidbody>();
            foreach (var body in Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                if (!chosen.Contains(body.gameObject)) others.Add(body);

            // One Undo step restores every transform physics may move. The drop runs over many
            // editor frames, so store the complete state now instead of diffing at the end of this one.
            var movable = new List<Object>();
            foreach (var go in props) movable.Add(go.transform);
            foreach (var body in others) movable.Add(body.transform);
            Undo.RegisterCompleteObjectUndo(movable.ToArray(), "Drop props with physics");

            // Everything that is not selected holds still while we step.
            if (protectOthers)
                foreach (var body in others)
                    if (!body.isKinematic) { body.isKinematic = true; s.frozen.Add(body); }

            foreach (var go in props)
            {
                if (go.GetComponentInChildren<Collider>() == null)
                {
                    var hull = go.AddComponent<MeshCollider>();
                    hull.sharedMesh = go.GetComponentInChildren<MeshFilter>().sharedMesh;
                    hull.convex = true;
                    s.temporary.Add(hull);
                }
                if (!go.TryGetComponent(out Rigidbody body))
                {
                    body = go.AddComponent<Rigidbody>();
                    s.temporary.Add(body);
                }
                body.isKinematic = false;
                body.maxDepenetrationVelocity = 2f;   // overlapping props ease apart instead of popping
                s.selected.Add(body);
            }

            Physics.simulationMode = SimulationMode.Script;   // we own the physics clock now
            Physics.SyncTransforms();
            session = s;
        }

        /// <summary>Advances physics. Returns true while any selected prop is still moving.</summary>
        public static bool Step(int count)
        {
            if (session == null) return false;
            for (int i = 0; i < count; i++)
            {
                Physics.Simulate(Time.fixedDeltaTime);
                session.steps++;
            }
            if (session.steps < MinSteps) return true;
            if (session.steps >= MaxSteps) return false;
            foreach (var body in session.selected)
                if (!body.IsSleeping()) return true;
            return false;
        }

        public static void End()
        {
            if (session == null) return;
            foreach (var body in session.frozen) if (body) body.isKinematic = false;
            foreach (var extra in session.temporary) if (extra) Object.DestroyImmediate(extra);
            Physics.simulationMode = session.oldMode;
            session = null;
            EditorSceneManager.MarkAllScenesDirty();
        }
    }
}

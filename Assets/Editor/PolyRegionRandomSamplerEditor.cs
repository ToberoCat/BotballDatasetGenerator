#if UNITY_EDITOR
using Randomizer;
using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(PolyRegionRandomSampler))]
public class PolyRegionRandomSamplerEditor : Editor
{
    PolyRegionRandomSampler S => (PolyRegionRandomSampler)target;
    const float HANDLE_PIXELS = 10f;
    const float LINE_HANDLE_DISTANCE = 8f;

    private bool showVertexControls = true;
    private bool showControls = true;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space(10);

        showVertexControls = EditorGUILayout.Foldout(showVertexControls, "Vertex Controls", true);
        if (showVertexControls)
        {
            EditorGUILayout.HelpBox(
                "In Scene View:\nSHIFT+Click: Add vertex at cursor\nALT+Click on vertex: Delete vertex\nCTRL+Click on line: Insert vertex",
                MessageType.Info);

            if (GUILayout.Button("Clear All Vertices", GUILayout.Height(25)))
            {
                Undo.RecordObject(S, "Clear Vertices");
                S.vertices.Clear();
                EditorUtility.SetDirty(S);
            }

            if (GUILayout.Button("Create Default Square", GUILayout.Height(25)))
            {
                Undo.RecordObject(S, "Create Default Square");
                S.vertices.Clear();
                S.vertices.Add(new Vector2(-2, -1));
                S.vertices.Add(new Vector2(2, -1));
                S.vertices.Add(new Vector2(2, 1));
                S.vertices.Add(new Vector2(-2, 1));
                EditorUtility.SetDirty(S);
            }
        }

        EditorGUILayout.Space(5);

        showControls = EditorGUILayout.Foldout(showControls, "Sample Controls", true);
        if (showControls)
        {
            if (GUILayout.Button("Generate Random Sample", GUILayout.Height(30)))
            {
                Undo.RecordObject(S, "Generate Sample");
                S.SampleWorldSpace();
                EditorUtility.SetDirty(S);
                SceneView.RepaintAll();
            }

            if (S.currentSample != Vector3.zero)
            {
                EditorGUILayout.Space(5);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Vector3Field("Current Sample", S.currentSample);
                EditorGUI.EndDisabledGroup();
            }
        }
    }

    void OnSceneGUI()
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 250, 60));
        GUILayout.Label("SHIFT+Click: Add vertex\nALT+Click: Delete vertex\nCTRL+Click on line: Insert vertex",
            new GUIStyle(EditorStyles.helpBox) { fontSize = 12, normal = { textColor = Color.white } });
        GUILayout.EndArea();
        Handles.EndGUI();

        for (int i = 0; i < S.vertices.Count; i++)
        {
            Vector3 world = S.transform.TransformPoint(
                new Vector3(S.vertices[i].x, 0, S.vertices[i].y));

            float size = HandleUtility.GetHandleSize(world) * 0.08f;
            Handles.color = Color.magenta;
            EditorGUI.BeginChangeCheck();
            Vector3 newWorld = Handles.FreeMoveHandle(
                world, size, Vector3.zero, Handles.DotHandleCap);

            if (Event.current.alt && Event.current.type == EventType.MouseDown &&
                (Event.current.button == 0) &&
                (Vector2.Distance(Event.current.mousePosition,
                    HandleUtility.WorldToGUIPoint(world)) < HANDLE_PIXELS))
            {
                Undo.RecordObject(S, "Delete Vertex");
                S.vertices.RemoveAt(i);
                EditorUtility.SetDirty(S);
                Event.current.Use();
                return;
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(S, "Move Vertex");
                Vector3 local = S.transform.InverseTransformPoint(newWorld);
                S.vertices[i] = new Vector2(local.x, local.z);
                EditorUtility.SetDirty(S);
            }
        }

        HandleLineClicks();
        HandleAddPoint();
        if (GUI.changed) SceneView.RepaintAll();
    }

    void HandleLineClicks()
    {
        if (S.vertices.Count < 2) return;

        Event e = Event.current;
        if (e.control && e.type == EventType.MouseDown && e.button == 0)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Plane plane = new Plane(S.transform.up, S.transform.position);

            if (plane.Raycast(ray, out float hitDst))
            {
                Vector3 hitPoint = ray.GetPoint(hitDst);
                Vector3 localHit = S.transform.InverseTransformPoint(hitPoint);
                Vector2 localHit2D = new Vector2(localHit.x, localHit.z);

                int closestLineIndex = -1;
                float closestDistance = float.MaxValue;
                Vector2 projectedPoint = Vector2.zero;

                for (int i = 0; i < S.vertices.Count; i++)
                {
                    Vector2 a = S.vertices[i];
                    Vector2 b = S.vertices[(i + 1) % S.vertices.Count];

                    Vector2 lineVec = b - a;
                    float lineLength = lineVec.magnitude;
                    Vector2 lineDir = lineVec / lineLength;

                    float projectionLength = Vector2.Dot(localHit2D - a, lineDir);
                    projectionLength = Mathf.Clamp(projectionLength, 0, lineLength);

                    Vector2 projected = a + lineDir * projectionLength;
                    float distance = Vector2.Distance(projected, localHit2D);

                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestLineIndex = i;
                        projectedPoint = projected;
                    }
                }

                Vector3 worldProjected = S.transform.TransformPoint(new Vector3(projectedPoint.x, 0, projectedPoint.y));
                float screenDistance = Vector2.Distance(
                    e.mousePosition,
                    HandleUtility.WorldToGUIPoint(worldProjected)
                );

                if (closestLineIndex >= 0 && screenDistance < LINE_HANDLE_DISTANCE)
                {
                    Undo.RecordObject(S, "Insert Vertex");
                    S.vertices.Insert(closestLineIndex + 1, projectedPoint);
                    EditorUtility.SetDirty(S);
                    e.Use();
                }
            }
        }

        if (S.vertices.Count >= 2 && e.control && e.type == EventType.Repaint)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Plane plane = new Plane(S.transform.up, S.transform.position);

            if (plane.Raycast(ray, out float hitDst))
            {
                Vector3 hitPoint = ray.GetPoint(hitDst);
                Vector3 localHit = S.transform.InverseTransformPoint(hitPoint);
                Vector2 localHit2D = new Vector2(localHit.x, localHit.z);

                int closestLineIndex = -1;
                float closestDistance = float.MaxValue;
                Vector2 projectedPoint = Vector2.zero;

                for (int i = 0; i < S.vertices.Count; i++)
                {
                    Vector2 a = S.vertices[i];
                    Vector2 b = S.vertices[(i + 1) % S.vertices.Count];

                    Vector2 lineVec = b - a;
                    float lineLength = lineVec.magnitude;
                    Vector2 lineDir = lineVec / lineLength;

                    float projectionLength = Vector2.Dot(localHit2D - a, lineDir);
                    projectionLength = Mathf.Clamp(projectionLength, 0, lineLength);

                    Vector2 projected = a + lineDir * projectionLength;
                    float distance = Vector2.Distance(projected, localHit2D);

                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestLineIndex = i;
                        projectedPoint = projected;
                    }
                }

                Vector3 worldProjected = S.transform.TransformPoint(new Vector3(projectedPoint.x, 0, projectedPoint.y));
                float screenDistance = Vector2.Distance(
                    e.mousePosition,
                    HandleUtility.WorldToGUIPoint(worldProjected)
                );

                if (closestLineIndex >= 0 && screenDistance < LINE_HANDLE_DISTANCE)
                {
                    Handles.color = Color.green;
                    float handleSize = HandleUtility.GetHandleSize(worldProjected) * 0.1f;
                    Handles.SphereHandleCap(0, worldProjected, Quaternion.identity, handleSize, EventType.Repaint);
                }
            }
        }
    }

    void HandleAddPoint()
    {
        Event e = Event.current;
        if (e.shift && e.type == EventType.MouseDown && e.button == 0)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Plane plane = new Plane(S.transform.up, S.transform.position);
            if (plane.Raycast(ray, out float hitDst))
            {
                Vector3 hit = ray.GetPoint(hitDst);
                Vector3 local3 = S.transform.InverseTransformPoint(hit);
                Vector2 local2 = new Vector2(local3.x, local3.z);

                Undo.RecordObject(S, "Add Vertex");
                S.vertices.Add(local2);
                EditorUtility.SetDirty(S);
            }

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            e.Use();
        }

        if (e.shift && e.type == EventType.Repaint)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Plane plane = new Plane(S.transform.up, S.transform.position);
            if (plane.Raycast(ray, out float hitDst))
            {
                Vector3 hit = ray.GetPoint(hitDst);
                Handles.color = Color.green;
                float handleSize = HandleUtility.GetHandleSize(hit) * 0.1f;
                Handles.SphereHandleCap(0, hit, Quaternion.identity, handleSize, EventType.Repaint);
            }
        }
    }
}
#endif
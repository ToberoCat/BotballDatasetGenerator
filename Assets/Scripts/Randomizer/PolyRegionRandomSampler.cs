using System;
using System.Collections.Generic;
using Shapes;
using UnityEngine;
using Random = UnityEngine.Random;

// ToDo: Use poison disk sampling for better distribution; Allow passing the amount of points needed and their radius to make sure they don't overlap

namespace Randomizer
{
    public class PolyRegionRandomSampler : MonoBehaviour
    {
        [Tooltip("Region in local XZ-plane, Y = 0")]
        public List<Vector2> vertices = new()
        {
            new Vector2(-2, -1), new Vector2(2, -1),
            new Vector2(1, 2), new Vector2(-1, 2)
        };

        [HideInInspector] public Vector3 currentSample;
        [SerializeField] private bool showSample = true;

        [Range(0, 200)] public int previewSamples = 50;

        struct Triangle
        {
            public Vector2 a, b, c;
            public float area;

            public Triangle(Vector2 a, Vector2 b, Vector2 c)
            {
                this.a = a;
                this.b = b;
                this.c = c;
                area = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
            }

            public Vector2 RandomPoint()
            {
                float r1 = Mathf.Sqrt(Random.value);
                float r2 = Random.value;
                return (1 - r1) * a + r1 * (1 - r2) * b + r1 * r2 * c; // barycentric
            }
        }

        List<Triangle> tris;
        float[] cumArea; // CDF

        void Awake() => Precompute();
        void OnValidate() => Precompute();

        public Vector3 SampleWorldSpace()
        {
            if (tris == null || tris.Count == 0) Precompute();

            if (tris.Count == 0)
            {
                Debug.LogWarning("Triangulation failed, no valid region to sample from");
                return transform.position;
            }

            float pick = Random.value * cumArea[^1];
            int i = Array.BinarySearch(cumArea, pick);
            if (i < 0) i = ~i;
            Vector2 local = tris[i].RandomPoint();

            int maxAttempts = 5;
            int attempts = 0;

            while (!IsPointInPolygon(local, vertices) && attempts < maxAttempts)
            {
                pick = Random.value * cumArea[^1];
                i = Array.BinarySearch(cumArea, pick);
                if (i < 0) i = ~i;
                local = tris[i].RandomPoint();
                attempts++;
            }

            if (attempts >= maxAttempts && !IsPointInPolygon(local, vertices))
            {
                Debug.LogWarning("Failed to find a point inside the polygon after multiple attempts");
                if (tris.Count > 0)
                {
                    local = tris[0].a;
                }
            }

            currentSample = transform.TransformPoint(new Vector3(local.x, 0, local.y));
            return currentSample;
        }

        private bool IsPointInPolygon(Vector2 point, List<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3) return false;

            bool isInside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                if (((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
                    (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) +
                        polygon[i].x))
                {
                    isInside = !isInside;
                }
            }

            return isInside;
        }

        void OnDrawGizmos()
        {
            if (vertices == null || vertices.Count < 2) return;
            using (Draw.Command(Camera.current))
            {
                Draw.ResetAllDrawStates();
                Draw.LineGeometry = LineGeometry.Volumetric3D;
                Draw.ThicknessSpace = ThicknessSpace.Pixels;
                Draw.Thickness = 2;
                Draw.Color = Color.cyan;
                for (int i = 0; i < vertices.Count; i++)
                {
                    Vector3 p0 = transform.TransformPoint(new Vector3(vertices[i].x, 0, vertices[i].y));
                    Vector3 p1 = transform.TransformPoint(new Vector3(vertices[(i + 1) % vertices.Count].x, 0,
                        vertices[(i + 1) % vertices.Count].y));
                    Draw.Line(p0, p1);
                }

                if (showSample && currentSample != Vector3.zero)
                {
                    Draw.Color = Color.yellow;
                    Draw.Sphere(currentSample, 0.05f);
                }
            }
        }

        void Precompute()
        {
            if (vertices == null || vertices.Count < 3)
            {
                tris = new List<Triangle>();
                cumArea = new float[0];
                return;
            }

            tris = EarClip(vertices);

            if (tris.Count == 0)
            {
                Debug.LogWarning("Ear clipping triangulation failed");
                cumArea = new float[0];
                return;
            }

            cumArea = new float[tris.Count];
            float acc = 0;
            for (int i = 0; i < tris.Count; i++)
            {
                acc += tris[i].area;
                cumArea[i] = acc;
            }
        }

        static List<Triangle> EarClip(List<Vector2> poly)
        {
            if (poly.Count < 3) return new List<Triangle>();

            List<Vector2> v = new(poly);
            if (SignedArea(v) > 0) v.Reverse();
            List<Triangle> t = new();

            int safetyCounter = 0;
            int maxIterations = poly.Count * 2;

            while (v.Count > 3 && safetyCounter < maxIterations)
            {
                safetyCounter++;
                bool ear = false;
                for (int i = 0; i < v.Count; i++)
                {
                    Vector2 a = v[(i - 1 + v.Count) % v.Count],
                        b = v[i],
                        c = v[(i + 1) % v.Count];
                    if (Convex(a, b, c) && HoleFree(a, b, c, v))
                    {
                        t.Add(new Triangle(a, b, c));
                        v.RemoveAt(i);
                        ear = true;
                        break;
                    }
                }

                if (!ear) break;
            }

            if (v.Count == 3) t.Add(new Triangle(v[0], v[1], v[2]));
            return t;
        }

        static bool Convex(Vector2 a, Vector2 b, Vector2 c) => Cross(b - a, c - b) < 0;

        static bool HoleFree(Vector2 a, Vector2 b, Vector2 c, List<Vector2> p)
        {
            for (int i = 0; i < p.Count; i++)
            {
                Vector2 q = p[i];
                if (q == a || q == b || q == c) continue;
                if (PointInTri(q, a, b, c)) return false;
            }

            return true;
        }

        static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float s = Cross(c - a, p - a) / Cross(c - a, b - a);
            float t = Cross(b - a, p - a) / Cross(c - a, b - a);
            return s >= 0 && t >= 0 && (s + t) <= 1;
        }

        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        static float SignedArea(List<Vector2> p)
        {
            float A = 0;
            for (int i = 0; i < p.Count; i++)
            {
                Vector2 p0 = p[i], p1 = p[(i + 1) % p.Count];
                A += p0.x * p1.y - p1.x * p0.y;
            }

            return 0.5f * A;
        }
    }
}
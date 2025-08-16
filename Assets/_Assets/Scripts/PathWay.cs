using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class Pathway : MonoBehaviour
{
    [Header("Spline")]
    public bool loop = true;
    [Range(2, 50)] public int gizmoSubdivPerSegment = 16; // for Scene drawing
    public Color gizmoColor = new Color(0.1f, 0.8f, 1f, 0.9f);
    public float gizmoTangentScale = 0.4f;

    [Header("Cache (read-only)")]
    [SerializeField] private List<Transform> nodes = new List<Transform>();

    public int NodeCount => nodes.Count;

    void OnEnable() { RefreshNodes(); }
    void OnTransformChildrenChanged() { RefreshNodes(); }
    void OnValidate() { RefreshNodes(); }

    public void RefreshNodes()
    {
        nodes.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            var t = transform.GetChild(i);
            if (t != null) nodes.Add(t);
        }
    }

    public Vector3 GetNode(int i)
    {
        if (nodes.Count == 0) return transform.position;
        i = Mod(i, nodes.Count);
        return nodes[i].position;
    }

    /// <summary>Catmull–Rom position in segment i with local u (0..1).</summary>
    public Vector3 GetPointOnSegment(int segIndex, float u)
    {
        int n = nodes.Count;
        if (n == 0) return transform.position;
        if (n == 1) return nodes[0].position;
        if (n == 2) return Vector3.Lerp(GetNode(segIndex), GetNode(segIndex + 1), Mathf.Clamp01(u));

        // Catmull indices (p0,p1,p2,p3). Segment goes from p1->p2
        int p1 = Mod(segIndex, n);
        int p2 = Mod(segIndex + 1, n);
        int p0 = loop ? Mod(segIndex - 1, n) : Mathf.Max(0, p1 - 1);
        int p3 = loop ? Mod(segIndex + 2, n) : Mathf.Min(n - 1, p2 + 1);

        return CatmullRom(GetNode(p0), GetNode(p1), GetNode(p2), GetNode(p3), Mathf.Clamp01(u));
    }

    /// <summary>Tangent (first derivative) on segment i with local u (0..1).</summary>
    public Vector3 GetTangentOnSegment(int segIndex, float u)
    {
        int n = nodes.Count;
        if (n == 0) return Vector3.forward;
        if (n == 1) return Vector3.forward;
        if (n == 2) return (GetNode(segIndex + 1) - GetNode(segIndex)).normalized;

        int p1 = Mod(segIndex, n);
        int p2 = Mod(segIndex + 1, n);
        int p0 = loop ? Mod(segIndex - 1, n) : Mathf.Max(0, p1 - 1);
        int p3 = loop ? Mod(segIndex + 2, n) : Mathf.Min(n - 1, p2 + 1);

        return CatmullRomTangent(GetNode(p0), GetNode(p1), GetNode(p2), GetNode(p3), Mathf.Clamp01(u)).normalized;
    }

    public int SegmentCount => loop ? Mathf.Max(0, nodes.Count) : Mathf.Max(0, nodes.Count - 1);

    // --- Catmull–Rom helpers (centripetal-ish standard with tension=0.5 implicitly) ---
    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    static Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        return 0.5f * (
            (-p0 + p2) +
            2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t +
            3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2
        );
    }

    static int Mod(int x, int m) { int r = x % m; return r < 0 ? r + m : r; }

    void OnDrawGizmos()
    {
        if (nodes == null || nodes.Count < 2) return;
        Gizmos.color = gizmoColor;

        int segs = SegmentCount;
        for (int s = 0; s < segs; s++)
        {
            Vector3 prev = GetPointOnSegment(s, 0f);
            for (int i = 1; i <= gizmoSubdivPerSegment; i++)
            {
                float u = i / (float)gizmoSubdivPerSegment;
                Vector3 curr = GetPointOnSegment(s, u);
                Gizmos.DrawLine(prev, curr);

                // draw sparse tangents
                if (i % gizmoSubdivPerSegment == 0)
                {
                    Vector3 tan = GetTangentOnSegment(s, u);
                    Gizmos.DrawLine(curr, curr + tan * gizmoTangentScale);
                }

                prev = curr;
            }
        }

        // Draw small spheres at nodes
        for (int i = 0; i < nodes.Count; i++)
        {
            Gizmos.DrawWireSphere(nodes[i].position, 0.12f);
        }
    }
}

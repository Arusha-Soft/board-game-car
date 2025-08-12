using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody))]
public class PlayerTokenMover : MonoBehaviour
{
    [Header("Tiles in exact visiting order ")]
    public Transform[] tiles;
    public int currentIndex = 0;

    [Header("Step-by-step (legacy)")]
    public float moveSpeed = 3f;     // used by both modes as base speed
    public Vector3 tileOffset = Vector3.zero;

    [Header("Hop (one bounce per step)")]
    public bool enableHop = true;    // used only when continuousMove == false
    public float hopHeight = 0.4f;
    public float minStepDuration = 0.18f;

    [Header("Tile Press (DOTween)")]
    public bool pressTiles = true;
    public float pressDepth = 0.2f;          // local Y down
    public float pressDownDuration = 0.08f;
    public float releaseDuration = 0.15f;
    public Ease pressEase = Ease.OutQuad;
    public Ease releaseEase = Ease.OutQuad;

    [Header("Tile Mesh Swap (optional)")]
    [Tooltip("If set, this mesh is used when a tile is pressed, unless an override is provided for that index.")]
    public Mesh defaultPressedMesh;
    [Tooltip("Per-tile override for pressed mesh (optional). Leave null to use defaultPressedMesh.")]
    public Mesh[] pressedMeshOverrides;

    [Header("Continuous Move")]
    public bool continuousMove = true; // << turn ON for smooth glide
    [Tooltip("Overall speed profile from 0..1 of the move. (x=time, y=progress)")]
    public AnimationCurve easeProfile = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("Extra time padding to make easing more pronounced (seconds). 0 = purely length/speed.")]
    public float extraEasingTime = 0.15f;

    Rigidbody rb;
    Coroutine moveRoutine;
    float fixedY;

    // Cached per-tile data
    Vector3[] tileLocalStart;
    MeshFilter[] tileMeshFilters;
    Mesh[] tileOriginalMeshes;

    int lastPressedIndex = -1;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    void Start()
    {
        CacheTileData();

        fixedY = transform.position.y; // lock base Y
        if (tiles != null && tiles.Length > 0 && tiles[currentIndex])
        {
            Vector3 p = tiles[currentIndex].position + tileOffset;
            p.y = fixedY;
            rb.position = p;
        }
    }

    void CacheTileData()
    {
        if (tiles == null) return;

        int n = tiles.Length;
        tileLocalStart = new Vector3[n];
        tileMeshFilters = new MeshFilter[n];
        tileOriginalMeshes = new Mesh[n];

        for (int i = 0; i < n; i++)
        {
            if (!tiles[i]) continue;
            tileLocalStart[i] = tiles[i].localPosition;

            // look in children too
            var mf = tiles[i].GetComponentInChildren<MeshFilter>();
            tileMeshFilters[i] = mf;
            if (mf) tileOriginalMeshes[i] = mf.sharedMesh; // remember original
        }
    }

    /// <summary>
    /// Public entry: moves either step-by-step (with hop) or continuous (glide) based on 'continuousMove'.
    /// </summary>
    public void MoveSteps(int steps)
    {
        if (tiles == null || tiles.Length == 0 || steps <= 0) return;
        if (tileLocalStart == null || tileLocalStart.Length != tiles.Length)
            CacheTileData();

        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(continuousMove
            ? MoveContinuousRoutine(steps)
            : MoveStepsRoutine(steps));
    }

    // =========================
    // NEW: Continuous movement
    // =========================
    IEnumerator MoveContinuousRoutine(int steps)
    {
        int n = tiles.Length;

        // Build path: start at current position (projected to fixedY), then tile centers for each step
        var nodes = new List<Vector3>(steps + 1);
        Vector3 startPos = rb.position; startPos.y = fixedY;
        nodes.Add(startPos);

        var stepTileIndices = new List<int>(steps); // which tile index is hit at each node (excluding start)
        for (int s = 1; s <= steps; s++)
        {
            int idx = (currentIndex + s) % n;
            Vector3 p = tiles[idx].position + tileOffset; p.y = fixedY;
            nodes.Add(p);
            stepTileIndices.Add(idx);
        }

        // Precompute segment lengths and cumulative distance at each node
        float totalLen = 0f;
        var segLen = new float[nodes.Count - 1];
        var cumAtNode = new float[nodes.Count]; // distance from start at node i
        cumAtNode[0] = 0f;
        for (int i = 0; i < segLen.Length; i++)
        {
            float d = Vector3.Distance(nodes[i], nodes[i + 1]);
            segLen[i] = d;
            totalLen += d;
            cumAtNode[i + 1] = totalLen;
        }
        if (totalLen < 0.0001f) yield break;

        // Time from length and speed + small padding for nicer easing
        float totalTime = (totalLen / Mathf.Max(0.001f, moveSpeed)) + Mathf.Max(0f, extraEasingTime);

        // We'll press tiles as we pass their exact node positions
        int nextPressNode = 1; // node index we’re heading to for the next press
        int nextPressTileIdx = stepTileIndices[0]; // tiles[(currentIndex+1)%n]

        // Ensure previously pressed tile (if any) pops up before starting
        ReleaseTile(lastPressedIndex);

        float t = 0f;
        while (t < totalTime)
        {
            float u = Mathf.Clamp01(t / totalTime);            // 0..1 time
            float progress = Mathf.Clamp01(easeProfile.Evaluate(u)); // 0..1 eased progress
            float dist = progress * totalLen;                   // distance along polyline

            // Move along the path based on 'dist'
            Vector3 pos = GetPointAtDistance(nodes, segLen, dist, totalLen);
            pos.y = fixedY;
            rb.MovePosition(pos);

            // Handle tile presses when we cross node thresholds
            while (nextPressNode <= steps && dist >= cumAtNode[nextPressNode] - 0.0001f)
            {
                // We just reached the center of tile 'nextPressTileIdx'
                // Release previous pressed tile, then press this one
                ReleaseTile(lastPressedIndex);
                PressTile(nextPressTileIdx);

                nextPressNode++;
                if (nextPressNode <= steps)
                {
                    int sIndex = nextPressNode - 1;
                    nextPressTileIdx = stepTileIndices[sIndex];
                }
                else
                {
                    // all press nodes handled
                    break;
                }
            }

            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // Snap to final node to avoid any drift
        Vector3 finalPos = nodes[nodes.Count - 1];
        rb.MovePosition(finalPos);

        // Final tile should NOT stay pressed
        ReleaseTile(lastPressedIndex);

        // Update logical index
        currentIndex = (currentIndex + steps) % n;
        Debug.Log($"Arrived tile #{currentIndex + 1} ({tiles[currentIndex].name})");

        moveRoutine = null;
    }

    /// <summary>
    /// Returns point along polyline 'nodes' at a given distance from start.
    /// </summary>
    Vector3 GetPointAtDistance(List<Vector3> nodes, float[] segLen, float dist, float totalLen)
    {
        if (nodes.Count == 1) return nodes[0];

        // ✅ clamp to TOTAL length, not the length of the last segment
        dist = Mathf.Clamp(dist, 0f, totalLen);

        float run = 0f;
        for (int i = 0; i < segLen.Length; i++)
        {
            float L = segLen[i];
            if (dist <= run + L || i == segLen.Length - 1)
            {
                float k = L <= 0f ? 0f : (dist - run) / L; // 0..1 within this segment
                return Vector3.Lerp(nodes[i], nodes[i + 1], k);
            }
            run += L;
        }
        return nodes[nodes.Count - 1];
    }

    // =========================
    // Legacy step-by-step move   - old move
    // =========================
    IEnumerator MoveStepsRoutine(int steps)
    {
        int n = tiles.Length;

        for (int s = 0; s < steps; s++)
        {
            // When we start moving off the current tile, release the previously pressed one
            ReleaseTile(lastPressedIndex);

            int nextIndex = (currentIndex + 1) % n;

            Vector3 start = rb.position;
            Vector3 target = tiles[nextIndex].position + tileOffset;
            start.y = fixedY;
            target.y = fixedY;

            float horizDist = Vector2.Distance(new Vector2(start.x, start.z), new Vector2(target.x, target.z));
            float duration = Mathf.Max(minStepDuration, horizDist / Mathf.Max(0.001f, moveSpeed));

            float t = 0f;
            while (t < duration)
            {
                float u = Mathf.Clamp01(t / duration);
                float easeU = 1f - Mathf.Pow(1f - u, 3f);
                Vector3 pos = Vector3.Lerp(start, target, easeU);

                float arc = enableHop ? (4f * u * (1f - u)) * hopHeight : 0f;
                pos.y = fixedY + arc;

                rb.MovePosition(pos);

                t += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            // Land on target, press & swap mesh
            rb.MovePosition(target);
            PressTile(nextIndex);
            yield return new WaitForFixedUpdate();

            currentIndex = nextIndex;
            Debug.Log($"Arrived tile #{currentIndex + 1} ({tiles[currentIndex].name})");

            // Final result tile should NOT stay pressed
            if (s == steps - 1)
            {
                ReleaseTile(currentIndex);
                yield return new WaitForFixedUpdate();
            }
        }

        moveRoutine = null;
    }

    // =========================
    // Press / Release helpers
    // =========================
    void PressTile(int idx)
    {
        if (!pressTiles || idx < 0 || idx >= tiles.Length || tiles[idx] == null) return;

        var t = tiles[idx];
        t.DOKill(); // stop any running tweens on transform

        // Press down (local Y only)
        Vector3 baseLocal = tileLocalStart[idx];
        Vector3 targetLocal = baseLocal;
        targetLocal.y = baseLocal.y - Mathf.Abs(pressDepth);
        t.DOLocalMove(targetLocal, pressDownDuration).SetEase(pressEase);

        // Mesh swap
        var mf = tileMeshFilters[idx];
        if (mf)
        {
            Mesh pressed = null;
            if (pressedMeshOverrides != null && idx < pressedMeshOverrides.Length)
                pressed = pressedMeshOverrides[idx];

            if (pressed == null) pressed = defaultPressedMesh;

            if (pressed != null)
                mf.sharedMesh = pressed;
        }

        lastPressedIndex = idx;
    }

    void ReleaseTile(int idx)
    {
        if (!pressTiles || idx < 0 || idx >= tiles.Length || tiles[idx] == null) return;

        var t = tiles[idx];
        t.DOKill();
        t.DOLocalMove(tileLocalStart[idx], releaseDuration).SetEase(releaseEase);

        // Restore original mesh
        var mf = tileMeshFilters[idx];
        if (mf && tileOriginalMeshes[idx] != null)
            mf.sharedMesh = tileOriginalMeshes[idx];

        if (lastPressedIndex == idx) lastPressedIndex = -1;
    }
}

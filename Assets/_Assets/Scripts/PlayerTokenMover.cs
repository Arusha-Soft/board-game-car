using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;       // <-- for Button
using DG.Tweening;

[RequireComponent(typeof(Rigidbody))]
public class PlayerTokenMover : MonoBehaviour
{
    [Header("UI")]
    public Button goButton; // assign your Go button in the Inspector

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
    public bool continuousMove = true; // turn ON for smooth glide
    [Tooltip("Overall speed profile from 0..1 of the move. (x=time, y=progress)")]
    public AnimationCurve easeProfile = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("Extra time padding to make easing more pronounced (seconds). 0 = purely length/speed.")]
    public float extraEasingTime = 0.15f;

    [Header("Car Turning (additive -90° at certain tiles)")]
    public bool enableTurns = true;
    public int[] turnOnIndices = new int[] { 0, 10, 20, 30 };
    public float turnDegreesY = -90f;
    public float turnDuration = 0.20f;
    public Ease turnEase = Ease.InOutSine; // we set InOutSine in code anyway
    public bool useWorldSpaceTurn = true; // false -> Local space

    [Tooltip("If true, also turn immediately when starting a move while parked on a turn tile (0/10/20/30).")]
    public bool turnWhenStartingOnTurnTile = false;

    // --- Internals ---
    Rigidbody rb;
    Coroutine moveRoutine;
    float fixedY;

    Vector3[] tileLocalStart;
    MeshFilter[] tileMeshFilters;
    Mesh[] tileOriginalMeshes;

    int lastPressedIndex = -1;
    HashSet<int> turnSet;

    Tween turnTween; // current yaw tween, so we can block until complete

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

        // Build quick lookup for turn indices
        turnSet = new HashSet<int>();
        if (turnOnIndices != null)
        {
            for (int i = 0; i < turnOnIndices.Length; i++)
                turnSet.Add(NormalizeIndex(turnOnIndices[i]));
        }

        fixedY = transform.position.y; // lock base Y
        if (tiles != null && tiles.Length > 0 && tiles[currentIndex])
        {
            Vector3 p = tiles[currentIndex].position + tileOffset;
            p.y = fixedY;
            rb.position = p;
        }
    }

    void OnDisable()
    {
        // Safety: if the object gets disabled mid-move, restore button interactivity.
        if (goButton) goButton.interactable = true;
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

            var mf = tiles[i].GetComponentInChildren<MeshFilter>();
            tileMeshFilters[i] = mf;
            if (mf) tileOriginalMeshes[i] = mf.sharedMesh;
        }
    }

    public void MoveSteps(int steps)
    {
        if (tiles == null || tiles.Length == 0 || steps <= 0) return;
        if (tileLocalStart == null || tileLocalStart.Length != tiles.Length)
            CacheTileData();

        // If a previous routine is running, stop it cleanly
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        // Disable Go button while moving
        if (goButton) goButton.interactable = false;

        moveRoutine = StartCoroutine(MoveStepsEntry(steps));
    }

    IEnumerator MoveStepsEntry(int steps)
    {
        // Optional: if we start ON a turn tile, pivot before moving out (BLOCK until done)
        if (turnWhenStartingOnTurnTile)
        {
            var tw = StartTurnAtIndex(currentIndex);
            if (tw != null)
            {
                while (tw.IsActive() && tw.IsPlaying())
                    yield return new WaitForFixedUpdate();
            }
        }

        if (continuousMove)
            yield return MoveContinuousRoutine(steps);
        else
            yield return MoveStepsRoutine(steps);

        // Re-enable Go button after movement completes
        if (goButton) goButton.interactable = true;
        moveRoutine = null;
    }

    // =========================
    // Continuous movement (BLOCK while turning)
    // =========================
    IEnumerator MoveContinuousRoutine(int steps)
    {
        int n = tiles.Length;

        // Build path
        var nodes = new List<Vector3>(steps + 1);
        Vector3 startPos = rb.position; startPos.y = fixedY;
        nodes.Add(startPos);

        var stepTileIndices = new List<int>(steps); // each arrival tile index
        for (int s = 1; s <= steps; s++)
        {
            int idx = (currentIndex + s) % n;
            Vector3 p = tiles[idx].position + tileOffset; p.y = fixedY;
            nodes.Add(p);
            stepTileIndices.Add(idx);
        }

        // Precompute lengths
        float totalLen = 0f;
        var segLen = new float[nodes.Count - 1];
        var cumAtNode = new float[nodes.Count];
        cumAtNode[0] = 0f;
        for (int i = 0; i < segLen.Length; i++)
        {
            float d = Vector3.Distance(nodes[i], nodes[i + 1]);
            segLen[i] = d;
            totalLen += d;
            cumAtNode[i + 1] = totalLen;
        }
        if (totalLen < 0.0001f) yield break;

        float totalTime = (totalLen / Mathf.Max(0.001f, moveSpeed)) + Mathf.Max(0f, extraEasingTime);

        int nextPressNode = 1;                     // next node to fire arrival effects
        int nextPressTileIdx = stepTileIndices[0]; // tile index of that node

        // Ensure previously pressed tile is released before starting
        ReleaseTile(lastPressedIndex);

        float t = 0f;
        while (t < totalTime)
        {
            float u = Mathf.Clamp01(t / totalTime);
            float progress = Mathf.Clamp01(easeProfile.Evaluate(u));
            float dist = progress * totalLen;

            // Move along path
            Vector3 pos = GetPointAtDistance(nodes, segLen, dist, totalLen);
            pos.y = fixedY;
            rb.MovePosition(pos);

            // Fire arrival effects as we cross node thresholds
            while (nextPressNode <= steps && dist >= cumAtNode[nextPressNode] - 0.0001f)
            {
                ReleaseTile(lastPressedIndex);
                PressTile(nextPressTileIdx);

                // Start turn and BLOCK movement until it finishes
                var tw = StartTurnAtIndex(nextPressTileIdx);
                if (tw != null)
                {
                    while (tw.IsActive() && tw.IsPlaying())
                        yield return new WaitForFixedUpdate();
                }

                nextPressNode++;
                if (nextPressNode <= steps)
                {
                    int sIndex = nextPressNode - 1;
                    nextPressTileIdx = stepTileIndices[sIndex];
                }
                else break;
            }

            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // Snap to final node to avoid drift
        Vector3 finalPos = nodes[nodes.Count - 1];
        rb.MovePosition(finalPos);

        // GUARANTEE final arrival effects even if while-loop missed last threshold
        int finalIdx = (currentIndex + steps) % n;
        if (nextPressNode <= steps)
        {
            var tw = StartTurnAtIndex(finalIdx);
            if (tw != null)
            {
                while (tw.IsActive() && tw.IsPlaying())
                    yield return new WaitForFixedUpdate();
            }
        }

        // Final tile should NOT stay pressed
        ReleaseTile(lastPressedIndex);

        // Update logical index
        currentIndex = finalIdx;
        Debug.Log($"Arrived tile #{currentIndex + 1} ({tiles[currentIndex].name})");
    }

    Vector3 GetPointAtDistance(List<Vector3> nodes, float[] segLen, float dist, float totalLen)
    {
        if (nodes.Count == 1) return nodes[0];
        dist = Mathf.Clamp(dist, 0f, totalLen);

        float run = 0f;
        for (int i = 0; i < segLen.Length; i++)
        {
            float L = segLen[i];
            if (dist <= run + L || i == segLen.Length - 1)
            {
                float k = L <= 0f ? 0f : (dist - run) / L;
                return Vector3.Lerp(nodes[i], nodes[i + 1], k);
            }
            run += L;
        }
        return nodes[nodes.Count - 1];
    }

    // =========================
    // Legacy step-by-step move (BLOCK while turning)
    // =========================
    IEnumerator MoveStepsRoutine(int steps)
    {
        int n = tiles.Length;

        for (int s = 0; s < steps; s++)
        {
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

            // Land on target, press & TURN (block)
            rb.MovePosition(target);
            PressTile(nextIndex);

            var tw = StartTurnAtIndex(nextIndex);
            if (tw != null)
                yield return tw.WaitForCompletion();
            else
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

    // =========================
    // Turn helpers
    // =========================
    int NormalizeIndex(int idx)
    {
        int n = (tiles != null) ? tiles.Length : 0;
        if (n <= 0) return idx;
        idx %= n;
        if (idx < 0) idx += n;
        return idx;
    }

    /// <summary>
    /// Start a precise quaternion turn if the tile index is in turnSet.
    /// Returns the Tween so callers can block until it finishes.
    /// </summary>
    Tween StartTurnAtIndex(int tileIdx)
    {
        if (!enableTurns || tiles == null || tiles.Length == 0) return null;
        if (turnSet == null || !turnSet.Contains(NormalizeIndex(tileIdx))) return null;

        // Kill any ongoing turning tween to avoid overlap/drift
        if (turnTween != null && turnTween.IsActive()) turnTween.Kill(true);

        // Compute precise target rotation as a quaternion (no Euler accumulation errors)
        Quaternion target;
        if (useWorldSpaceTurn)
        {
            // World-axis additive
            target = Quaternion.AngleAxis(turnDegreesY, Vector3.up) * transform.rotation;
        }
        else
        {
            // Local-axis additive
            target = transform.rotation * Quaternion.AngleAxis(turnDegreesY, Vector3.up);
        }

        // Tween to the exact quaternion; run in FixedUpdate to sync with MovePosition
        float dur = Mathf.Max(0.05f, turnDuration);
        turnTween = transform
            .DORotateQuaternion(target, dur)
            .SetEase(Ease.InOutSine)     // smooth S-curve
            .SetUpdate(UpdateType.Fixed) // keep in physics loop
            .OnComplete(() =>
            {
                // Snap exactly to target to eliminate any residual micro-error
                transform.rotation = target;
                turnTween = null;
            });

        return turnTween;
    }
}

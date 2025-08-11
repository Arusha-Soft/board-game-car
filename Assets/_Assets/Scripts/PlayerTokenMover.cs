using System.Collections;
using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody))]
public class PlayerTokenMover : MonoBehaviour
{
    [Header("Tiles in exact visiting order (0..N-1)")]
    public Transform[] tiles;
    public int currentIndex = 0;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public Vector3 tileOffset = Vector3.zero;

    [Header("Hop (one bounce per step)")]
    public bool enableHop = true;
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

            // Be flexible: look in children too
            var mf = tiles[i].GetComponentInChildren<MeshFilter>();
            tileMeshFilters[i] = mf;
            if (mf) tileOriginalMeshes[i] = mf.sharedMesh; // remember original
        }
    }

    public void MoveSteps(int steps)
    {
        if (tiles == null || tiles.Length == 0 || steps <= 0) return;
        if (tileLocalStart == null || tileLocalStart.Length != tiles.Length)
            CacheTileData();

        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveStepsRoutine(steps));
    }

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

            // ✅ Requirement #1: Final result tile should NOT stay pressed
            if (s == steps - 1)
            {
                // Immediately release the result tile (pop back up & restore mesh)
                ReleaseTile(currentIndex);
                yield return new WaitForFixedUpdate();
            }
        }

        moveRoutine = null;
    }

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
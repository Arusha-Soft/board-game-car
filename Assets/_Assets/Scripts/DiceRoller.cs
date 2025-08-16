using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TwoDiceRoller : MonoBehaviour
{

    [Header("Camera Shake")]
    public SmoothFollow cameraFollow;        
    public float camShakeDuration = 0.25f;
    public float camShakeMagnitude = 0.35f;
    public float camShakeFrequency = 28f;

    [Header("References")]
    public Transform dice1;
    public Transform dice2;
    public Button goButton;
    public TextMeshProUGUI resultText; // optional
    public Camera viewCamera;          // leave null -> Camera.main
    public PlayerTokenMover player; // assign in Inspector


    [Header("Durations")]
    public float moveDuration = 1.0f;   // total move time
    public float snapDuration = 0.35f;  // ease into final face
    public float returnDuration = 0.6f; // on 'R'

    [Header("Screen Targeting (pixels)")]
    public Vector2 dice1ScreenOffset = new Vector2(-80f, 0f);
    public Vector2 dice2ScreenOffset = new Vector2(80f, 0f);
    public bool followCameraDuringMove = true; // re-evaluate target each frame

    [Header("Bounce Profile (two hits)")]
    [Range(0.05f, 0.9f)] public float firstImpactT = 0.20f;
    [Range(0.1f, 0.98f)] public float secondImpactT = 0.60f;
    public float firstBounceHeight = 0.35f;
    [Range(0.05f, 1f)] public float secondBounceScale = 0.25f;

    [Header("Spin Settings (decelerate over time)")]
    public Vector2 spinSpeedRange = new Vector2(140f, 300f); // deg/sec per axis
    public float spinOverallScale = 0.85f;
    [Range(0f, 1f)] public float endSpinFactor = 0.1f;       // spin left at the end

    [Header("Boundary (BoxCollider)")]
    public BoxCollider boundary;          // assign in Inspector
    public float boundaryPadding = 0f;    // extra margin from walls
    public bool includeDiceColliderExtents = true; // keep whole die inside

    [Header("Overlap Avoidance")]
    public bool preventOverlap = true;
    public float separationPadding = 0.005f; // extra gap between dice

    [Header("Return Options")]
    public bool resetRotationOnReturn = false;

    // internal
    Vector3 dice1StartPos, dice2StartPos;
    Quaternion dice1StartRot, dice2StartRot;
    float dice1RadiusXZ, dice2RadiusXZ;
    bool busy;

    void Awake()
    {
        if (goButton) goButton.onClick.AddListener(OnGoClicked);
        if (!viewCamera) viewCamera = Camera.main;
    }

    void Start()
    {
        if (dice1) { dice1StartPos = dice1.position; dice1StartRot = dice1.rotation; }
        if (dice2) { dice2StartPos = dice2.position; dice2StartRot = dice2.rotation; }

        dice1RadiusXZ = GetDiceXZRadius(dice1);
        dice2RadiusXZ = GetDiceXZRadius(dice2);
    }

    void OnDestroy()
    {
        if (goButton) goButton.onClick.RemoveListener(OnGoClicked);
    }

    void Update()
    {
        if (!busy && Input.GetKeyDown(KeyCode.R))
            StartCoroutine(ReturnToStarts());
    }

    void OnGoClicked()
    {
        if (busy || !dice1 || !dice2) return;
        if (!viewCamera) viewCamera = Camera.main;

        // Shake camera immediately on Go
        if (!cameraFollow && Camera.main)
            cameraFollow = Camera.main.GetComponent<SmoothFollow>();
        if (cameraFollow)
            cameraFollow.Shake(camShakeDuration, camShakeMagnitude, camShakeFrequency);

        StartCoroutine(RollMoveToScreenCenter_TwoBounces());
    }

    // Convert a screen point (center + pixelOffset) to a world point on plane y = yLevel
    Vector3 ScreenCenterOnPlaneY(float yLevel, Vector2 pixelOffset)
    {
        Vector3 screen = new Vector3(Screen.width * 0.5f + pixelOffset.x,
                                     Screen.height * 0.5f + pixelOffset.y, 0f);
        Ray ray = viewCamera.ScreenPointToRay(screen);

        Plane plane = new Plane(Vector3.up, new Vector3(0f, yLevel, 0f));
        if (plane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);

        return new Vector3(0f, yLevel, 0f);
    }

    IEnumerator RollMoveToScreenCenter_TwoBounces()
    {
        busy = true;
        if (goButton) goButton.interactable = false;

        // constant spin that decelerates over time
        Vector3 angVel1 = new Vector3(
            Random.Range(spinSpeedRange.x, spinSpeedRange.y) * (Random.value < 0.5f ? -1f : 1f),
            Random.Range(spinSpeedRange.x, spinSpeedRange.y) * (Random.value < 0.5f ? -1f : 1f),
            Random.Range(spinSpeedRange.x, spinSpeedRange.y) * (Random.value < 0.5f ? -1f : 1f)
        ) * spinOverallScale;

        Vector3 angVel2 = new Vector3(
            Random.Range(spinSpeedRange.x, spinSpeedRange.y) * (Random.value < 0.5f ? -1f : 1f),
            Random.Range(spinSpeedRange.x, spinSpeedRange.y) * (Random.value < 0.5f ? -1f : 1f),
            Random.Range(spinSpeedRange.x, spinSpeedRange.y) * (Random.value < 0.5f ? -1f : 1f)
        ) * spinOverallScale;

        Vector3 startPos1 = dice1.position;
        Vector3 startPos2 = dice2.position;

        // Baseline = each die's start Y (final Y matches start Y)
        float baseY1 = startPos1.y;
        float baseY2 = startPos2.y;

        // initial desired targets at baseline (then clamp + separation)
        Vector3 desired1 = ScreenCenterOnPlaneY(baseY1, dice1ScreenOffset);
        Vector3 desired2 = ScreenCenterOnPlaneY(baseY2, dice2ScreenOffset);

        // clamp to boundary
        Vector3 fixedTarget1 = ClampXZToBoundary(desired1, dice1, baseY1);
        Vector3 fixedTarget2 = ClampXZToBoundary(desired2, dice2, baseY2);

        // Horizontal starts (keep current pos)
        Vector3 hStart1 = new Vector3(startPos1.x, baseY1, startPos1.z);
        Vector3 hStart2 = new Vector3(startPos2.x, baseY2, startPos2.z);

        // Ensure reasonable ordering of impact times
        float t1 = Mathf.Clamp01(firstImpactT);
        float t2 = Mathf.Clamp01(secondImpactT);
        if (t2 <= t1 + 0.05f) t2 = Mathf.Min(0.98f, t1 + 0.05f);

        float t = 0f;
        while (t < moveDuration)
        {
            float u = Mathf.Clamp01(t / moveDuration);

            // decelerate spin: fast at start, slow at end
            float spinEase = 1f - Mathf.Pow(1f - u, 3f);
            float spinFactor = Mathf.Lerp(1f, endSpinFactor, spinEase);
            dice1.Rotate(angVel1 * spinFactor * Time.deltaTime, Space.Self);
            dice2.Rotate(angVel2 * spinFactor * Time.deltaTime, Space.Self);

            // live clamped targets (follow camera) + pre-separate targets
            Vector3 liveTarget1 = followCameraDuringMove ? ScreenCenterOnPlaneY(baseY1, dice1ScreenOffset) : fixedTarget1;
            Vector3 liveTarget2 = followCameraDuringMove ? ScreenCenterOnPlaneY(baseY2, dice2ScreenOffset) : fixedTarget2;

            liveTarget1 = ClampXZToBoundary(liveTarget1, dice1, baseY1);
            liveTarget2 = ClampXZToBoundary(liveTarget2, dice2, baseY2);

            if (preventOverlap)
                SeparateAndClampXZ(ref liveTarget1, baseY1, dice1RadiusXZ, ref liveTarget2, baseY2, dice2RadiusXZ);

            // ease horizontal movement toward (possibly separated) targets
            float easeU = 1f - Mathf.Pow(1f - u, 3f);
            Vector3 hPos1 = Vector3.Lerp(hStart1, liveTarget1, easeU);
            Vector3 hPos2 = Vector3.Lerp(hStart2, liveTarget2, easeU);

            // vertical two-bounce profile
            float y1 = TwoBounceY(baseY1, u, t1, t2, firstBounceHeight, secondBounceScale);
            float y2 = TwoBounceY(baseY2, u, t1, t2, firstBounceHeight, secondBounceScale);

            // final positions this frame
            Vector3 next1 = new Vector3(hPos1.x, y1, hPos1.z);
            Vector3 next2 = new Vector3(hPos2.x, y2, hPos2.z);

            // enforce separation at the actual positions too (double safety)
            if (preventOverlap)
                SeparateAndClampXZ(ref next1, y1, dice1RadiusXZ, ref next2, y2, dice2RadiusXZ);

            dice1.position = next1;
            dice2.position = next2;

            t += Time.deltaTime;
            yield return null;
        }

        // Final settle at center at baseline Y, clamped + separated
        Vector3 end1 = ClampXZToBoundary(ScreenCenterOnPlaneY(baseY1, dice1ScreenOffset), dice1, baseY1);
        Vector3 end2 = ClampXZToBoundary(ScreenCenterOnPlaneY(baseY2, dice2ScreenOffset), dice2, baseY2);
        if (preventOverlap)
            SeparateAndClampXZ(ref end1, baseY1, dice1RadiusXZ, ref end2, baseY2, dice2RadiusXZ);

        dice1.position = end1;
        dice2.position = end2;

        // choose faces + ease to final rotations (no spin during/after this)
        int face1 = Random.Range(1, 7);
        int face2 = Random.Range(1, 7);
        Quaternion target1Rot = GetRotationForFace(face1, Random.Range(0f, 360f));
        Quaternion target2Rot = GetRotationForFace(face2, Random.Range(0f, 360f));

        Quaternion start1Rot = dice1.rotation;
        Quaternion start2Rot = dice2.rotation;

        t = 0f;
        while (t < snapDuration)
        {
            float u = 1f - Mathf.Pow(1f - (t / snapDuration), 3f);
            dice1.rotation = Quaternion.Slerp(start1Rot, target1Rot, u);
            dice2.rotation = Quaternion.Slerp(start2Rot, target2Rot, u);
            t += Time.deltaTime;
            yield return null;
        }
        dice1.rotation = target1Rot;
        dice2.rotation = target2Rot;

        if (resultText) resultText.text = (face1 + face2).ToString();

        //Move Player Here
        int total = face1 + face2;
        if (player) player.MoveSteps(total);

        busy = false;
        //if (goButton) goButton.interactable = true;   // Enable this Thruhh Player Code
    }

    // ===== Helpers =====

    // Keep world XZ of a point inside a BoxCollider boundary (supports rotation/scale).
    Vector3 ClampXZToBoundary(Vector3 worldPoint, Transform die, float baseY)
    {
        if (!boundary) { worldPoint.y = baseY; return worldPoint; }

        // World-space padding (walls + optional die extents)
        float padWorld = Mathf.Max(0f, boundaryPadding);
        if (includeDiceColliderExtents && die)
        {
            var dieCol = die.GetComponentInChildren<Collider>();
            if (dieCol)
            {
                var e = dieCol.bounds.extents;
                padWorld += Mathf.Max(e.x, e.z); // keep whole die inside horizontally
            }
        }

        // Work in boundary local space (true OBB)
        Transform bt = boundary.transform;
        Vector3 local = bt.InverseTransformPoint(worldPoint);
        Vector3 half = boundary.size * 0.5f;
        Vector3 center = boundary.center;

        // Convert world padding into local padding per-axis
        Vector3 ls = bt.lossyScale;
        float padLocalX = padWorld / Mathf.Max(0.0001f, Mathf.Abs(ls.x));
        float padLocalZ = padWorld / Mathf.Max(0.0001f, Mathf.Abs(ls.z));

        float minX = center.x - half.x + padLocalX;
        float maxX = center.x + half.x - padLocalX;
        float minZ = center.z - half.z + padLocalZ;
        float maxZ = center.z + half.z - padLocalZ; // ✅ FIXED (was wrong order before)

        // Handle tiny boxes / huge padding gracefully
        if (minX > maxX) { float m = 0.5f * (minX + maxX); minX = maxX = m; }
        if (minZ > maxZ) { float m = 0.5f * (minZ + maxZ); minZ = maxZ = m; }

        local.x = Mathf.Clamp(local.x, minX, maxX);
        local.z = Mathf.Clamp(local.z, minZ, maxZ);

        Vector3 clamped = bt.TransformPoint(local);
        clamped.y = baseY; // keep your per-die baseline Y
        return clamped;
    }

    // Enforce minimum XZ distance between two points, then clamp both to boundary (2 passes).
    void SeparateAndClampXZ(ref Vector3 p1, float y1, float r1, ref Vector3 p2, float y2, float r2)
    {
        // 2 passes: separate → clamp → separate → clamp (handles corners)
        for (int i = 0; i < 2; i++)
        {
            // separation on XZ
            Vector2 a = new Vector2(p1.x, p1.z);
            Vector2 b = new Vector2(p2.x, p2.z);
            float minDist = r1 + r2 + Mathf.Max(0f, separationPadding);

            Vector2 d = b - a;
            float dist = d.magnitude;

            if (dist < minDist)
            {
                Vector2 dir = dist > 1e-5f ? d / Mathf.Max(dist, 1e-5f) : new Vector2(1f, 0f);
                float overlap = minDist - dist;
                Vector2 shift = dir * (overlap * 0.5f);
                a -= shift;
                b += shift;

                p1.x = a.x; p1.z = a.y;
                p2.x = b.x; p2.z = b.y;
            }

            // clamp to boundary (if any)
            if (boundary)
            {
                p1 = ClampXZToBoundary(p1, dice1, y1);
                p2 = ClampXZToBoundary(p2, dice2, y2);
            }

            // restore Ys
            p1.y = y1; p2.y = y2;
        }
    }

    float GetDiceXZRadius(Transform die)
    {
        if (!die) return 0.1f;
        var col = die.GetComponentInChildren<Collider>();
        if (!col) return 0.1f;
        var e = col.bounds.extents;
        return Mathf.Max(0.01f, Mathf.Max(e.x, e.z));
    }

    // Two impacts: at t1 and t2 (in 0..1 of move). Baseline = baseY.
    float TwoBounceY(float baseY, float u01, float t1, float t2, float H1, float secondScale)
    {
        u01 = Mathf.Clamp01(u01);
        float H2 = Mathf.Max(0f, H1 * secondScale);

        if (u01 <= t1)
        {
            float s = Mathf.Clamp01(u01 / Mathf.Max(t1, 1e-5f));
            float easeIn = s * s;
            return Mathf.Lerp(baseY + H1 * 0.5f, baseY, easeIn); // hit baseline at t1
        }
        else if (u01 <= t2)
        {
            float s = Mathf.Clamp01((u01 - t1) / Mathf.Max(t2 - t1, 1e-5f));
            float arch = 4f * s * (1f - s); // 0..1..0
            return baseY + arch * H1;
        }
        else
        {
            float s = Mathf.Clamp01((u01 - t2) / Mathf.Max(1f - t2, 1e-5f));
            float arch = 4f * s * (1f - s);
            return baseY + arch * H2;
        }
    }

    IEnumerator ReturnToStarts()
    {
        busy = true;
        if (goButton) goButton.interactable = false;

        Vector3 posStart1 = dice1.position;
        Vector3 posStart2 = dice2.position;
        Quaternion rotStart1 = dice1.rotation;
        Quaternion rotStart2 = dice2.rotation;

        float t = 0f;
        while (t < returnDuration)
        {
            float u = 1f - Mathf.Pow(1f - (t / returnDuration), 3f);
            dice1.position = Vector3.Lerp(posStart1, dice1StartPos, u);
            dice2.position = Vector3.Lerp(posStart2, dice2StartPos, u);
            if (resetRotationOnReturn)
            {
                dice1.rotation = Quaternion.Slerp(rotStart1, dice1StartRot, u);
                dice2.rotation = Quaternion.Slerp(rotStart2, dice2StartRot, u);
            }
            t += Time.deltaTime;
            yield return null;
        }

        dice1.position = dice1StartPos;
        dice2.position = dice2StartPos;
        if (resetRotationOnReturn)
        {
            dice1.rotation = dice1StartRot;
            dice2.rotation = dice2StartRot;
        }

        busy = false;
        if (goButton) goButton.interactable = true;
    }

    // Face → (x,z) mapping; Y is random
    Quaternion GetRotationForFace(int face, float y)
    {
        float x = 0f, z = 0f;
        switch (face)
        {
            case 3: x = 0f; z = 0f; break;
            case 5: x = 90f; z = 0f; break;
            case 4: x = 180f; z = 0f; break;
            case 2: x = 270f; z = 0f; break;
            case 6: x = 0f; z = 90f; break;
            case 1: x = 0f; z = 270f; break;
            default:
                face = Mathf.Clamp(face, 1, 6);
                return GetRotationForFace(face, y);
        }
        return Quaternion.Euler(x, y, z);
    }
}

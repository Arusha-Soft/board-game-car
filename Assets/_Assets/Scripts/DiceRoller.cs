
#region Dice Moves to Given Position
//using System.Collections;
//using UnityEngine;
//using UnityEngine.UI;
//using TMPro;

//public class TwoDiceRoller : MonoBehaviour
//{
//    [Header("References")]
//    public Transform dice1;
//    public Transform dice2;
//    public Button goButton;
//    public TextMeshProUGUI resultText; // optional

//    [Header("Durations")]
//    public float spinDuration = 1.0f;     // spin time before snapping to face
//    public float snapDuration = 0.35f;    // ease into the final face
//    public float moveDuration = 1.0f;     // move-to-target time on Go
//    public float returnDuration = 0.6f;   // move-back time on 'R'

//    [Header("Movement Targets")]
//    public Vector3 dice1TargetPosition;
//    public Vector3 dice2TargetPosition;

//    [Header("Return Options")]
//    public bool resetRotationOnReturn = false; // if true, go back to initial rotation too

//    // internal state
//    Vector3 dice1StartPos, dice2StartPos;
//    Quaternion dice1StartRot, dice2StartRot;
//    bool busy; // prevents overlapping actions

//    void Awake()
//    {
//        if (goButton != null)
//            goButton.onClick.AddListener(OnGoClicked);
//    }

//    void Start()
//    {
//        // Capture true initial state once
//        if (dice1 != null)
//        {
//            dice1StartPos = dice1.position;
//            dice1StartRot = dice1.rotation;
//        }
//        if (dice2 != null)
//        {
//            dice2StartPos = dice2.position;
//            dice2StartRot = dice2.rotation;
//        }
//    }

//    void OnDestroy()
//    {
//        if (goButton != null)
//            goButton.onClick.RemoveListener(OnGoClicked);
//    }

//    void Update()
//    {
//        // Return to initial positions on 'R'
//        if (!busy && Input.GetKeyDown(KeyCode.R))
//        {
//            StartCoroutine(ReturnToStarts());
//        }
//    }

//    void OnGoClicked()
//    {
//        if (busy || dice1 == null || dice2 == null) return;
//        StartCoroutine(RollMoveToTargets());
//    }

//    IEnumerator RollMoveToTargets()
//    {
//        busy = true;
//        if (goButton) goButton.interactable = false;

//        // random spin speeds (independent)
//        Vector3 angVel1 = new Vector3(
//            Random.Range(360f, 720f) * (Random.value < 0.5f ? -1f : 1f),
//            Random.Range(360f, 720f) * (Random.value < 0.5f ? -1f : 1f),
//            Random.Range(360f, 720f) * (Random.value < 0.5f ? -1f : 1f)
//        );
//        Vector3 angVel2 = new Vector3(
//            Random.Range(360f, 720f) * (Random.value < 0.5f ? -1f : 1f),
//            Random.Range(360f, 720f) * (Random.value < 0.5f ? -1f : 1f),
//            Random.Range(360f, 720f) * (Random.value < 0.5f ? -1f : 1f)
//        );

//        // spin & move in parallel
//        float t = 0f;
//        while (t < moveDuration)
//        {
//            dice1.Rotate(angVel1 * Time.deltaTime, Space.Self);
//            dice2.Rotate(angVel2 * Time.deltaTime, Space.Self);

//            // ease-out movement
//            float eased = 1f - Mathf.Pow(1f - (t / moveDuration), 3f);
//            dice1.position = Vector3.Lerp(dice1StartPos, dice1TargetPosition, eased);
//            dice2.position = Vector3.Lerp(dice2StartPos, dice2TargetPosition, eased);

//            t += Time.deltaTime;
//            yield return null;
//        }

//        // Ensure exact target position
//        dice1.position = dice1TargetPosition;
//        dice2.position = dice2TargetPosition;

//        // choose random faces & Y rotations
//        int face1 = Random.Range(1, 7);
//        int face2 = Random.Range(1, 7);
//        Quaternion target1 = GetRotationForFace(face1, Random.Range(0f, 360f));
//        Quaternion target2 = GetRotationForFace(face2, Random.Range(0f, 360f));

//        // snap (ease) into final rotations
//        Quaternion start1 = dice1.rotation;
//        Quaternion start2 = dice2.rotation;
//        t = 0f;
//        while (t < snapDuration)
//        {
//            float u = t / snapDuration;
//            u = 1f - Mathf.Pow(1f - u, 3f);
//            dice1.rotation = Quaternion.Slerp(start1, target1, u);
//            dice2.rotation = Quaternion.Slerp(start2, target2, u);
//            t += Time.deltaTime;
//            yield return null;
//        }
//        dice1.rotation = target1;
//        dice2.rotation = target2;

//        // show sum only
//        if (resultText != null)
//            resultText.text = (face1 + face2).ToString();

//        busy = false;
//        if (goButton) goButton.interactable = true;
//    }

//    IEnumerator ReturnToStarts()
//    {
//        busy = true;
//        if (goButton) goButton.interactable = false;

//        Vector3 posStart1 = dice1.position;
//        Vector3 posStart2 = dice2.position;
//        Quaternion rotStart1 = dice1.rotation;
//        Quaternion rotStart2 = dice2.rotation;

//        float t = 0f;
//        while (t < returnDuration)
//        {
//            float u = t / returnDuration;
//            u = 1f - Mathf.Pow(1f - u, 3f); // ease-out cubic

//            dice1.position = Vector3.Lerp(posStart1, dice1StartPos, u);
//            dice2.position = Vector3.Lerp(posStart2, dice2StartPos, u);

//            if (resetRotationOnReturn)
//            {
//                dice1.rotation = Quaternion.Slerp(rotStart1, dice1StartRot, u);
//                dice2.rotation = Quaternion.Slerp(rotStart2, dice2StartRot, u);
//            }

//            t += Time.deltaTime;
//            yield return null;
//        }

//        dice1.position = dice1StartPos;
//        dice2.position = dice2StartPos;

//        if (resetRotationOnReturn)
//        {
//            dice1.rotation = dice1StartRot;
//            dice2.rotation = dice2StartRot;
//        }

//        busy = false;
//        if (goButton) goButton.interactable = true;
//    }

//    // Your face→(x,z) mapping; Y is free/random
//    Quaternion GetRotationForFace(int face, float y)
//    {
//        float x = 0f, z = 0f;
//        switch (face)
//        {
//            case 3: x = 0f; z = 0f; break;   // (0,0,0) -> 3
//            case 5: x = 90f; z = 0f; break;   // (90,0,0) -> 5
//            case 4: x = 180f; z = 0f; break;   // (180,0,0) -> 4
//            case 2: x = 270f; z = 0f; break;   // (270,0,0) -> 2
//            case 6: x = 0f; z = 90f; break;   // (0,0,90) -> 6
//            case 1: x = 0f; z = 270f; break;   // (0,0,270) -> 1
//            default:
//                face = Mathf.Clamp(face, 1, 6);
//                return GetRotationForFace(face, y);
//        }
//        return Quaternion.Euler(x, y, z);
//    }
//}
#endregion

#region Dice Go to Center of Screen 

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TwoDiceRoller : MonoBehaviour
{
    [Header("References")]
    public Transform dice1;
    public Transform dice2;
    public Button goButton;
    public TextMeshProUGUI resultText; // optional
    public Camera viewCamera;          // leave null -> Camera.main

    [Header("Durations")]
    public float moveDuration = 1.0f;   // spin+move time
    public float snapDuration = 0.35f;  // ease into final face
    public float returnDuration = 0.6f; // on 'R'

    [Header("Screen Targeting (pixels)")]
    // Offsets from the exact center of the screen, in pixels
    public Vector2 dice1ScreenOffset = new Vector2(-80f, 0f);
    public Vector2 dice2ScreenOffset = new Vector2(80f, 0f);
    public bool followCameraDuringMove = true; // re-evaluate target each frame

    [Header("Return Options")]
    public bool resetRotationOnReturn = false;

    // internal
    Vector3 dice1StartPos, dice2StartPos;
    Quaternion dice1StartRot, dice2StartRot;
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
        StartCoroutine(RollMoveToScreenCenter());
    }

    // Convert a screen point (center + pixelOffset) to a world point on plane y = yLevel
    Vector3 ScreenCenterOnPlaneY(float yLevel, Vector2 pixelOffset)
    {
        Vector3 screen = new Vector3(Screen.width * 0.5f + pixelOffset.x,
                                     Screen.height * 0.5f + pixelOffset.y, 0f);
        Ray ray = viewCamera.ScreenPointToRay(screen);

        // Plane with normal up, at height yLevel
        Plane plane = new Plane(Vector3.up, new Vector3(0f, yLevel, 0f));
        if (plane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);

        // Fallback: if no intersection (edge case), keep current xz
        return new Vector3(0f, yLevel, 0f);
    }

    IEnumerator RollMoveToScreenCenter()
    {
        busy = true;
        if (goButton) goButton.interactable = false;

        // random angular velocities
        Vector3 angVel1 = new Vector3(
            Random.Range(360f, 720f) * (Random.value < 0.5f ? -1f : 1f),
            Random.Range(360f, 720f) * (Random.value < 0.5f ? -1f : 1f),
            Random.Range(360f, 720f) * (Random.value < 0.5f ? -1f : 1f)
        );
        Vector3 angVel2 = new Vector3(
            Random.Range(360f, 720f) * (Random.value < 0.5f ? -1f : 1f),
            Random.Range(360f, 720f) * (Random.value < 0.5f ? -1f : 1f),
            Random.Range(360f, 720f) * (Random.value < 0.5f ? -1f : 1f)
        );

        Vector3 startPos1 = dice1.position;
        Vector3 startPos2 = dice2.position;

        // cache fixed targets if we don't follow the camera during move
        Vector3 fixedTarget1 = ScreenCenterOnPlaneY(startPos1.y, dice1ScreenOffset);
        Vector3 fixedTarget2 = ScreenCenterOnPlaneY(startPos2.y, dice2ScreenOffset);

        float t = 0f;
        while (t < moveDuration)
        {
            dice1.Rotate(angVel1 * Time.deltaTime, Space.Self);
            dice2.Rotate(angVel2 * Time.deltaTime, Space.Self);

            Vector3 target1 = followCameraDuringMove
                ? ScreenCenterOnPlaneY(startPos1.y, dice1ScreenOffset)
                : fixedTarget1;
            Vector3 target2 = followCameraDuringMove
                ? ScreenCenterOnPlaneY(startPos2.y, dice2ScreenOffset)
                : fixedTarget2;

            // keep Y fixed to the start Y (only move XZ)
            target1.y = startPos1.y;
            target2.y = startPos2.y;

            float u = 1f - Mathf.Pow(1f - (t / moveDuration), 3f); // ease-out
            dice1.position = Vector3.Lerp(startPos1, target1, u);
            dice2.position = Vector3.Lerp(startPos2, target2, u);

            t += Time.deltaTime;
            yield return null;
        }

        // final snap to exact XZ at screen center, Y preserved
        Vector3 end1 = ScreenCenterOnPlaneY(startPos1.y, dice1ScreenOffset);
        Vector3 end2 = ScreenCenterOnPlaneY(startPos2.y, dice2ScreenOffset);
        end1.y = startPos1.y; end2.y = startPos2.y;
        dice1.position = end1;
        dice2.position = end2;

        // choose faces + ease to final rotations
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

        busy = false;
        if (goButton) goButton.interactable = true;
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


#endregion 

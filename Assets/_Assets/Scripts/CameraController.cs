using UnityEngine;
using System.Collections;

public class SmoothFollow : MonoBehaviour
{
    [Header("Follow Settings")]
    private Transform target;
    public float smoothSpeed = 5f;

    bool isCarSet;
    Vector3 offset;

    // --- shake support ---
    Vector3 shakeOffset = Vector3.zero;
    Coroutine shakeRoutine;

    void Start()
    {
        //if (target == null)
        //{
        //    Debug.LogError("SmoothFollow: No target assigned!");
        //    enabled = false;
        //    return;
        //}

        //offset = transform.position - target.position;
    }

    void SetCamera(GameObject currentcar)
    {
        target = currentcar.transform;
        offset = transform.position - target.position;
        isCarSet = true;
    }

    void LateUpdate()
    {
        if (!isCarSet)
            return;

        // Desired position + shake offset (applied in world space)
        Vector3 desiredPosition = target.position + offset + shakeOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }

    /// <summary>Shake the camera in its local right/up directions.</summary>
    public void Shake(float duration = 0.25f, float magnitude = 0.35f, float frequency = 28f)
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine(duration, magnitude, frequency));
    }

    IEnumerator ShakeRoutine(float duration, float magnitude, float frequency)
    {
        float t = 0f;
        // Perlin seeds to get smooth jitter
        float seedX = Random.value * 1000f;
        float seedY = Random.value * 1000f;

        while (t < duration)
        {
            float u = t / duration;               // 0..1
            float damper = 1f - u;                // linear falloff (can swap with curve)
            float nx = (Mathf.PerlinNoise(seedX, Time.time * frequency) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(seedY, Time.time * frequency) - 0.5f) * 2f;

            Vector3 local = new Vector3(nx, ny, 0f) * (magnitude * damper);

            // convert to world using camera�s current right/up
            shakeOffset = transform.right * local.x + transform.up * local.y;

            t += Time.deltaTime;
            yield return null;
        }

        shakeOffset = Vector3.zero;
        shakeRoutine = null;
    }

    private void OnEnable()
    {
        CarSpawner.onCarNumber += SetCamera;
    }

    private void OnDisable()
    {
        CarSpawner.onCarNumber -= SetCamera;
    }
}

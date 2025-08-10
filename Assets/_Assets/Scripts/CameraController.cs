using UnityEngine;
using System.Collections;

public class SmoothFollow : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform target;
    public float smoothSpeed = 5f;

    [Header("Shake Settings")]
    public float shakeDuration = 0.3f;
    public float shakeMagnitude = 0.2f;

    private Vector3 offset;
    private Vector3 shakeOffset;
    private bool isShaking = false;

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("SmoothFollow: No target assigned!");
            enabled = false;
            return;
        }

        // Store the initial offset between camera and player
        offset = transform.position - target.position;
    }

    void LateUpdate()
    {
        // Desired position = player position + initial offset
        Vector3 desiredPosition = target.position + offset;

        // Smooth follow
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }
    public void Shake()
    {
        if (!isShaking)
            StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        isShaking = true;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;

            shakeOffset = new Vector3(x, y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        shakeOffset = Vector3.zero;
        isShaking = false;
    }
}

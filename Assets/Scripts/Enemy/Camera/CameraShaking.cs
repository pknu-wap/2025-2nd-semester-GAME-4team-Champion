using UnityEngine;
using System.Collections;

public class CameraShaking : MonoBehaviour
{
    public static CameraShaking Instance;
    public bool IsShaking { get; private set; }

    private Transform cam;
    private Vector3 originalPos;

    void Awake()
    {
        Instance = this;
        cam = transform;
        originalPos = cam.localPosition;
    }

    public IEnumerator Shake(float duration, float magnitude)
    {
        if (IsShaking) yield break;

        IsShaking = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            cam.localPosition = originalPos + new Vector3(x, y, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }

        cam.localPosition = originalPos;
        IsShaking = false;
    }
}

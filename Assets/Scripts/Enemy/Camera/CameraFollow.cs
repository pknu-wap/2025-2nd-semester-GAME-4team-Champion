using UnityEngine;
using System.Collections.Generic;

public class CameraFollow : MonoBehaviour
{
    [Header("Follow Targets")]
    public List<Transform> targets = new List<Transform>();

    public float smoothSpeed = 5f;
    public Vector3 offset;
    public BoxCollider2D cameraBounds;

    private float camHalfHeight;
    private float camHalfWidth;

    void Start()
    {
        Camera cam = Camera.main;
        camHalfHeight = cam.orthographicSize;
        camHalfWidth = cam.aspect * camHalfHeight;
    }

    void LateUpdate()
    {
        if (targets == null || targets.Count == 0) return;

        Vector3 avgPos = Vector3.zero;
        foreach (Transform t in targets)
        {
            if (t != null)
                avgPos += t.position;
        }
        avgPos /= targets.Count;

        Vector3 desiredPos = avgPos + offset;
        desiredPos.z = -10f;

        Vector3 smoothedPos = Vector3.Lerp(transform.position, desiredPos, smoothSpeed * Time.deltaTime);

        if (cameraBounds != null)
        {
            Bounds bounds = cameraBounds.bounds;
            float minX = bounds.min.x + camHalfWidth;
            float maxX = bounds.max.x - camHalfWidth;
            float minY = bounds.min.y + camHalfHeight;
            float maxY = bounds.max.y - camHalfHeight;

            smoothedPos.x = Mathf.Clamp(smoothedPos.x, minX, maxX);
            smoothedPos.y = Mathf.Clamp(smoothedPos.y, minY, maxY);
        }

        transform.position = smoothedPos;
    }
}

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class PlayerOcclusionSensor : MonoBehaviour
{
    [Header("Filter")]
    public LayerMask occluderMask;  // 나무/지붕/수풀 레이어 포함
    public float minDistance = 0f;

    [Header("Top-Down 옵션")]
    public bool useYDepthCheck = true;
    public float yDepthMargin = 0.0f;

    private readonly HashSet<object> _inside = new HashSet<object>(32); // Sprite/Tilemap 둘 다 관리

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private bool PassesFilter(Collider2D other)
    {
        return ((1 << other.gameObject.layer) & occluderMask) != 0;
    }
    private bool PassesYDepth(Transform occ)
    {
        if (!useYDepthCheck) return true;
        return transform.position.y < occ.position.y - yDepthMargin;
    }

    private void TryRegister(Collider2D other)
    {
        if (!PassesFilter(other)) return;

        // 1) 스프라이트 오브젝트
        var fader = other.GetComponentInParent<OccludableFader>();
        if (fader && PassesYDepth(fader.transform))
        {
            if (_inside.Add(fader)) fader.Register();
            return;
        }

        // 2) 타일맵
        var tfader = other.GetComponentInParent<TilemapOcclusionFader>();
        if (tfader && PassesYDepth(tfader.transform))
        {
            if (_inside.Add(tfader)) tfader.Register();
        }
    }

    private void TryUnregister(Collider2D other)
    {
        var fader = other.GetComponentInParent<OccludableFader>();
        if (fader && _inside.Remove(fader)) { fader.Unregister(); return; }

        var tfader = other.GetComponentInParent<TilemapOcclusionFader>();
        if (tfader && _inside.Remove(tfader)) { tfader.Unregister(); }
    }

    private void OnTriggerEnter2D(Collider2D other) => TryRegister(other);
    private void OnTriggerExit2D(Collider2D other) => TryUnregister(other);

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!useYDepthCheck) return;

        var fader = other.GetComponentInParent<OccludableFader>();
        if (fader)
        {
            bool should = PassesFilter(other) && PassesYDepth(fader.transform);
            if (should && !_inside.Contains(fader)) { _inside.Add(fader); fader.Register(); }
            else if (!should && _inside.Contains(fader)) { _inside.Remove(fader); fader.Unregister(); }
            return;
        }

        var tfader = other.GetComponentInParent<TilemapOcclusionFader>();
        if (tfader)
        {
            bool should = PassesFilter(other) && PassesYDepth(tfader.transform);
            if (should && !_inside.Contains(tfader)) { _inside.Add(tfader); tfader.Register(); }
            else if (!should && _inside.Contains(tfader)) { _inside.Remove(tfader); tfader.Unregister(); }
        }
    }
}

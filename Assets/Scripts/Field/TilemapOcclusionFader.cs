using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 타일맵을 부드럽게 페이드 인/아웃.
/// 플레이어 센서가 겹치면 Register(), 벗어나면 Unregister()를 호출받습니다.
/// </summary>
[DisallowMultipleComponent]
public class TilemapOcclusionFader : MonoBehaviour
{
    [Header("Fade")]
    [Range(0f, 1f)] public float fadedAlpha = 0.35f;
    [Min(0f)] public float fadeDuration = 0.12f;

    private Tilemap _tilemap;
    private int _refCount = 0;
    private float _targetAlpha = 1f;
    private Coroutine _co;

    private void Awake()
    {
        _tilemap = GetComponent<Tilemap>();
        if (!_tilemap) { enabled = false; return; }
    }

    public void Register()
    {
        _refCount++;
        if (_refCount < 0) _refCount = 0;
        SetTargetAlpha(fadedAlpha);
    }

    public void Unregister()
    {
        _refCount--;
        if (_refCount < 0) _refCount = 0;
        if (_refCount == 0) SetTargetAlpha(1f);
    }

    private void SetTargetAlpha(float a)
    {
        if (Mathf.Approximately(_targetAlpha, a)) return;
        _targetAlpha = a;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(FadeRoutine(a));
    }

    private IEnumerator FadeRoutine(float target)
    {
        // 현재 색에서 알파만 보간(타일맵 전체)
        Color start = _tilemap.color;
        Color end = new Color(start.r, start.g, start.b, target);
        float dur = Mathf.Max(0.0001f, fadeDuration);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            _tilemap.color = Color.Lerp(start, end, t);
            yield return null;
        }
        _tilemap.color = end;
        _co = null;
    }
}

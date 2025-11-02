using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어에게 가려질 수 있는 오브젝트에 붙입니다(트리/건물 등).
/// Register()/Unregister() 호출 횟수(refCount)에 따라 투명/불투명 전환.
/// 여러 SpriteRenderer 지원, 부드럽게 페이드.
/// </summary>
[DisallowMultipleComponent]
public class OccludableFader : MonoBehaviour
{
    [Header("Fade")]
    [Range(0f, 1f)] public float fadedAlpha = 0.35f;
    [Min(0f)] public float fadeDuration = 0.12f;

    [Header("Targets (auto-detect if empty)")]
    [SerializeField] private SpriteRenderer[] renderers;

    // 내부
    private int _refCount = 0;
    private float _currentTargetAlpha = 1f;
    private Coroutine _fadeCo;
    private static readonly List<SpriteRenderer> s_tmp = new List<SpriteRenderer>(8);

    private void Awake()
    {
        if (renderers == null || renderers.Length == 0)
        {
            // 자신과 자식의 SpriteRenderer 자동 수집
            GetComponentsInChildren(true, s_tmp);
            renderers = s_tmp.ToArray();
            s_tmp.Clear();
        }
    }

    /// <summary>플레이어 센서가 겹치기 시작했을 때 호출</summary>
    public void Register()
    {
        _refCount++;
        if (_refCount < 0) _refCount = 0;
        SetTargetAlpha(fadedAlpha);
    }

    /// <summary>플레이어 센서가 벗어났을 때 호출</summary>
    public void Unregister()
    {
        _refCount--;
        if (_refCount < 0) _refCount = 0;
        if (_refCount == 0)
            SetTargetAlpha(1f);
    }

    private void SetTargetAlpha(float a)
    {
        if (Mathf.Approximately(_currentTargetAlpha, a)) return;
        _currentTargetAlpha = a;
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeRoutine(a));
    }

    private IEnumerator FadeRoutine(float target)
    {
        if (renderers == null || renderers.Length == 0) yield break;

        float dur = Mathf.Max(0.0001f, fadeDuration);
        // 시작 알파: 첫 렌더러 기준
        float start = renderers[0].color.a;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float a = Mathf.Lerp(start, target, t);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;
                var c = r.color;
                c.a = a;
                r.color = c;
            }
            yield return null;
        }
        // 보정
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (!r) continue;
            var c = r.color;
            c.a = target;
            r.color = c;
        }
        _fadeCo = null;
    }
}

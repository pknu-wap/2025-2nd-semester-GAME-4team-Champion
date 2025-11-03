using UnityEngine;
using System;
using System.Collections.Generic;

public class CameraFXRouter : MonoBehaviour
{
    [Serializable]
    public struct TagProfile { public string tag; public CameraFXProfile profile; }

    [Header("Camera Controller")]
    [SerializeField] private CameraFXController fx;

    [Header("Tag → Profile Routing")]
    [SerializeField] private TagProfile[] routes;

    private readonly Dictionary<string, CameraFXProfile> _map = new();

    private void Awake()
    {
        if (fx == null) fx = FindFirstObjectByType<CameraFXController>();

        _map.Clear();
        if (routes != null)
        {
            foreach (var r in routes)
                if (!string.IsNullOrEmpty(r.tag) && r.profile != null)
                    _map[r.tag] = r.profile;
        }

        // ✅ 전역 태그 버스 구독
        TagBus.OnTag += OnTagCasted;
    }

    private void OnDestroy()
    {
        TagBus.OnTag -= OnTagCasted;
    }

    private void OnTagCasted(string tag)
    {
        if (fx == null) return;
        if (_map.TryGetValue(tag, out var p) && p != null)
            fx.PlayProfile(p);
        // else: 매핑 안 된 태그는 무시 (원하면 Debug.Log로 확인)
    }

    // 수동 테스트용
    public void PlayByTag(string tag) => OnTagCasted(tag);
}

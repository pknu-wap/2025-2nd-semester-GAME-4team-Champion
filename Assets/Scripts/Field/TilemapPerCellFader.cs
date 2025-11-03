using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public class TilemapPerCellFader : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private Transform player; // Tag=Player 자동 탐색

    [Header("Area (cells)")]
    [Tooltip("좌우/위 반경(셀). ex) (1,2) = 좌우 1, 위로 2")]
    public Vector2Int radius = new Vector2Int(1, 2);

    [Header("Fade")]
    [Range(0f, 1f)] public float fadedAlpha = 0.35f;
    [Min(0.01f)] public float fadeDuration = 0.12f;
    [Tooltip("반경을 벗어난 타일은 즉시 1.0으로 복구")]
    public bool restoreInstantOutsideRadius = true;

    [Header("Top-Down 옵션")]
    [Tooltip("플레이어보다 위(Y가 큼) 타일만 페이드")]
    public bool fadeAheadOnly = true;
    public float yMargin = 0f;

    // 상태
    private readonly HashSet<Vector3Int> _target = new(); // 이번 프레임 페이드 대상
    private readonly HashSet<Vector3Int> _faded = new();  // 현재 페이드 중(알파<1) 셀들
    private readonly Dictionary<Vector3Int, float> _alpha = new(); // 현재 알파 캐시

    private float _fadeSpeed;

    private void Reset()
    {
        tilemap = GetComponent<Tilemap>();
    }

    private void Awake()
    {
        if (!tilemap) tilemap = GetComponent<Tilemap>();
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        _fadeSpeed = 1f / Mathf.Max(0.01f, fadeDuration);
    }

    private void OnValidate()
    {
        radius.x = Mathf.Max(0, radius.x);
        radius.y = Mathf.Max(0, radius.y);
        _fadeSpeed = 1f / Mathf.Max(0.01f, fadeDuration);
    }

    private void OnDisable() { RestoreAllImmediate(); }
    private void OnDestroy() { RestoreAllImmediate(); }

    private void Update()
    {
        if (!tilemap || !player) return;

        // 1) 이번 프레임 대상 셀 계산
        _target.Clear();

        var center = tilemap.WorldToCell(player.position);
        int minX = center.x - radius.x;
        int maxX = center.x + radius.x;

        int minY = fadeAheadOnly ? center.y + 1 : center.y - radius.y;
        int maxY = center.y + radius.y;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (!tilemap.HasTile(cell)) continue;

                if (fadeAheadOnly)
                {
                    var wc = tilemap.GetCellCenterWorld(cell);
                    if (wc.y < player.position.y + yMargin) continue;
                }
                _target.Add(cell);
            }
        }

        // 2) 타겟에 포함된 셀은 fadedAlpha로, 포함되지 않은 기존 페이드 셀은 1.0으로
        // 2-1) 타겟 → 페이드 다운
        foreach (var c in _target)
        {
            FadeTowards(c, fadedAlpha);
            _faded.Add(c);
        }

        // 2-2) 타겟 밖이 된 셀들 → 복구
        //      복구 후보를 따로 모아 한 번에 처리(이터레이터 안전)
        _toRestore.Clear();
        foreach (var c in _faded)
            if (!_target.Contains(c))
                _toRestore.Add(c);

        foreach (var c in _toRestore)
        {
            if (restoreInstantOutsideRadius)
            {
                SetAlphaImmediate(c, 1f);
                _faded.Remove(c);
                _alpha.Remove(c);
            }
            else
            {
                bool done = FadeTowards(c, 1f);
                if (done) { _faded.Remove(c); _alpha.Remove(c); }
            }
        }

        // 2-3) 타겟에 포함되어 있어도 이미 1로 복귀했으면 faded 목록에서 제거(경계 흔들림 방지)
        _toRestore.Clear();
        foreach (var c in _faded)
        {
            if (_alpha.TryGetValue(c, out var a) && Mathf.Approximately(a, 1f) && !_target.Contains(c))
                _toRestore.Add(c);
        }
        foreach (var c in _toRestore)
        {
            _faded.Remove(c);
            _alpha.Remove(c);
        }
    }

    // 내부 버퍼
    private readonly List<Vector3Int> _toRestore = new(64);

    private bool FadeTowards(Vector3Int cell, float target)
    {
        float cur = tilemap.GetColor(cell).a;
        float next = Mathf.MoveTowards(cur, target, _fadeSpeed * Time.deltaTime);
        if (!Mathf.Approximately(next, cur))
        {
            var col = tilemap.GetColor(cell);
            col.a = next;
            tilemap.SetTileFlags(cell, TileFlags.None);
            tilemap.SetColor(cell, col);
        }
        _alpha[cell] = next;
        return Mathf.Approximately(next, target);
    }

    private void SetAlphaImmediate(Vector3Int cell, float a)
    {
        var col = tilemap.GetColor(cell);
        if (!Mathf.Approximately(col.a, a))
        {
            col.a = a;
            tilemap.SetTileFlags(cell, TileFlags.None);
            tilemap.SetColor(cell, col);
        }
    }

    private void RestoreAllImmediate()
    {
        if (!tilemap) return;
        foreach (var kv in _alpha)
        {
            var cell = kv.Key;
            if (!tilemap.HasTile(cell)) continue;
            SetAlphaImmediate(cell, 1f);
        }
        _alpha.Clear();
        _faded.Clear();
        _target.Clear();
        _toRestore.Clear();
    }
}

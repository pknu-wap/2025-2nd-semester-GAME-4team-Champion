using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 플레이어 주변 '일부 타일'만 부드럽게 페이드 인/아웃.
/// - 타일맵에 부착
/// - player(Transform)만 지정하면 동작 (선택: sensorCollider로 정확도 ↑)
/// - 성능: 매 프레임 전체 타일을 만지지 않고, '현재 반경 + 직전 반경'의 타일만 갱신
/// </summary>
[DisallowMultipleComponent]
public class TilemapPerCellFader : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private Transform player;                 // 플레이어 Transform (Tag=Player 자동 탐색)
    [SerializeField] private Collider2D sensorCollider;        // (선택) 플레이어의 작은 Trigger 센서

    [Header("Area (in cells)")]
    [Tooltip("가로/세로 반경(셀 단위). 예: (1,2)면 좌우 1셀, 위로 2셀")]
    public Vector2Int radius = new Vector2Int(1, 2);

    [Header("Fade")]
    [Range(0f, 1f)] public float fadedAlpha = 0.35f;
    [Tooltip("페이드에 걸리는 시간(초)")]
    [Min(0.01f)] public float fadeDuration = 0.12f;

    [Header("Top-Down 옵션")]
    [Tooltip("플레이어보다 '위쪽(Y가 큰)' 타일만 페이드")]
    public bool fadeAheadOnly = true;
    [Tooltip("Y 위/아래 판정 여유값")]
    public float yMargin = 0.0f;

    private readonly HashSet<Vector3Int> _now = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> _prev = new HashSet<Vector3Int>();
    private readonly Dictionary<Vector3Int, float> _alpha = new Dictionary<Vector3Int, float>(); // 현재 알파

    private float _fadeSpeed; // 초당 변화량

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
        if (radius.x < 0) radius.x = 0;
        if (radius.y < 0) radius.y = 0;
        _fadeSpeed = 1f / Mathf.Max(0.01f, fadeDuration);
    }

    private void Update()
    {
        if (!tilemap || !player) return;

        // 1) 이번 프레임에 페이드 대상이 될 셀 집합 계산
        _prev.Clear();
        foreach (var c in _now) _prev.Add(c);
        _now.Clear();

        if (sensorCollider)
        {
            // 센서 콜라이더 AABB를 셀 영역으로 변환해 스캔 (정확도↑)
            Bounds b = sensorCollider.bounds;
            // 위로 radius.y, 좌우 radius.x까지 확장
            b.Expand(new Vector3(radius.x * 2 + 1, radius.y * 2 + 1, 0f) * tilemap.layoutGrid.cellSize.magnitude * 0.5f);
            AddCellsInBounds(b);
        }
        else
        {
            // 플레이어 기준 셀 + 반경
            Vector3Int center = tilemap.WorldToCell(player.position);
            for (int dx = -radius.x; dx <= radius.x; dx++)
            {
                for (int dy = 0; dy <= radius.y; dy++) // 위쪽만 기본
                {
                    var c = new Vector3Int(center.x + dx, center.y + dy, 0);
                    if (!tilemap.HasTile(c)) continue;

                    if (fadeAheadOnly)
                    {
                        Vector3 worldCenter = tilemap.GetCellCenterWorld(c);
                        if (worldCenter.y < player.position.y + yMargin)
                            continue;
                    }
                    _now.Add(c);
                }
            }
        }

        // 2) 이번 프레임에 상태가 바뀐 셀들만 처리 (now ∪ prev)
        //    - now 에 있는 셀 → 목표 알파 = fadedAlpha
        //    - prev 에만 있고 now 에는 없는 셀 → 목표 알파 = 1
        foreach (var c in _now)
            StepAlpha(c, fadedAlpha);

        foreach (var c in _prev)
        {
            if (_now.Contains(c)) continue;
            StepAlpha(c, 1f);
        }
    }

    private void AddCellsInBounds(Bounds worldBounds)
    {
        // worldBounds를 셀 영역으로 변환
        Vector3Int min = tilemap.WorldToCell(worldBounds.min);
        Vector3Int max = tilemap.WorldToCell(worldBounds.max);

        for (int x = min.x - radius.x; x <= max.x + radius.x; x++)
        {
            for (int y = (fadeAheadOnly ? Mathf.Max(min.y, tilemap.WorldToCell(player.position).y) : min.y);
                 y <= max.y + radius.y; y++)
            {
                var c = new Vector3Int(x, y, 0);
                if (!tilemap.HasTile(c)) continue;

                if (fadeAheadOnly)
                {
                    Vector3 wc = tilemap.GetCellCenterWorld(c);
                    if (wc.y < player.position.y + yMargin) continue;
                }

                _now.Add(c);
            }
        }
    }

    private void StepAlpha(Vector3Int cell, float targetAlpha)
    {
        // 현재 알파 조회 (없으면 타일맵 현재 컬러 기반)
        float cur;
        if (!_alpha.TryGetValue(cell, out cur))
        {
            cur = tilemap.GetColor(cell).a;
            _alpha[cell] = cur;
        }

        // 부드럽게 보간
        float next = Mathf.MoveTowards(cur, targetAlpha, _fadeSpeed * Time.deltaTime);
        if (Mathf.Approximately(next, cur)) return;

        // 색 반영 (RGB는 유지, 알파만 변경)
        Color c = tilemap.GetColor(cell);
        c.a = next;
        tilemap.SetTileFlags(cell, TileFlags.None);   // 색 변경 허용
        tilemap.SetColor(cell, c);

        _alpha[cell] = next;

        // 완전히 1로 복귀하면 캐시 정리
        if (Mathf.Approximately(next, 1f) && Mathf.Approximately(targetAlpha, 1f))
            _alpha.Remove(cell);
    }
}

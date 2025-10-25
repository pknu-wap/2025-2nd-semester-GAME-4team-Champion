using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(Tilemap))]
public class FieldMapBaker : MonoBehaviour
{
    [Header("Baker Options")]
    [SerializeField] private bool bakeOnStart = true;
    [SerializeField] private bool hideBakedChildrenInHierarchy = true;

    private Tilemap _src;
    private const string CHILD_PREFIX = "_Baked_";

    private void OnEnable()
    {
        if (!bakeOnStart) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null) StartCoroutine(DelayedBake()); // 에디터에서도 1프레임 지연
            };
            return;
        }
#endif
        StartCoroutine(DelayedBake());
    }

    private System.Collections.IEnumerator DelayedBake()
    {
        yield return null; // 1프레임 지연로 초기화 순서 회피
        SafeBake();
    }

    [ContextMenu("Bake Now")]
    public void SafeBake()
    {
        if (_src == null) _src = GetComponent<Tilemap>();
        if (_src == null)
        {
            Debug.LogWarning("[FieldMapBaker] Source Tilemap not found.", this);
            return;
        }

        // 원본에 아무것도 없으면 정리만 하고 끝
        if (_src.GetUsedTilesCount() == 0)
        {
            CleanupBakedChildren();
            Debug.Log("[FieldMapBaker] Source Tilemap is empty. Nothing to bake.", this);
            return;
        }

        CleanupBakedChildren();

        // 1) 타입별 위치 수집 (안전 경로: allPositionsWithin + GetTile<FieldTile>)
        var byType = new Dictionary<FieldEffectType, List<Vector3Int>>();
        var byTile = new Dictionary<Vector3Int, FieldTile>();

        var bounds = _src.cellBounds;
        foreach (var pos in bounds.allPositionsWithin)
        {
            var ft = _src.GetTile<FieldTile>(pos);
            if (!ft) continue; // FieldTile만 취급

            if (!byType.TryGetValue(ft.effectType, out var list))
            {
                list = new List<Vector3Int>();
                byType[ft.effectType] = list;
            }
            list.Add(pos);
            byTile[pos] = ft;
        }

        if (byType.Count == 0)
        {
            Debug.Log("[FieldMapBaker] No FieldTile found to bake.", this);
            return;
        }

        // 2) 타입별 자식 타일맵 생성
        foreach (var kv in byType)
        {
            var type = kv.Key;
            var cells = kv.Value;
            if (cells == null || cells.Count == 0) continue;

            var child = new GameObject($"{CHILD_PREFIX}{type}");
            child.transform.SetParent(transform, false);
            if (hideBakedChildrenInHierarchy)
                child.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;

            var tm = child.AddComponent<Tilemap>();
            var rd = child.AddComponent<TilemapRenderer>();
            if (rd == null) { Debug.LogError("[FieldMapBaker] TilemapRenderer add failed.", child); continue; }
            rd.sortingOrder = 0;

            // 시각 타일 복사 (키가 존재하는 것만)
            foreach (var cell in cells)
            {
                if (byTile.TryGetValue(cell, out var tile) && tile)
                    tm.SetTile(cell, tile);
            }
            tm.RefreshAllTiles();

            // ---------- Unity 6 Physics2D ----------
            var tmc = child.AddComponent<TilemapCollider2D>();
            tmc.compositeOperation = Collider2D.CompositeOperation.Merge;
            var comp = child.AddComponent<CompositeCollider2D>();
            comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
            comp.generationType = CompositeCollider2D.GenerationType.Synchronous;

            var rb = child.GetComponent<Rigidbody2D>();
            if (rb == null) rb = child.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            rb.simulated = true;
            rb.gravityScale = 0f;
            rb.useFullKinematicContacts = false;

            // 트리거 여부 결정(혼재 시 false가 하나라도 있으면 충돌체)
            bool isTrigger = true;
            foreach (var cell in cells)
            {
                if (byTile.TryGetValue(cell, out var tile) && tile && tile.isTriggerZone == false)
                { isTrigger = false; break; }
            }
            comp.isTrigger = isTrigger;

            // 3) 타입별 스크립트 부착
            AttachTypeScripts(type, child, cells, byTile);
            tm.RefreshAllTiles();
        }

        Debug.Log($"[FieldMapBaker] Bake OK. Types: {byType.Count}", this);
    }

    private void AttachTypeScripts(FieldEffectType type, GameObject child, List<Vector3Int> cells, Dictionary<Vector3Int, FieldTile> byTile)
    {
        // '첫 유효 FieldTile' 찾기
        FieldTile any = null;
        if (cells != null)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (byTile.TryGetValue(cells[i], out var t) && t) { any = t; break; }
            }
        }

        switch (type)
        {
            case FieldEffectType.SpeedPad:
                {
                    var comp = child.GetComponent<CompositeCollider2D>();
                    if (comp) comp.isTrigger = true; // SpeedPad는 트리거 강제

                    var zone = child.AddComponent<SpeedPadZone>();
                    if (zone != null && any && any.speedPadSettings != null)
                    {
                        // SpeedPadZone.settings 는 SerializeField(private)일 수 있으므로 리플렉션으로만 세팅
                        var fi = typeof(SpeedPadZone).GetField("settings",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                        if (fi != null) fi.SetValue(zone, any.speedPadSettings);
                    }
                    break;
                }

            case FieldEffectType.JJangStone:
                {
                    var comp = child.GetComponent<CompositeCollider2D>();
                    if (comp) comp.isTrigger = true; // 넘어짐도 트리거 권장

                    var trip = child.AddComponent<JJangStone>();
                    if (trip != null)
                    {
                        // 공개 API가 있으면 그것 사용 권장. (여긴 방어형 리플렉션)
                        SetFieldIfExists(trip, "tripAnimTrigger", "Trip");
                        SetFieldIfExists(trip, "retriggerCooldown", 1.0f);
                        SetFieldIfExists(trip, "tripDuration", 2.0f);

                        if (any)
                        {
                            SetFieldIfExists(trip, "pushForce", any.tripPushForce);
                            SetFieldIfExists(trip, "stopDelay", any.tripStopDelay);
                        }
                    }
                    break;
                }

            case FieldEffectType.SolidWall:
                {
                    var comp = child.GetComponent<CompositeCollider2D>();
                    if (comp) comp.isTrigger = false; // 벽은 충돌체
                    break;
                }

            default:
                break;
        }
    }

    private static void SetFieldIfExists(object target, string fieldName, object value)
    {
        if (target == null) return;
        var f = target.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (f != null) f.SetValue(target, value);
    }

    private void CleanupBakedChildren()
    {
        var list = new List<GameObject>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i);
            if (c && c.name.StartsWith(CHILD_PREFIX))
                list.Add(c.gameObject);
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            foreach (var go in list)
                Undo.DestroyObjectImmediate(go);
        }
        else
#endif
        {
            foreach (var go in list)
                Destroy(go);
        }
    }
}

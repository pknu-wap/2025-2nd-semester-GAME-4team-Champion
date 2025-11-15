using System.Collections;
using UnityEngine;

public class CombatState : MonoBehaviour
{
    // ---------- References ----------
    [Header("References")]
    [SerializeField] private PlayerCombat owner;          // 소유자(호출 래핑용)
    [SerializeField] private PlayerMoveBehaviour moveRef; // 이동/플립 제어
    [SerializeField] private Animator animator;           // InCombat bool
    [SerializeField] private PlayerAttack attack;         // 공격 중 자동 플립 금지 옵션용
    [SerializeField] private CameraLockOn cameraLockOn;   // << 락온 컨트롤(선택)

    // ---------- Config ----------
    [Header("Combat State")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private float disengageDistance = 150f;
    [SerializeField] private float enemyScanInterval = 0.25f;
    [SerializeField, Range(0f, 1f)] private float combatYSpeedMultiplier = 0.3f;

    [Header("Auto Face")]
    [SerializeField] private bool autoFaceOnCombatEnter = true;
    [SerializeField] private float autoFaceSearchRadius = 15f;
    [SerializeField] private bool autoFaceDuringCombat = true;
    [SerializeField] private bool autoFaceEvenWhileAttacking = false;
    [SerializeField] private float autoFaceInputDeadzoneX = 0.2f;

    [Header("Optimization")]
    [SerializeField] private int overlapBufferSize = 48;

    // ---------- State ----------
    private bool inCombat = false;
    private Coroutine combatMonitorCo;
    private Collider2D[] _overlapBuf;

    // ---------- Public API ----------
    public bool IsInCombat => inCombat;
    public float CombatYSpeedMul => combatYSpeedMultiplier;
    public LayerMask EnemyMask => enemyMask;

    /// <summary>
    /// 외부에서 초기 바인딩할 때 사용. cameraLockOn은 선택 인자.
    /// </summary>
    public void Bind(PlayerCombat ownerCombat, PlayerMoveBehaviour mv, Animator anim, PlayerAttack atk, CameraLockOn lockOn = null)
    {
        owner = ownerCombat;
        moveRef = mv;
        animator = anim;
        attack = atk;
        if (lockOn) cameraLockOn = lockOn;

        if (_overlapBuf == null || _overlapBuf.Length != Mathf.Max(8, overlapBufferSize))
            _overlapBuf = new Collider2D[Mathf.Max(8, overlapBufferSize)];

        // 자동 보강
        AutoWireIfMissing();
    }

    private void Reset()
    {
        if (!owner) owner = GetComponent<PlayerCombat>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
        if (!attack) attack = GetComponent<PlayerAttack>();
        // 에디터에서 넣지 않았으면 씬에서 자동 검색
        if (!cameraLockOn) cameraLockOn = FindFirstObjectByType<CameraLockOn>(FindObjectsInactive.Include);
    }

    private void Awake()
    {
        if (_overlapBuf == null) _overlapBuf = new Collider2D[Mathf.Max(8, overlapBufferSize)];
        AutoWireIfMissing();
    }

    private void AutoWireIfMissing()
    {
        // 런타임에서도 누락 시 한 번 더 보강
        if (!cameraLockOn)
            cameraLockOn = FindFirstObjectByType<CameraLockOn>(FindObjectsInactive.Include);
    }

    public void EnterCombat(string reason = null)
    {
        if (inCombat) return;
        inCombat = true;

        animator?.SetBool("InCombat", true);
        cameraLockOn?.EnterCombat(); // << 널 안전
        moveRef?.SetFlipFromMovementBlocked(true);

        if (autoFaceOnCombatEnter) TryAutoFaceNearestEnemyX();

        if (combatMonitorCo != null) StopCoroutine(combatMonitorCo);
        combatMonitorCo = StartCoroutine(CombatMonitor());

        if (owner != null && owner.DebugLogs) Debug.Log($"[Combat] Enter ({reason})");
    }

    public void ExitCombat()
    {
        if (!inCombat) return;
        inCombat = false;

        animator?.SetBool("InCombat", false);
        cameraLockOn?.ExitCombat(); // << 널 안전
        moveRef?.SetFlipFromMovementBlocked(false);

        if (combatMonitorCo != null)
        {
            StopCoroutine(combatMonitorCo);
            combatMonitorCo = null;
        }

        if (owner != null && owner.DebugLogs) Debug.Log("[Combat] Exit");
    }

    // ----- Internal -----
    private IEnumerator CombatMonitor()
    {
        var wait = new WaitForSeconds(enemyScanInterval);
        while (inCombat)
        {
            if (autoFaceDuringCombat) AutoFaceTick();

            if (!HasEnemyWithin(disengageDistance))
            {
                ExitCombat();
                yield break;
            }
            yield return wait;
        }
    }

    private bool HasEnemyWithin(float dist)
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, dist, enemyMask);
        return hits != null && hits.Length > 0;
    }

    private void AutoFaceTick()
    {
        if (!moveRef) return;

        // 플레이어가 수평 입력을 크게 주면 자동 전환 보류
        if (Mathf.Abs(moveRef.CurrentInput.x) >= autoFaceInputDeadzoneX)
            return;

        // 공격 중엔 자동 플립 금지(옵션)
        if (!autoFaceEvenWhileAttacking && attack != null && attack.IsAttacking)
            return;

        TryAutoFaceNearestEnemyX();
    }

    private void TryAutoFaceNearestEnemyX()
    {
        float radius = (autoFaceSearchRadius > 0f) ? autoFaceSearchRadius : disengageDistance;
        var hits = Physics2D.OverlapCircleAll(transform.position, radius, enemyMask);
        if (hits == null || hits.Length == 0) return;

        Transform nearest = null;
        float bestSqr = float.PositiveInfinity;
        Vector2 myPos = transform.position;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (!h) continue;
            float d2 = ((Vector2)h.transform.position - myPos).sqrMagnitude;
            if (d2 < bestSqr) { bestSqr = d2; nearest = h.transform; }
        }

        if (nearest && moveRef)
            moveRef.FaceTargetX(nearest.position.x);
    }
}

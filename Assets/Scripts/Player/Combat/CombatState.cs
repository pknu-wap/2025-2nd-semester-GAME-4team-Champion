using System.Collections;
using UnityEngine;

public class CombatState : MonoBehaviour
{
    // ---------- References ----------
    [Header("References")]
    [SerializeField] private PlayerCombat owner;          // 소유자(호출 래핑용)
    [SerializeField] private PlayerMoveBehaviour moveRef; // 이동/플립 제어
    [SerializeField] private Animator animator;           // 인컴뱃 bool
    [SerializeField] private PlayerAttack attack;         // 공격 중 자동 플립 금지 옵션용

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
    [SerializeField] private int overlapBufferSize = 48; // NonAlloc 버퍼 크기(상황에 맞게 조정)

    // ---------- State ----------
    private bool inCombat = false;
    private Coroutine combatMonitorCo;

    // NonAlloc 버퍼
    private Collider2D[] _overlapBuf;

    // ---------- Public API ----------
    public bool IsInCombat => inCombat;
    public float CombatYSpeedMul => combatYSpeedMultiplier;
    public LayerMask EnemyMask => enemyMask;

    public void Bind(PlayerCombat ownerCombat, PlayerMoveBehaviour mv, Animator anim, PlayerAttack atk)
    {
        owner = ownerCombat;
        moveRef = mv;
        animator = anim;
        attack = atk;
        if (_overlapBuf == null || _overlapBuf.Length != Mathf.Max(8, overlapBufferSize))
            _overlapBuf = new Collider2D[Mathf.Max(8, overlapBufferSize)];
    }

    private void Reset()
    {
        if (!owner) owner = GetComponent<PlayerCombat>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
        if (!attack) attack = GetComponent<PlayerAttack>();
    }

    public void EnterCombat(string reason = null)
    {
        if (inCombat) return;
        inCombat = true;
        animator?.SetBool("InCombat", true);

        // 전투 중에는 이동 입력으로 인한 X플립을 잠깐 막아두는 기존 동작 유지
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

        // 공격 중엔 자동 플립을 하지 않도록(옵션)
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

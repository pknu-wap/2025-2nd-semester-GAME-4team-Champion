using System.Collections;
using UnityEngine;

public class CombatState : MonoBehaviour
{
    // ---------- References ----------
    [Header("References")]
    [SerializeField] private PlayerCombat owner;          // ������(ȣ�� ���ο�)
    [SerializeField] private PlayerMoveBehaviour moveRef; // �̵�/�ø� ����
    [SerializeField] private Animator animator;           // InCombat bool
    [SerializeField] private PlayerAttack attack;         // ���� �� �ڵ� �ø� ���� �ɼǿ�
    [SerializeField] private CameraLockOn cameraLockOn;   // << ���� ��Ʈ��(����)

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
    /// �ܺο��� �ʱ� ���ε��� �� ���. cameraLockOn�� ���� ����.
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

        // �ڵ� ����
        AutoWireIfMissing();
    }

    private void Reset()
    {
        if (!owner) owner = GetComponent<PlayerCombat>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
        if (!attack) attack = GetComponent<PlayerAttack>();
        // �����Ϳ��� ���� �ʾ����� ������ �ڵ� �˻�
        if (!cameraLockOn) cameraLockOn = FindFirstObjectByType<CameraLockOn>(FindObjectsInactive.Include);
    }

    private void Awake()
    {
        if (_overlapBuf == null) _overlapBuf = new Collider2D[Mathf.Max(8, overlapBufferSize)];
        AutoWireIfMissing();
    }

    private void AutoWireIfMissing()
    {
        // ��Ÿ�ӿ����� ���� �� �� �� �� ����
        if (!cameraLockOn)
            cameraLockOn = FindFirstObjectByType<CameraLockOn>(FindObjectsInactive.Include);
    }

    public void EnterCombat(string reason = null)
    {
        if (inCombat) return;
        inCombat = true;

        animator?.SetBool("InCombat", true);
        cameraLockOn?.EnterCombat(); // << �� ����
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
        cameraLockOn?.ExitCombat(); // << �� ����
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

        // �÷��̾ ���� �Է��� ũ�� �ָ� �ڵ� ��ȯ ����
        if (Mathf.Abs(moveRef.CurrentInput.x) >= autoFaceInputDeadzoneX)
            return;

        // ���� �߿� �ڵ� �ø� ����(�ɼ�)
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

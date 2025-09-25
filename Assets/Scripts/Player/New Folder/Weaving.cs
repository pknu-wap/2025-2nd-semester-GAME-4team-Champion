using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public enum DefenseOutcome { None, Block, Parry }

public class EnemyWeaving : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;

    [Header("Weaving(Parry) Config")]
    [SerializeField] private float guardAngle = 120f;     // 정면 콘 각도
    [SerializeField] private float parryWindow = 0.30f;   // 가드 시작 후 패링 유효 시간
    [SerializeField] private float weavingPostHold = 0.10f; // 패링 직후 가드를 유지할 추가 시간

    [Header("Block Balance")]
    [SerializeField] private float blockDamageMul = 0f;
    [SerializeField] private float blockKnockMul = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    // 상태
    private bool isBlocking = false;
    private float blockPressedTime = -999f;

    private float parryLockEndTime = 0f;
    private Coroutine parryLockCo;
    private Coroutine forcedBlockCo;

    // 외부가 구독해서 AI 이동/행동을 잠그고 싶다면 사용
    public UnityEvent<float> OnParryLockRequested; // duration seconds

    // 읽기/튜닝용 프로퍼티
    public bool IsBlocking => isBlocking;
    public float LastBlockPressedTime => blockPressedTime;
    public float ParryWindow => parryWindow;
    public float WeavingPostHold => weavingPostHold;
    public float BlockDamageMul => blockDamageMul;
    public float BlockKnockMul => blockKnockMul;

    public void SetAnimator(Animator a) => animator = a; // 필요 시 주입

    // === 가드 입력/AI 제어용 API ===
    public void StartBlock()
    {
        isBlocking = true;
        blockPressedTime = Time.time;
        animator?.SetBool("isBlocking", true);
        if (debugLogs) Debug.Log("[EnemyWeaving] Block start");
    }

    public void StopBlock()
    {
        if (forcedBlockCo != null) return; // 강제 유지 중이면 무시
        isBlocking = false;
        animator?.SetBool("isBlocking", false);
        if (debugLogs) Debug.Log("[EnemyWeaving] Block stop");
    }

    /// 정면 콘 + 패링 윈도우 판정
    public DefenseOutcome Evaluate(Vector2 facing/*적이 보는 방향*/, Vector2 dirToAttacker/*적→공격자*/, bool parryable)
    {
        if (!isBlocking) return DefenseOutcome.None;

        float cosHalf = Mathf.Cos(guardAngle * 0.5f * Mathf.Deg2Rad);
        bool inFront = Vector2.Dot(facing.normalized, dirToAttacker.normalized) >= cosHalf;
        if (!inFront) return DefenseOutcome.None;

        bool canParry = parryable && (Time.time - blockPressedTime) <= parryWindow;
        return canParry ? DefenseOutcome.Parry : DefenseOutcome.Block;
    }

    /// 패링 성공 후 조작(이동/행동) 락이 필요하면 외부(AI)가 이 이벤트를 받아 처리
    public void StartParryLock(float duration)
    {
        if (duration <= 0f) return;
        parryLockEndTime = Mathf.Max(parryLockEndTime, Time.time + duration);
        if (parryLockCo == null) parryLockCo = StartCoroutine(ParryLockRoutine());

        OnParryLockRequested?.Invoke(duration);
        if (debugLogs) Debug.Log($"[EnemyWeaving] ParryLock for {duration:F2}s");
    }

    private IEnumerator ParryLockRoutine()
    {
        while (Time.time < parryLockEndTime) yield return null;
        parryLockCo = null;
    }

    /// 패링 후 일정 시간 가드를 강제로 유지(플레이어 입력이 없더라도)
    public void ForceBlockFor(float seconds)
    {
        if (forcedBlockCo != null) StopCoroutine(forcedBlockCo);
        forcedBlockCo = StartCoroutine(ForceBlockRoutine(seconds));
    }

    private IEnumerator ForceBlockRoutine(float seconds)
    {
        float end = Time.time + Mathf.Max(0f, seconds);
        isBlocking = true;
        animator?.SetBool("isBlocking", true);
        while (Time.time < end) yield return null;

        // AI가 계속 막고 싶다면 바깥에서 StartBlock을 다시 호출
        isBlocking = false;
        animator?.SetBool("isBlocking", false);
        forcedBlockCo = null;
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public enum DefenseOutcome { None, Block, Parry }

public class EnemyWeaving : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;

    [Header("Weaving(Parry) Config")]
    [SerializeField] private float guardAngle = 120f;     // ���� �� ����
    [SerializeField] private float parryWindow = 0.30f;   // ���� ���� �� �и� ��ȿ �ð�
    [SerializeField] private float weavingPostHold = 0.10f; // �и� ���� ���带 ������ �߰� �ð�

    [Header("Block Balance")]
    [SerializeField] private float blockDamageMul = 0f;
    [SerializeField] private float blockKnockMul = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    // ����
    private bool isBlocking = false;
    private float blockPressedTime = -999f;

    private float parryLockEndTime = 0f;
    private Coroutine parryLockCo;
    private Coroutine forcedBlockCo;

    // �ܺΰ� �����ؼ� AI �̵�/�ൿ�� ��װ� �ʹٸ� ���
    public UnityEvent<float> OnParryLockRequested; // duration seconds

    // �б�/Ʃ�׿� ������Ƽ
    public bool IsBlocking => isBlocking;
    public float LastBlockPressedTime => blockPressedTime;
    public float ParryWindow => parryWindow;
    public float WeavingPostHold => weavingPostHold;
    public float BlockDamageMul => blockDamageMul;
    public float BlockKnockMul => blockKnockMul;

    public void SetAnimator(Animator a) => animator = a; // �ʿ� �� ����

    // === ���� �Է�/AI ����� API ===
    public void StartBlock()
    {
        isBlocking = true;
        blockPressedTime = Time.time;
        animator?.SetBool("isBlocking", true);
        if (debugLogs) Debug.Log("[EnemyWeaving] Block start");
    }

    public void StopBlock()
    {
        if (forcedBlockCo != null) return; // ���� ���� ���̸� ����
        isBlocking = false;
        animator?.SetBool("isBlocking", false);
        if (debugLogs) Debug.Log("[EnemyWeaving] Block stop");
    }

    /// ���� �� + �и� ������ ����
    public DefenseOutcome Evaluate(Vector2 facing/*���� ���� ����*/, Vector2 dirToAttacker/*���������*/, bool parryable)
    {
        if (!isBlocking) return DefenseOutcome.None;

        float cosHalf = Mathf.Cos(guardAngle * 0.5f * Mathf.Deg2Rad);
        bool inFront = Vector2.Dot(facing.normalized, dirToAttacker.normalized) >= cosHalf;
        if (!inFront) return DefenseOutcome.None;

        bool canParry = parryable && (Time.time - blockPressedTime) <= parryWindow;
        return canParry ? DefenseOutcome.Parry : DefenseOutcome.Block;
    }

    /// �и� ���� �� ����(�̵�/�ൿ) ���� �ʿ��ϸ� �ܺ�(AI)�� �� �̺�Ʈ�� �޾� ó��
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

    /// �и� �� ���� �ð� ���带 ������ ����(�÷��̾� �Է��� ������)
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

        // AI�� ��� ���� �ʹٸ� �ٱ����� StartBlock�� �ٽ� ȣ��
        isBlocking = false;
        animator?.SetBool("isBlocking", false);
        forcedBlockCo = null;
    }
}

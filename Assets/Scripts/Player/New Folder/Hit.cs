using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyHit : MonoBehaviour, IDamageable, IParryable
{
    [Header("Refs")]
    [SerializeField] private EnemyWeaving weaving;
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private SpriteRenderer spriteForFacing; // 없으면 Facing 수동 설정

    [Header("Facing")]
    [Tooltip("수동으로 바라보는 방향(기본값: 오른쪽). spriteForFacing이 있으면 자동 계산.")]
    [SerializeField] private Vector2 facingFallback = Vector2.right;

    [Header("Vitals")]
    [SerializeField] private float hpMax = 50f;

    [Header("Hit Reaction")]
    [SerializeField] private float baseHitstun = 0.20f;
    [SerializeField] private float blockHitstunMul = 0.5f;
    [SerializeField] private bool knockbackXOnly = true;

    [Header("Animation Variants")]
    [SerializeField] private int hitVariants = 3;      // Hit1..HitN
    [SerializeField] private int weavingVariants = 3;  // Weaving1..WeavingN

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private float hp;
    private bool inHitstun = false;
    private float hitstunEndTime = 0f;
    private float iFrameEndTime = 0f;
    private Coroutine hitstunCo;

    private void Reset()
    {
        if (!weaving) weaving = GetComponent<EnemyWeaving>();
        if (!animator) animator = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!spriteForFacing) spriteForFacing = GetComponentInChildren<SpriteRenderer>();
    }

    private void Awake()
    {
        if (!weaving) weaving = GetComponent<EnemyWeaving>();
        if (!animator) animator = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        hp = hpMax;
    }

    // === IDamageable ===
    // hitDirFromAttacker: 공격자 → 적
    public void ApplyHit(float damage, float knockback, Vector2 hitDirFromAttacker, GameObject attacker)
    {
        if (Time.time < iFrameEndTime) return;

        // 적 → 공격자
        Vector2 dirToAttacker = -hitDirFromAttacker.normalized;

        // 적이 바라보는 방향
        Vector2 facing = GetFacing();

        DefenseOutcome outcome = (weaving != null)
            ? weaving.Evaluate(facing, dirToAttacker, parryable: true)
            : DefenseOutcome.None;

        // === Parry(Weaving) 성공 ===
        if (outcome == DefenseOutcome.Parry)
        {
            if (debugLogs) Debug.Log($"[Enemy] WEAVING OK at t={Time.time:F2}");
            PlayRandomWeaving();

            // 공격자에게 패링 콜백 전달(있으면)
            attacker?.GetComponent<IParryable>()?.OnParried(transform.position);

            // 패링 락/가드 강제 유지
            float windowEnd = (weaving ? weaving.LastBlockPressedTime + weaving.ParryWindow : Time.time);
            float lockTime = Mathf.Max(0f, (windowEnd + (weaving ? weaving.WeavingPostHold : 0.1f)) - Time.time);
            weaving?.StartParryLock(lockTime);
            weaving?.ForceBlockFor(lockTime);

            // 짧은 무적
            iFrameEndTime = Time.time + 0.05f;
            return;
        }

        // === Block ===
        if (outcome == DefenseOutcome.Block)
        {
            float finalDamage = damage * (weaving ? weaving.BlockDamageMul : 0f);
            float finalKnock = knockback * (weaving ? weaving.BlockKnockMul : 0.3f);

            ApplyDamage(finalDamage);
            ApplyKnockback(dirToAttacker, finalKnock);

            // 가드 중 경직(애니는 재생 안 함)
            float stun = baseHitstun * blockHitstunMul;
            StartHitstun(stun, playHitAnim: false);

            animator?.SetTrigger("BlockHit");
            return;
        }

        // === 가드 실패 ===
        ApplyDamage(damage);
        ApplyKnockback(dirToAttacker, knockback);
        StartHitstun(baseHitstun, playHitAnim: true);
    }

    // === IParryable (플레이어에게 패링 당했을 때의 리액션, 선택) ===
    public void OnParried(Vector3 parrySourcePosition)
    {
        // 살짝 뒤로 밀리게(선택)
        Vector2 dir = ((Vector2)transform.position - (Vector2)parrySourcePosition).normalized;
        rb?.AddForce(dir * 6f, ForceMode2D.Impulse);
        // 여기서 스턴/효과음 등 추가 가능
    }

    // === 내부 유틸 ===
    private Vector2 GetFacing()
    {
        if (spriteForFacing != null)
        {
            // 스프라이트가 오른쪽을 기본으로 본다고 가정
            float x = spriteForFacing.flipX ? -1f : 1f;
            return new Vector2(x, 0f);
        }
        return (facingFallback.sqrMagnitude > 0.0001f) ? facingFallback.normalized : Vector2.right;
    }

    private void ApplyDamage(float amount)
    {
        if (amount <= 0f) return;
        hp = Mathf.Max(0f, hp - amount);
        if (debugLogs) Debug.Log($"[Enemy] HP -{amount} → {hp}/{hpMax}");
        if (hp <= 0f) Die();
    }

    private void Die()
    {
        animator?.SetTrigger("Die");
        // 필요 시: Destroy(gameObject, t) 등
        enabled = false;
    }

    private void ApplyKnockback(Vector2 dirToAttacker, float force)
    {
        if (!rb || force <= 0f) return;

        if (knockbackXOnly)
        {
            float x = -Mathf.Sign(dirToAttacker.x);
            if (Mathf.Abs(x) < 0.0001f) x = 1f;
            rb.linearVelocity = new Vector2(x * force, 0f);
        }
        else
        {
            Vector2 push = -dirToAttacker.normalized * force; // 공격자 반대 방향
            rb.linearVelocity = push;
        }
    }

    private void StartHitstun(float duration, bool playHitAnim)
    {
        if (duration <= 0f) return;

        float end = Time.time + duration;
        hitstunEndTime = Mathf.Max(hitstunEndTime, end);
        if (hitstunCo == null) hitstunCo = StartCoroutine(HitstunRoutine());

        if (playHitAnim) PlayRandomHit();
    }

    private IEnumerator HitstunRoutine()
    {
        inHitstun = true;
        while (Time.time < hitstunEndTime) yield return null;
        inHitstun = false;
        hitstunCo = null;
    }

    private void PlayRandomHit()
    {
        if (!animator || hitVariants <= 0) return;
        int idx = Mathf.Clamp(Random.Range(1, hitVariants + 1), 1, hitVariants);
        animator.SetTrigger($"Hit{idx}");
    }

    private void PlayRandomWeaving()
    {
        if (!animator || weavingVariants <= 0) return;
        int idx = Mathf.Clamp(Random.Range(1, weavingVariants + 1), 1, weavingVariants);
        animator.SetTrigger($"Weaving{idx}");
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMoveBehaviour moveRef; // 같은 오브젝트면 자동 할당 권장
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;

    // --------- Vitals (체력 / 스태미너) ---------
    [Header("Vitals")]
    [SerializeField] private float hpMax = 100f;
    [SerializeField] private float staminaMax = 100f;
    [SerializeField] private float staminaRegenPerSec = 25f; // 비방어 시 초당 회복
    [SerializeField] private float staminaBreakTime = 1.5f;  // 브레이크(스턴) 지속

    public float Hp01 => Mathf.Clamp01(hp / Mathf.Max(1f, hpMax));
    public float Stamina01 => Mathf.Clamp01(stamina / Mathf.Max(1f, staminaMax));

    public event Action<float, float> OnHealthChanged;   // (current, max)
    public event Action<float, float> OnStaminaChanged;  // (current, max)

    private float hp;
    private float stamina;

    private bool staminaBroken;
    private float staminaBreakEndTime;

    // --------- Guard / Parry (기존 로직 유지) ---------
    [Header("Guard / Parry")]
    [SerializeField] private float guardAngle = 120f;     // 정면 콘 각도
    [SerializeField] private float parryWindow = 0.3f;   // 누른 직후 패링 윈도우
    [SerializeField] private float blockDamageMul = 0f;
    [SerializeField] private float blockKnockMul = 0.3f;  // 방어시 넉백 계수

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private bool isBlocking;
    private float blockPressedTime = -999f;

    // --------- Attack (콤보/차지) ---------
    [Header("Attack - Combo")]
    [SerializeField] private int maxCombo = 5;
    [SerializeField] private float comboGapMax = 0.6f;
    [SerializeField] private float bufferWindow = 0.35f;
    [SerializeField] private bool lockMoveDuringAttack = true;

    [Header("Attack - Hitbox")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private float baseDamage = 10f;
    [SerializeField] private float baseKnockback = 6f;
    [SerializeField] private float baseRange = 0.9f;
    [SerializeField] private float baseRadius = 0.6f;

    [Header("Combo Timings (sec)")]
    [SerializeField] private float windup = 0.08f;
    [SerializeField] private float active = 0.06f;
    [SerializeField] private float recovery = 0.12f;

    [Header("Charge Attack")]
    [SerializeField] private float chargeTime = 0.5f;
    [SerializeField] private float chargeDamageMul = 3.0f;
    [SerializeField] private float chargeKnockMul = 2.0f;
    [SerializeField] private float chargeRangeMul = 1.2f;
    [SerializeField] private float chargeRadiusMul = 1.2f;

    private int comboIndex = 0;
    private bool isAttacking = false;
    private bool nextBuffered = false;
    private float lastAttackEndTime = -999f;

    private bool attackHeld = false;
    private float attackPressedTime = -999f;
    private Coroutine chargeCo;

    // === Hit Reaction ===
    [Header("Hit Reaction")]
    [SerializeField] private float baseHitstun = 0.20f; // 일반 피격 경직 시간
    [SerializeField] private float blockHitstunMul = 0.5f; // 가드시 경직 감소 배수
    private float hitstunEndTime = 0f;

    private bool inHitstun = false; 
    private float iFrameEndTime = 0f;
    private Coroutine hitstunCo;
    private Coroutine attackCo; // 공격 코루틴 트래킹(경직 시 취소용)


    // --------- Collision (선택: 공격 활성 중 충돌 무시) ---------
    [Header("Collision (optional)")]
    [SerializeField] private bool ignoreEnemyCollisionDuringActive = true;
    [SerializeField] private float extraIgnoreTime = 0.02f;
    private Collider2D[] myColliders;

    // --------- Input ---------
    private PlayerMove inputWrapper; // .inputactions 래퍼 재사용
    private InputAction blockAction; // "Block"
    private InputAction attackAction; // "Attack"

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();

        // 체력/스태미너 초기화
        hp = hpMax;
        stamina = staminaMax;
        OnHealthChanged?.Invoke(hp, hpMax);
        OnStaminaChanged?.Invoke(stamina, staminaMax);

        inputWrapper = new PlayerMove();
        myColliders = GetComponents<Collider2D>();
    }

    private void OnEnable()
    {
        inputWrapper.Enable();

        // Block
        blockAction = inputWrapper.asset.FindAction("Block");
        if (blockAction != null)
        {
            blockAction.started += OnBlockStarted;
            blockAction.canceled += OnBlockCanceled;
        }
        else
        {
            Debug.LogWarning("[PlayerCombat] 'Block' 액션이 없습니다. .inputactions에 추가하세요.");
        }

        // Attack
        attackAction = inputWrapper.asset.FindAction("Attack");
        if (attackAction != null)
        {
            attackAction.started += OnAttackStarted;
            attackAction.canceled += OnAttackCanceled;
        }
        else
        {
            Debug.LogWarning("[PlayerCombat] 'Attack' 액션이 없습니다. .inputactions에 추가하세요.");
        }

        StartCoroutine(VitalsTick());
    }

    private void OnDisable()
    {
        if (blockAction != null)
        {
            blockAction.started -= OnBlockStarted;
            blockAction.canceled -= OnBlockCanceled;
        }
        if (attackAction != null)
        {
            attackAction.started -= OnAttackStarted;
            attackAction.canceled -= OnAttackCanceled;
        }
        inputWrapper.Disable();
        StopAllCoroutines();
    }

    // ====== Block / Parry ======
    private void OnBlockStarted(InputAction.CallbackContext ctx)
    {
        // 스태미너 브레이크 때만 막고, 히트스턴 중에도 가드는 가능하게 둠
        if (staminaBroken) return;

        isBlocking = true;
        blockPressedTime = Time.time;
        if (animator) animator.SetBool("isBlocking", true);
    }


    private void OnBlockCanceled(InputAction.CallbackContext ctx)
    {
        isBlocking = false;
        if (animator) animator.SetBool("isBlocking", false);
    }

    // ====== 체력/스태미너 틱 ======
    private IEnumerator VitalsTick()
    {
        var wait = new WaitForEndOfFrame();
        while (true)
        {
            // 스태미너 브레이크 해제
            if (staminaBroken && Time.time >= staminaBreakEndTime)
            {
                staminaBroken = false;

                // 이동 잠금 해제
                if (moveRef) moveRef.SetMovementLocked(false, true);

                if (animator) animator.SetBool("GuardBroken", false); // 기존 파라미터 이름 유지
            }

            // 비방어 중 스태미너 회복
            if (!isBlocking && !staminaBroken && stamina < staminaMax)
            {
                stamina = Mathf.Min(staminaMax, stamina + staminaRegenPerSec * Time.deltaTime);
                OnStaminaChanged?.Invoke(stamina, staminaMax);
            }

            yield return wait;
        }
    }

    private void StaminaBreak()
    {
        staminaBroken = true;
        isBlocking = false;
        staminaBreakEndTime = Time.time + staminaBreakTime;

        if (moveRef) moveRef.SetMovementLocked(true, true);

        if (animator)
        {
            animator.SetBool("GuardBroken", true);   // 애니 파라미터 이름을 바꾸고 싶다면 Animator에서도 교체
            animator.SetTrigger("GuardBreak");
        }
    }

    // hitDir: "적 → 플레이어" 방향, hitstun: 적이 주는 경직(초). 음수면 기본값 사용.
    public void OnHit(float damage, float knockback, Vector2 hitDir, bool parryable, GameObject attacker = null, float hitstun = -1f)
    {
        // i-프레임 동안은 무시
        if (Time.time < iFrameEndTime) return;

        // 정면 콘 판정
        Vector2 facing = (moveRef != null && moveRef.LastFacing.sqrMagnitude > 0f) ? moveRef.LastFacing : Vector2.right;
        Vector2 inFrontDir = -hitDir.normalized; // 적→플레이어의 반대 = 플레이어→적
        float cosHalf = Mathf.Cos(guardAngle * 0.5f * Mathf.Deg2Rad);
        bool inFront = Vector2.Dot(facing, inFrontDir) >= cosHalf;

        // 가드 성공 분기
        if (!staminaBroken && isBlocking && inFront)
        {
            bool canParry = parryable && (Time.time - blockPressedTime) <= parryWindow;

            if (canParry)
            {
                if (debugLogs)
                    Debug.Log($"[PARRY OK] t={Time.time:F2}s, dt={(Time.time - blockPressedTime):F3}s, attacker={(attacker ? attacker.name : "null")}");

                if (animator) animator.SetTrigger("Parry");

                var parryableTarget = attacker ? attacker.GetComponent<IParryable>() : null;
                if (parryableTarget != null)
                    parryableTarget.OnParried(transform.position);

                // (선택) 패링 직후 짧은 i-프레임을 주고 싶으면 열어두세요.
                iFrameEndTime = Time.time + 0.05f;
                return;
            }
            else
            {
                // 일반 가드: 칩 데미지 + 넉백 감소 + 스태미너 소모
                float finalDamage = damage * blockDamageMul;
                float finalKnock = knockback * blockKnockMul;

                ApplyDamage(finalDamage);
                ApplyKnockback(inFrontDir, finalKnock);

                stamina -= damage; // 튜닝 가능(계수 곱 등)
                OnStaminaChanged?.Invoke(stamina, staminaMax);

                if (stamina <= 0f)
                {
                    StaminaBreak();
                }
                else
                {
                    float stun = (hitstun >= 0f ? hitstun : baseHitstun) * blockHitstunMul;
                    StartHitstun(stun);
                }

                if (animator) animator.SetTrigger("BlockHit");
                return;
            }
        }

        // 가드 실패/측후방/브레이크/비방어
        ApplyDamage(damage);
        ApplyKnockback(inFrontDir, knockback);
        float stunRaw = (hitstun >= 0f ? hitstun : baseHitstun);
        StartHitstun(stunRaw);
    }


    private void ApplyDamage(float amount)
    {
        if (amount <= 0f) return;
        hp = Mathf.Max(0f, hp - amount);
        OnHealthChanged?.Invoke(hp, hpMax);

        if (debugLogs)
            Debug.Log($"[HP] -{amount} (now {hp}/{hpMax})");

        if (hp <= 0f)
            OnDeath();
    }

    private void OnDeath()
    {
        // TODO: 사망 처리(리스폰, 게임오버 UI 등)
        if (debugLogs) Debug.Log("[Player] DEAD");
        // 예시: 조작 잠금 + 애니 트리거
        if (moveRef) moveRef.SetMovementLocked(true, true);
        if (animator) animator.SetTrigger("Die");
        // Destroy(gameObject); // 원하면 파괴
    }

    // PlayerCombat.cs
    private void ApplyKnockback(Vector2 dirToEnemy, float force)
    {
        if (force <= 0f || rb == null) return;
        float x = -Mathf.Sign(dirToEnemy.x);

        // 만약 거의 수직으로 맞아 X가 0에 가깝다면, 마지막 바라보는 방향으로 밀기
        if (Mathf.Abs(x) < 0.0001f)
            x = (moveRef && Mathf.Abs(moveRef.LastFacing.x) > 0.0001f) ? Mathf.Sign(moveRef.LastFacing.x) : 1f;

        rb.linearVelocity = new Vector2(x * force, 0f);
    }



    // ====== Attack 입력 ======
    private void OnAttackStarted(InputAction.CallbackContext ctx)
    {
        // 히트스턴/스태미너 브레이크 중에는 공격 시작 불가
        if (staminaBroken || inHitstun) return;

        attackHeld = true;
        attackPressedTime = Time.time;

        if (chargeCo != null) StopCoroutine(chargeCo);
        chargeCo = StartCoroutine(CheckChargeReady());
    }


    private void OnAttackCanceled(InputAction.CallbackContext ctx)
    {
        attackHeld = false;
        if (chargeCo != null) { StopCoroutine(chargeCo); chargeCo = null; }
        if (animator) animator.SetBool("Charging", false);

        if (staminaBroken) return;

        if (isAttacking)
        {
            nextBuffered = true;
        }
        else
        {
            if (Time.time - lastAttackEndTime > comboGapMax)
                comboIndex = 0;

            attackCo = StartCoroutine(DoAttackStep(comboIndex));
        }
    }

    private IEnumerator CheckChargeReady()
    {
        float t0 = Time.time;
        while (attackHeld && !isAttacking)
        {
            float held = Time.time - t0;
            if (held >= chargeTime)
            {
                if (animator) animator.SetBool("Charging", true);
                if (!isAttacking)
                    attackCo = StartCoroutine(DoChargeAttack());

                yield break;
            }
            yield return null;
        }
    }

    // ====== Attack 로직 ======
    private IEnumerator DoAttackStep(int step)
    {
        isAttacking = true;
        nextBuffered = false;

        if (lockMoveDuringAttack && moveRef) moveRef.SetMovementLocked(true, false);
        if (animator) animator.SetTrigger($"Atk{Mathf.Clamp(step + 1, 1, maxCombo)}");

        // 예비
        yield return new WaitForSeconds(windup);

        // 활성: 히트박스
        DoHitbox(baseDamage * DamageMulByStep(step),
                 baseKnockback * KnockbackMulByStep(step),
                 baseRange * RangeMulByStep(step),
                 baseRadius * RadiusMulByStep(step));

        yield return new WaitForSeconds(active);

        // 후딜
        yield return new WaitForSeconds(recovery);

        if (lockMoveDuringAttack && moveRef) moveRef.SetMovementLocked(false, false);

        isAttacking = false;
        lastAttackEndTime = Time.time;

        if (nextBuffered && step < maxCombo - 1 && (Time.time - attackPressedTime) <= (active + recovery + bufferWindow + 0.2f))
        {
            comboIndex = step + 1;
            StartCoroutine(DoAttackStep(comboIndex));
        }
        else
        {
            comboIndex = 0;
            nextBuffered = false;
        }
        attackCo = null;
    }

    private IEnumerator DoChargeAttack()
    {
        isAttacking = true;

        if (lockMoveDuringAttack && moveRef) moveRef.SetMovementLocked(true, false);
        if (animator)
        {
            animator.ResetTrigger("Atk1"); animator.ResetTrigger("Atk2");
            animator.ResetTrigger("Atk3"); animator.ResetTrigger("Atk4");
            animator.ResetTrigger("Atk5");
            animator.SetBool("Charging", false);
            animator.SetTrigger("AtkCharge");
        }

        yield return new WaitForSeconds(windup + 0.07f);

        DoHitbox(baseDamage * chargeDamageMul,
                 baseKnockback * chargeKnockMul,
                 baseRange * chargeRangeMul,
                 baseRadius * chargeRadiusMul);

        yield return new WaitForSeconds(active + recovery);

        if (lockMoveDuringAttack && moveRef) moveRef.SetMovementLocked(false, false);

        isAttacking = false;
        lastAttackEndTime = Time.time;
        comboIndex = 0;
        attackCo = null;
    }

    private void CancelOffense()
    {
        // 공격/차지 중이면 중단
        if (attackCo != null) { StopCoroutine(attackCo); attackCo = null; }
        if (chargeCo != null) { StopCoroutine(chargeCo); chargeCo = null; }
        isAttacking = false;
        nextBuffered = false;
        comboIndex = 0;

        // 차지 애니 상태 정리
        if (animator)
        {
            animator.SetBool("Charging", false);
            // 필요하면 공격 트리거 리셋 추가 가능
            // animator.ResetTrigger("Atk1"); ... 등
        }

        // 이동 잠금 해제(공격 중 잠갔던 경우)
        if (moveRef) moveRef.SetMovementLocked(false, false);
    }

    private void StartHitstun(float duration)
    {
        if (duration <= 0f) return;

        CancelOffense(); // 공격/차지 중단

        // 경직 종료 시각을 누적 갱신(중첩 히트 안전)
        float end = Time.time + duration;
        hitstunEndTime = Mathf.Max(hitstunEndTime, end);

        if (hitstunCo == null)
            hitstunCo = StartCoroutine(HitstunRoutine());

        // ★ 이동은 막되 물리는 살리기: hardFreezePhysics=false, zeroVelocity=false
        if (moveRef) moveRef.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: false);

        if (animator) animator.SetTrigger("Hit");
    }


    // HitstunRoutine 시작 부분
    private IEnumerator HitstunRoutine()
    {
        inHitstun = true;
        // hitstunEndTime 까지 대기(여러 번 맞아도 한 코루틴만 유지)
        while (Time.time < hitstunEndTime)
            yield return null;

        inHitstun = false;
        if (moveRef) moveRef.SetMovementLocked(false, hardFreezePhysics: false); // 해제
        hitstunCo = null;
    }




    // ====== 히트박스 & 충돌 무시 ======
    private void DoHitbox(float dmg, float knock, float range, float radius)
    {
        Vector2 facing = (moveRef != null && moveRef.LastFacing.sqrMagnitude > 0f)
            ? moveRef.LastFacing
            : Vector2.right;

        Vector2 center = (Vector2)transform.position + facing.normalized * range;

        if (debugLogs)
            Debug.Log($"[HITBOX] dmg={dmg}, knock={knock}, center={center}, r={radius}");

        var hits = Physics2D.OverlapCircleAll(center, radius, enemyMask);
        var hitSet = new HashSet<Collider2D>();

        foreach (var h in hits)
        {
            if (h == null || hitSet.Contains(h)) continue;
            hitSet.Add(h);

            if (ignoreEnemyCollisionDuringActive)
                IgnoreCollisionsWith(h.transform.root, active + extraIgnoreTime);

            Vector2 toEnemy = ((Vector2)h.transform.position - (Vector2)transform.position).normalized;

            var dmgTarget = h.GetComponentInParent<IDamageable>();
            if (dmgTarget != null)
            {
                dmgTarget.ApplyHit(dmg, knock, toEnemy, gameObject);
            }
            else if (debugLogs)
            {
                Debug.Log($"[HIT] {h.name} take {dmg}, knock={knock}");
            }
        }
    }

    private void IgnoreCollisionsWith(Transform enemyRoot, float seconds)
    {
        if (myColliders == null) return;
        var enemyCols = enemyRoot.GetComponentsInChildren<Collider2D>();
        foreach (var my in myColliders)
        {
            if (!my) continue;
            foreach (var ec in enemyCols)
            {
                if (!ec) continue;
                Physics2D.IgnoreCollision(my, ec, true);
                StartCoroutine(ReenableCollisionLater(my, ec, seconds));
            }
        }
    }

    private IEnumerator ReenableCollisionLater(Collider2D a, Collider2D b, float t)
    {
        yield return new WaitForSeconds(t);
        if (a && b) Physics2D.IgnoreCollision(a, b, false);
    }

    // ====== 스텝별 가중치 ======
    private float DamageMulByStep(int step) => 1f + 0.1f * step;
    private float KnockbackMulByStep(int step) => 1f + 0.1f * step;
    private float RangeMulByStep(int step) => 1f;
    private float RadiusMulByStep(int step) => 1f;
}

// 선택: 적 패링 반응
public interface IParryable
{
    void OnParried(Vector3 parrySourcePosition);
}

// 데미지 인터페이스(적 등에 부착)
public interface IDamageable
{
    void ApplyHit(float damage, float knockback, Vector2 hitDirFromPlayer, GameObject attacker);
}

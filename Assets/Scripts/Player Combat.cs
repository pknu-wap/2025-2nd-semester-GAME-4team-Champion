using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMoveBehaviour moveRef; // 같은 오브젝트에 있으면 자동 할당 권장
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;

    [Header("Guard / Parry")]
    [SerializeField] private float guardAngle = 120f;     // 정면 콘 각도
    [SerializeField] private float parryWindow = 0.15f;   // 누른 직후 패링 윈도우
    [SerializeField] private float blockDamageMul = 0.2f; // 방어시 피해 계수(20%)
    [SerializeField] private float blockKnockMul = 0.3f;  // 방어시 넉백 계수
    [SerializeField] private float stamina = 100f;       // 가드 게이지
    [SerializeField] private float guardRegenPerSec = 25f;// 비방어 회복량
    [SerializeField] private float guardBreakTime = 1.5f; // 브레이크 시간

    private float guard;
    private bool isBlocking;
    private bool guardBroken;
    private float guardBreakEndTime;
    private float blockPressedTime = -999f;

    // 입력
    private PlayerMove inputWrapper;     // .inputactions가 생성한 래퍼 재사용
    private InputAction blockAction;     // "Block" 액션

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

        guard = stamina;

        // 입력 래퍼 준비
        inputWrapper = new PlayerMove();
    }

    private void OnEnable()
    {
        inputWrapper.Enable();

        // 안전하게 이름으로 찾기(맵이 뭐든 "Block"만 있으면 됨)
        blockAction = inputWrapper.asset.FindAction("Block");
        if (blockAction != null)
        {
            blockAction.started += OnBlockStarted;
            blockAction.canceled += OnBlockCanceled;
        }

        StartCoroutine(GuardTick());
    }

    private void OnDisable()
    {
        if (blockAction != null)
        {
            blockAction.started -= OnBlockStarted;
            blockAction.canceled -= OnBlockCanceled;
        }
        inputWrapper.Disable();
        StopAllCoroutines();
    }

    // === Block 입력 콜백 ===
    private void OnBlockStarted(InputAction.CallbackContext ctx)
    {
        if (guardBroken) return;
        isBlocking = true;
        blockPressedTime = Time.time;
        if (animator) animator.SetBool("isBlocking", true);
        Debug.Log("Blocking");
    }

    private void OnBlockCanceled(InputAction.CallbackContext ctx)
    {
        isBlocking = false;
        Debug.Log("blocked");
        if (animator) animator.SetBool("isBlocking", false);
    }

    // === 가드 게이지 / 브레이크 틱 ===
    private IEnumerator GuardTick()
    {
        var wait = new WaitForEndOfFrame();
        while (true)
        {
            // 브레이크 복구
            if (guardBroken && Time.time >= guardBreakEndTime)
            {
                guardBroken = false;

                // ★ 이동 잠금 해제
                if (moveRef) moveRef.SetMovementLocked(false, hardFreezePhysics: true);

                if (animator) animator.SetBool("GuardBroken", false);
                // guard = Mathf.Max(guard, guardMax * 0.5f); // 원하면 부분 회복
            }


            // 비방어 중 회복
            if (!isBlocking && !guardBroken && guard < stamina)
                guard = Mathf.Min(stamina, guard + guardRegenPerSec * Time.deltaTime);

            yield return wait;
        }
    }

    private void GuardBreak()
    {
        guardBroken = true;
        isBlocking = false;
        guardBreakEndTime = Time.time + guardBreakTime;

        // ★ 이동 잠금
        if (moveRef) moveRef.SetMovementLocked(true, hardFreezePhysics: true);

        if (animator)
        {
            animator.SetBool("GuardBroken", true);
            animator.SetTrigger("GuardBreak");
        }
    }


    // === 적 히트가 들어올 때 외부에서 호출 ===
    // hitDir: "적 → 플레이어" 방향, parryable: 패링 가능 여부
    public void OnHit(float damage, float knockback, Vector2 hitDir, bool parryable, GameObject attacker = null)
    {
        Vector2 facing = (moveRef != null && moveRef.LastFacing.sqrMagnitude > 0f)
            ? moveRef.LastFacing
            : Vector2.right;

        // 정면 콘 체크: 플레이어 정면(facing)과 "플레이어 → 적" 방향의 내적
        Vector2 inFrontDir = -hitDir.normalized; // 적→플레이어의 반대 = 플레이어→적
        float cosHalf = Mathf.Cos(guardAngle * 0.5f * Mathf.Deg2Rad);
        bool inFront = Vector2.Dot(facing, inFrontDir) >= cosHalf;

        if (!guardBroken && isBlocking && inFront)
        {
            bool canParry = parryable && (Time.time - blockPressedTime) <= parryWindow;

            if (canParry)
            {
                // 패링 성공
                if (animator) animator.SetTrigger("Parry");

                // 공격자에 패링 알림(있으면)
                var parryableTarget = attacker ? attacker.GetComponent<IParryable>() : null;
                if (parryableTarget != null)
                    parryableTarget.OnParried(transform.position);

                return;
            }
            else
            {
                // 일반 방어
                float finalDamage = damage * blockDamageMul;
                float finalKnock = knockback * blockKnockMul;

                ApplyDamage(finalDamage);
                ApplyKnockback(inFrontDir, finalKnock);

                guard -= damage; // 데미지 기반 소모(원하면 튜닝)
                if (guard <= 0f) GuardBreak();

                if (animator) animator.SetTrigger("BlockHit");
                return;
            }
        }

        // 방어 실패(측면/후방/브레이크/비방어)
        ApplyDamage(damage);
        ApplyKnockback(inFrontDir, knockback);
    }

    private void ApplyDamage(float amount)
    {
        // TODO: 체력 시스템 연결
        // Debug.Log($"Damage {amount}");
    }

    private void ApplyKnockback(Vector2 dirToEnemy, float force)
    {
        if (force <= 0f || rb == null) return;
        Vector2 pushDir = -dirToEnemy.normalized; // 적과 반대 방향으로 밀림
        rb.AddForce(pushDir * force, ForceMode2D.Impulse);
    }
}

// (선택) 적이 패링되었을 때 반응하고 싶다면 사용
public interface IParryable
{
    void OnParried(Vector3 parrySourcePosition);
}

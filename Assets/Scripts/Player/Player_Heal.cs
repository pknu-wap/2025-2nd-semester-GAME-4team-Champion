using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player_Heal : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private float healDuration = 0.5f; // R키로 회복 유지 시간
    [SerializeField] private float healAmount = 35f;    // 회복량

    [Header("Refs")]
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;

    private Coroutine healCo;
    public bool IsHealing { get; private set; }

    // ★ 입력 시스템 래퍼/액션
    private PlayerMove inputWrapper;
    private InputAction healAction;  // "Heal" 액션 (R 키 바인딩)

    public void Bind(PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    {
        combat = c;
        moveRef = m;
        animator = a;
    }

    private void Reset()
    {
        if (!combat) combat = GetComponent<PlayerCombat>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        inputWrapper = new PlayerMove();
    }

    private void OnEnable()
    {
        inputWrapper.Enable();
        // ★ Combat 맵을 확실히 활성화 (혹시 맵 자동 전환 쓰는 경우 대비)
        inputWrapper.Combat.Enable();

        healAction = inputWrapper.asset.FindAction("Heal");
        if (healAction != null)
        {
            // 변경: started → performed
            healAction.performed += OnHealPerformed;
        }
        else
        {
            Debug.LogWarning("[Player_Heal] 'Heal' 액션이 없습니다. .inputactions에 추가하고 R키에 바인딩하세요.");
        }
    }


    private void OnDisable()
    {
        if (healAction != null)
        {
            healAction.started -= OnHealPerformed;
        }
        inputWrapper.Disable();

        // 진행 중이면 종료 정리
        if (healCo != null) StopCoroutine(healCo);
        IsHealing = false;
        if (animator) animator.SetTrigger("HealStart");
        moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
        healCo = null;
    }

    private void OnHealPerformed(InputAction.CallbackContext _)
    {
        if (IsHealing) return;
        // (필요시) 전역 락 체크를 여기서 막을 수도 있음
        // if (combat != null && combat.IsActionLocked) return;
        StartHealing();
    }

    public void StartHealing()
    {
        if (IsHealing) return;
        healCo = StartCoroutine(HealRoutine());
    }

    public void CancelHealing()
    {
        if (!IsHealing) return;
        if (healCo != null) StopCoroutine(healCo);
        healCo = null;
        IsHealing = false;

        moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
    }

    private IEnumerator HealRoutine()
    {
        IsHealing = true;
        combat?.BlockStaminaRegenFor(healDuration);
        // ★ 전역 행동 락: 힐 동안 아무 행동도 못하게
        combat?.StartActionLock(healDuration, zeroVelocityOnStart: true);

        // 완전 무방비: 이동 락(물리 정지 권장)
        moveRef?.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: true);
        if (animator) animator.SetTrigger("HealStart");

        yield return new WaitForSeconds(healDuration);

        // 실제 회복
        if (combat != null) combat.Heal(+healAmount);

        // 종료 정리
        IsHealing = false;
        moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
        healCo = null;
    }
}

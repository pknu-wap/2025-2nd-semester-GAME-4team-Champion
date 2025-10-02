using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player_Heal : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private float healDuration = 0.5f; // RŰ�� ȸ�� ���� �ð�
    [SerializeField] private float healAmount = 35f;    // ȸ����

    [Header("Refs")]
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;

    private Coroutine healCo;
    public bool IsHealing { get; private set; }

    // �� �Է� �ý��� ����/�׼�
    private PlayerMove inputWrapper;
    private InputAction healAction;  // "Heal" �׼� (R Ű ���ε�)

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
        // �� Combat ���� Ȯ���� Ȱ��ȭ (Ȥ�� �� �ڵ� ��ȯ ���� ��� ���)
        inputWrapper.Combat.Enable();

        healAction = inputWrapper.asset.FindAction("Heal");
        if (healAction != null)
        {
            // ����: started �� performed
            healAction.performed += OnHealPerformed;
        }
        else
        {
            Debug.LogWarning("[Player_Heal] 'Heal' �׼��� �����ϴ�. .inputactions�� �߰��ϰ� RŰ�� ���ε��ϼ���.");
        }
    }


    private void OnDisable()
    {
        if (healAction != null)
        {
            healAction.started -= OnHealPerformed;
        }
        inputWrapper.Disable();

        // ���� ���̸� ���� ����
        if (healCo != null) StopCoroutine(healCo);
        IsHealing = false;
        if (animator) animator.SetTrigger("HealStart");
        moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
        healCo = null;
    }

    private void OnHealPerformed(InputAction.CallbackContext _)
    {
        if (IsHealing) return;
        // (�ʿ��) ���� �� üũ�� ���⼭ ���� ���� ����
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
        // �� ���� �ൿ ��: �� ���� �ƹ� �ൿ�� ���ϰ�
        combat?.StartActionLock(healDuration, zeroVelocityOnStart: true);

        // ���� �����: �̵� ��(���� ���� ����)
        moveRef?.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: true);
        if (animator) animator.SetTrigger("HealStart");

        yield return new WaitForSeconds(healDuration);

        // ���� ȸ��
        if (combat != null) combat.Heal(+healAmount);

        // ���� ����
        IsHealing = false;
        moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
        healCo = null;
    }
}

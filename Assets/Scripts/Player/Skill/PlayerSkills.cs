using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum SkillId { PowerStrike, Combination /*, Uppercut ... */ }

[DisallowMultipleComponent]
public class PlayerSkills : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerAttack attack;
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;

    [Header("Input Action Names (1/2/3)")]
    [SerializeField] private string skill1ActionName = "Skill1";
    [SerializeField] private string skill2ActionName = "Skill2";
    [SerializeField] private string skill3ActionName = "Skill3";

    [Header("Options")]
    [SerializeField] private bool autoLearnOnStart = false;     // ���� �� �ʱ� ��ų �ڵ� ����
    [SerializeField] private bool debugLogs = false;

    [Header("Initial Skills (Optional)")]
    [Tooltip("�� ������Ʈ �Ǵ� ������(�������̸� ��Ÿ�ӿ� �÷��̾� �ڽ����� ������)")]
    [SerializeField] private MonoBehaviour[] initialSkills; // IPlayerSkill ������Ʈ��

    // ---- Input ----
    private PlayerMove inputWrapper;
    private InputAction[] actions = new InputAction[3];
    private readonly string[] actionNames = new string[3];

    // ---- Slots (���� 3ĭ) ----
    private readonly IPlayerSkill[] slots = new IPlayerSkill[3];

    // ==== Unity Lifecycle ====
    private void Reset()
    {
        if (!attack) attack = GetComponent<PlayerAttack>();
        if (!combat) combat = GetComponent<PlayerCombat>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (!attack) attack = GetComponent<PlayerAttack>();
        if (!combat) combat = GetComponent<PlayerCombat>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();

        inputWrapper = new PlayerMove();

        actionNames[0] = skill1ActionName;
        actionNames[1] = skill2ActionName;
        actionNames[2] = skill3ActionName;
    }

    private void Start()
    {
        if (!autoLearnOnStart || initialSkills == null) return;

        // �ν����ͷ� ���� �ʱ� ��ų�� ����(������/�ܺο��� ���� ����)
        foreach (var mb in initialSkills)
        {
            if (mb is IPlayerSkill s) AcquireSkill(s);
        }
    }

    private void OnEnable()
    {
        inputWrapper?.Enable();

        // �׼� ���ε�
        BindAction(0, OnSkill1Performed, OnSkill1Canceled);
        BindAction(1, OnSkill2Performed, OnSkill2Canceled);
        BindAction(2, OnSkill3Performed, OnSkill3Canceled);
    }

    private void OnDisable()
    {
        // �׼� ����
        UnbindAction(0, OnSkill1Performed, OnSkill1Canceled);
        UnbindAction(1, OnSkill2Performed, OnSkill2Canceled);
        UnbindAction(2, OnSkill3Performed, OnSkill3Canceled);

        inputWrapper?.Disable();
    }

    // ==== Public API ====

    /// <summary>
    /// ��Ÿ�ӿ� ��ų�� ����. �� ����(1��2��3)�� ������� �����մϴ�.
    /// �ܺ�/�������̾ �÷��̾� �ڽ����� ���� �����Ǿ� ������ ����˴ϴ�.
    /// </summary>
    public bool AcquireSkill(IPlayerSkill skill)
    {
        if (skill == null) return false;

        // ��ü �� ���� ����
        var mb = skill as MonoBehaviour;
        if (!mb)
        {
            if (debugLogs) Debug.LogWarning("[PlayerSkills] skill is not a MonoBehaviour");
            return false;
        }

        bool isInScene = mb.gameObject.scene.IsValid();
        bool isUnderPlayer = isInScene && mb.transform.root == transform.root;

        if (!isInScene || !isUnderPlayer)
        {
            // ������/�ܺ� ������Ʈ �� �÷��̾� �ڽ����� ����
            var clone = Instantiate(mb.gameObject, transform);
            clone.name = mb.gameObject.name;
            var cloned = clone.GetComponent<IPlayerSkill>();
            if (cloned == null)
            {
                Destroy(clone);
                if (debugLogs) Debug.LogWarning("[PlayerSkills] Cloned object has no IPlayerSkill");
                return false;
            }
            skill = cloned;
            mb = clone.GetComponent<MonoBehaviour>();
        }

        // �ߺ� ����
        for (int i = 0; i < 3; i++)
            if (ReferenceEquals(slots[i], skill))
                return false;

        // �� ���� ã��
        int slot = FirstEmptySlot();
        if (slot < 0)
        {
            if (debugLogs) Debug.LogWarning("[PlayerSkills] ���� ���� ��(3/3).");
            return false;
        }

        slots[slot] = skill;
        if (debugLogs) Debug.Log($"[Skills] Learned {skill.SkillName} �� Slot {slot + 1}");

        return true;
    }

    /// <summary>���Կ� �� ��ų ��ȯ(������ null)</summary>
    public IPlayerSkill GetSkillInSlot(int slotIndex)
    {
        return (slotIndex >= 0 && slotIndex < 3) ? slots[slotIndex] : null;
    }

    /// <summary>��� ���� ����(������ ����, ������Ʈ�� ����)</summary>
    public void ResetLearned()
    {
        for (int i = 0; i < 3; i++) slots[i] = null;
    }

    // ==== Input ���ε� ��ƿ ====
    private void BindAction(int slotIndex,
        Action<InputAction.CallbackContext> onPerformed,
        Action<InputAction.CallbackContext> onCanceled)
    {
        if (inputWrapper == null) return;
        var map = inputWrapper.asset;
        if (map == null) return;

        string name = actionNames[slotIndex];
        if (string.IsNullOrEmpty(name)) return;

        var action = map.FindAction(name);
        actions[slotIndex] = action;

        if (action != null)
        {
            action.performed += onPerformed;
            action.canceled += onCanceled;
        }
        else if (debugLogs)
        {
            Debug.LogWarning($"[PlayerSkills] Input action '{name}' �� ã�� �� �����ϴ�.");
        }
    }

    private void UnbindAction(int slotIndex,
        Action<InputAction.CallbackContext> onPerformed,
        Action<InputAction.CallbackContext> onCanceled)
    {
        var action = actions[slotIndex];
        if (action == null) return;

        action.performed -= onPerformed;
        action.canceled -= onCanceled;
        actions[slotIndex] = null;
    }

    // ==== ���� ����� ====
    private void OnSkill1Performed(InputAction.CallbackContext _) => HandlePress(0);
    private void OnSkill2Performed(InputAction.CallbackContext _) => HandlePress(1);
    private void OnSkill3Performed(InputAction.CallbackContext _) => HandlePress(2);

    private void OnSkill1Canceled(InputAction.CallbackContext _) => HandleRelease(0);
    private void OnSkill2Canceled(InputAction.CallbackContext _) => HandleRelease(1);
    private void OnSkill3Canceled(InputAction.CallbackContext _) => HandleRelease(2);

    private void HandlePress(int slotIndex)
    {
        var skill = GetSkillInSlot(slotIndex);
        if (skill == null || attack == null || combat == null || moveRef == null || animator == null) return;

        // ������ �켱
        if (skill is IChargeSkill charge)
        {
            bool ok = charge.TryStartCharge(attack, combat, moveRef, animator);
            if (debugLogs && ok) Debug.Log($"[Skills] Charge Start {skill.SkillName} (slot {slotIndex + 1})");
            return;
        }

        // �Ϲ���
        bool castOk = skill.TryCastSkill(attack, combat, moveRef, animator);
        if (castOk)
        {
            attack.FreezeComboTimerFor(skill.GetTotalDuration() + 0.05f);
            if (debugLogs) Debug.Log($"[Skills] Cast {skill.SkillName} (slot {slotIndex + 1})");
        }
    }

    private void HandleRelease(int slotIndex)
    {
        var skill = GetSkillInSlot(slotIndex);
        if (skill == null) return;

        if (skill is IChargeSkill charge)
        {
            charge.ReleaseCharge();
            if (debugLogs) Debug.Log($"[Skills] Charge Release {skill.SkillName} (slot {slotIndex + 1})");
        }
    }

    // ==== Helpers ====
    private int FirstEmptySlot()
    {
        for (int i = 0; i < 3; i++)
            if (slots[i] == null) return i;
        return -1;
    }
}

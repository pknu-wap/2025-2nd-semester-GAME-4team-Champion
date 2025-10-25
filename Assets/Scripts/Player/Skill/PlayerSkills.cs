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
    [SerializeField] private bool autoLearnOnStart = false;     // 시작 시 초기 스킬 자동 습득
    [SerializeField] private bool debugLogs = false;

    [Header("Initial Skills (Optional)")]
    [Tooltip("씬 오브젝트 또는 프리팹(프리팹이면 런타임에 플레이어 자식으로 복제됨)")]
    [SerializeField] private MonoBehaviour[] initialSkills; // IPlayerSkill 컴포넌트들

    // ---- Input ----
    private PlayerMove inputWrapper;
    private InputAction[] actions = new InputAction[3];
    private readonly string[] actionNames = new string[3];

    // ---- Slots (고정 3칸) ----
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

        // 인스펙터로 넣은 초기 스킬들 습득(프리팹/외부여도 안전 복제)
        foreach (var mb in initialSkills)
        {
            if (mb is IPlayerSkill s) AcquireSkill(s);
        }
    }

    private void OnEnable()
    {
        inputWrapper?.Enable();

        // 액션 바인딩
        BindAction(0, OnSkill1Performed, OnSkill1Canceled);
        BindAction(1, OnSkill2Performed, OnSkill2Canceled);
        BindAction(2, OnSkill3Performed, OnSkill3Canceled);
    }

    private void OnDisable()
    {
        // 액션 해제
        UnbindAction(0, OnSkill1Performed, OnSkill1Canceled);
        UnbindAction(1, OnSkill2Performed, OnSkill2Canceled);
        UnbindAction(2, OnSkill3Performed, OnSkill3Canceled);

        inputWrapper?.Disable();
    }

    // ==== Public API ====

    /// <summary>
    /// 런타임에 스킬을 습득. 빈 슬롯(1→2→3)에 순서대로 배정합니다.
    /// 외부/프리팹이어도 플레이어 자식으로 안전 복제되어 참조가 보장됩니다.
    /// </summary>
    public bool AcquireSkill(IPlayerSkill skill)
    {
        if (skill == null) return false;

        // 실체 및 계층 보장
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
            // 프리팹/외부 오브젝트 → 플레이어 자식으로 복제
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

        // 중복 방지
        for (int i = 0; i < 3; i++)
            if (ReferenceEquals(slots[i], skill))
                return false;

        // 빈 슬롯 찾기
        int slot = FirstEmptySlot();
        if (slot < 0)
        {
            if (debugLogs) Debug.LogWarning("[PlayerSkills] 슬롯 가득 참(3/3).");
            return false;
        }

        slots[slot] = skill;
        if (debugLogs) Debug.Log($"[Skills] Learned {skill.SkillName} → Slot {slot + 1}");

        return true;
    }

    /// <summary>슬롯에 들어간 스킬 반환(없으면 null)</summary>
    public IPlayerSkill GetSkillInSlot(int slotIndex)
    {
        return (slotIndex >= 0 && slotIndex < 3) ? slots[slotIndex] : null;
    }

    /// <summary>모든 슬롯 비우기(참조만 해제, 오브젝트는 유지)</summary>
    public void ResetLearned()
    {
        for (int i = 0; i < 3; i++) slots[i] = null;
    }

    // ==== Input 바인딩 유틸 ====
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
            Debug.LogWarning($"[PlayerSkills] Input action '{name}' 를 찾을 수 없습니다.");
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

    // ==== 슬롯 라우팅 ====
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

        // 차지형 우선
        if (skill is IChargeSkill charge)
        {
            bool ok = charge.TryStartCharge(attack, combat, moveRef, animator);
            if (debugLogs && ok) Debug.Log($"[Skills] Charge Start {skill.SkillName} (slot {slotIndex + 1})");
            return;
        }

        // 일반형
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

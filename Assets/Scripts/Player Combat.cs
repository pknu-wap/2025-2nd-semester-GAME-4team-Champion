using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMoveBehaviour moveRef; // ���� ������Ʈ�� ������ �ڵ� �Ҵ� ����
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;

    [Header("Guard / Parry")]
    [SerializeField] private float guardAngle = 120f;     // ���� �� ����
    [SerializeField] private float parryWindow = 0.15f;   // ���� ���� �и� ������
    [SerializeField] private float blockDamageMul = 0.2f; // ���� ���� ���(20%)
    [SerializeField] private float blockKnockMul = 0.3f;  // ���� �˹� ���
    [SerializeField] private float stamina = 100f;       // ���� ������
    [SerializeField] private float guardRegenPerSec = 25f;// ���� ȸ����
    [SerializeField] private float guardBreakTime = 1.5f; // �극��ũ �ð�

    private float guard;
    private bool isBlocking;
    private bool guardBroken;
    private float guardBreakEndTime;
    private float blockPressedTime = -999f;

    // �Է�
    private PlayerMove inputWrapper;     // .inputactions�� ������ ���� ����
    private InputAction blockAction;     // "Block" �׼�

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

        // �Է� ���� �غ�
        inputWrapper = new PlayerMove();
    }

    private void OnEnable()
    {
        inputWrapper.Enable();

        // �����ϰ� �̸����� ã��(���� ���� "Block"�� ������ ��)
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

    // === Block �Է� �ݹ� ===
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

    // === ���� ������ / �극��ũ ƽ ===
    private IEnumerator GuardTick()
    {
        var wait = new WaitForEndOfFrame();
        while (true)
        {
            // �극��ũ ����
            if (guardBroken && Time.time >= guardBreakEndTime)
            {
                guardBroken = false;

                // �� �̵� ��� ����
                if (moveRef) moveRef.SetMovementLocked(false, hardFreezePhysics: true);

                if (animator) animator.SetBool("GuardBroken", false);
                // guard = Mathf.Max(guard, guardMax * 0.5f); // ���ϸ� �κ� ȸ��
            }


            // ���� �� ȸ��
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

        // �� �̵� ���
        if (moveRef) moveRef.SetMovementLocked(true, hardFreezePhysics: true);

        if (animator)
        {
            animator.SetBool("GuardBroken", true);
            animator.SetTrigger("GuardBreak");
        }
    }


    // === �� ��Ʈ�� ���� �� �ܺο��� ȣ�� ===
    // hitDir: "�� �� �÷��̾�" ����, parryable: �и� ���� ����
    public void OnHit(float damage, float knockback, Vector2 hitDir, bool parryable, GameObject attacker = null)
    {
        Vector2 facing = (moveRef != null && moveRef.LastFacing.sqrMagnitude > 0f)
            ? moveRef.LastFacing
            : Vector2.right;

        // ���� �� üũ: �÷��̾� ����(facing)�� "�÷��̾� �� ��" ������ ����
        Vector2 inFrontDir = -hitDir.normalized; // �����÷��̾��� �ݴ� = �÷��̾����
        float cosHalf = Mathf.Cos(guardAngle * 0.5f * Mathf.Deg2Rad);
        bool inFront = Vector2.Dot(facing, inFrontDir) >= cosHalf;

        if (!guardBroken && isBlocking && inFront)
        {
            bool canParry = parryable && (Time.time - blockPressedTime) <= parryWindow;

            if (canParry)
            {
                // �и� ����
                if (animator) animator.SetTrigger("Parry");

                // �����ڿ� �и� �˸�(������)
                var parryableTarget = attacker ? attacker.GetComponent<IParryable>() : null;
                if (parryableTarget != null)
                    parryableTarget.OnParried(transform.position);

                return;
            }
            else
            {
                // �Ϲ� ���
                float finalDamage = damage * blockDamageMul;
                float finalKnock = knockback * blockKnockMul;

                ApplyDamage(finalDamage);
                ApplyKnockback(inFrontDir, finalKnock);

                guard -= damage; // ������ ��� �Ҹ�(���ϸ� Ʃ��)
                if (guard <= 0f) GuardBreak();

                if (animator) animator.SetTrigger("BlockHit");
                return;
            }
        }

        // ��� ����(����/�Ĺ�/�극��ũ/����)
        ApplyDamage(damage);
        ApplyKnockback(inFrontDir, knockback);
    }

    private void ApplyDamage(float amount)
    {
        // TODO: ü�� �ý��� ����
        // Debug.Log($"Damage {amount}");
    }

    private void ApplyKnockback(Vector2 dirToEnemy, float force)
    {
        if (force <= 0f || rb == null) return;
        Vector2 pushDir = -dirToEnemy.normalized; // ���� �ݴ� �������� �и�
        rb.AddForce(pushDir * force, ForceMode2D.Impulse);
    }
}

// (����) ���� �и��Ǿ��� �� �����ϰ� �ʹٸ� ���
public interface IParryable
{
    void OnParried(Vector3 parrySourcePosition);
}

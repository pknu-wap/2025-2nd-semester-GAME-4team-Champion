using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BossCore : MonoBehaviour, IParryable, IDamageable
{
    [Header("Common")]
    [SerializeField] private float AttackCooldown = 2f;
    public float SummonTimer = 5f;
    [SerializeField] private bool Summoned = false;
    public Slider BossTimerSlider;
    [SerializeField] public Transform Player;

    [Header("Health / Stamina")]
    public float MaxHp = 100f;
    public float CurrentHp = 100f;
    public float MaxStamina = 100f;
    public float CurrentStamina = 100f;

    [Header("General")]
    [SerializeField] public float RecognizedArea = 10f;
    [SerializeField] public float Speed = 3.0f;
    [SerializeField] public float MinChaseDistance = 1.3f;

    [Header("UI & Cinematic")]
    public Text Timer;
    public GameObject Hp;
    public GameObject Stamina;
    [SerializeField] private GameObject Ui;
    [SerializeField] private GameObject UiBroke;
    [SerializeField] private CameraShaking CamShake;
    [SerializeField] private Animator HandAnim;
    [SerializeField] private Animator BeltAnim;
    [SerializeField] private Text TextUi;
    [SerializeField] private string FullText = "NEW CHALLENGER";

    [Header("Rendering")]
    [SerializeField] public SpriteRenderer SpriteRenderer;
    [SerializeField] public Collider2D MovementArea;

    [Header("Runtime")]
    [SerializeField] public float AttackTimer = 0f;
    public Rigidbody2D Rb { get; private set; }
    public bool IsActing { get; set; }
    private Collider2D PlayerCol;
    private AnimatorUpdateMode BhOldMode, HandOldMode, BeltOldMode;
    private float BhOldSpeed, HandOldSpeed, BeltOldSpeed;
    private Coroutine TypingCo;

    private BossFight _combat;

    public enum MeleeAttackType { One, OneOne, OneTwo, Roll }
    public enum RangeAttackType { Short, Mid, Long }
    public enum AttackSelectMode
    {
        Random,
        Melee_One,
        Melee_OneOne,
        Melee_OneTwo,
        Melee_Roll,
        Range_Short,
        Range_Mid,
        Range_Long
    }

    [Header("Attack Select (Debug)")]
    public AttackSelectMode SelectMode = AttackSelectMode.Random;
    public MeleeAttackType LastMeleeType { get; set; }

    [Header("Hit Window")]
    [SerializeField] private float hitActiveDuration = 0.08f;
    private bool _hitWindowOpen = false;
    private bool _hitAppliedThisWindow = false;

    [Header("Parry Settings")]
    public bool AllowParry = true;
    private bool _currentParryable = true;

    // 🔒 Snap 이후 잠깐 이동 금지(붙음 방지)
    private float _noMoveRemain = 0f;

    [SerializeField] private GameManager _gm;

    [Header("Parry Counter")]
    private int hitCount = 0;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        _combat = GetComponent<BossFight>();
        if (_combat == null) _combat = gameObject.AddComponent<BossFight>();
        _combat.BindCore(this);
    }

    private void Start()
    {
        AttackTimer = 0f;

        if (CamShake == null)
        {
            var mainCam = Camera.main;
            if (mainCam != null) CamShake = mainCam.GetComponent<CameraShaking>();
        }
        if (Player != null) PlayerCol = Player.GetComponent<Collider2D>();

#if UNITY_2022_3_OR_NEWER
        if (_gm == null) _gm = FindFirstObjectByType<GameManager>();
#else
        if (_gm == null) _gm = FindObjectOfType<GameManager>();
#endif

        if (!Summoned)
        {
            Rb.linearVelocity = Vector2.zero;
            if (SpriteRenderer != null) SpriteRenderer.enabled = false;
            if (TryGetComponent<Collider2D>(out var selfCol)) selfCol.enabled = false;
        }

        if (BossTimerSlider != null)
        {
            BossTimerSlider.maxValue = Mathf.Max(SummonTimer, 0f);
            BossTimerSlider.value = SummonTimer;
        }

        Rb.position = ClampInside(Rb.position);
    }

    private void Update()
    {
        if (Player == null || Rb == null) return;

        if (!Summoned)
        {
            if (SummonTimer > 0f)
            {
                SummonTimer -= Time.deltaTime;
                if (Timer != null) Timer.text = Mathf.CeilToInt(SummonTimer).ToString();
                if (BossTimerSlider != null) BossTimerSlider.value = SummonTimer;
            }

            if (SummonTimer <= 0f) ActivateBoss();
            return;
        }

        if (_noMoveRemain > 0f) _noMoveRemain -= Time.deltaTime;

        if (!IsActing)
        {
            AttackTimer += Time.deltaTime;
            if (AttackTimer >= AttackCooldown)
            {
                AttackWayChoice();
                AttackTimer = 0f;
            }
        }
    }

    private void FixedUpdate()
    {
        if (!Summoned)
        {
            Rb.linearVelocity = Vector2.zero;
            return;
        }

        if (_noMoveRemain > 0f)
        {
            Rb.linearVelocity = Vector2.zero;
            return;
        }

        if (!IsActing)
        {
            AIMoveMent();
        }
    }

    public void ApplyHit(float damage, float knockback, Vector2 direction, GameObject source)
    {
        Debug.Log($"[BossCore] ApplyHit called by {source.name}");

        CurrentHp -= damage;

        hitCount++;

        if (hitCount >= 3 && Random.value <= 0.7f)
        {
            Debug.Log("[BossCore] Parry triggered by accumulated hits!");
            OnParried(source.transform.position);
            hitCount = 0;
        }

        if (_combat != null && _combat.isDashing)
        {
            _combat.InterruptDash();
        }

        if (CurrentHp <= 0f)
        {
            Debug.Log("[BossCore] Boss defeated!");
        }
    }

    private void OnDisable()  => ResumeFromCinematic();
    private void OnDestroy()  => ResumeFromCinematic();

    public void StartNoMoveCooldown(float seconds)
    {
        _noMoveRemain = Mathf.Max(_noMoveRemain, Mathf.Max(0f, seconds));
    }

    private void ActivateBoss()
    {
        Time.timeScale = 0f;
        Summoned = true;

        if (SpriteRenderer != null) SpriteRenderer.enabled = true;
        if (TryGetComponent<Collider2D>(out var selfCol)) selfCol.enabled = true;

        Rb.linearVelocity = Vector2.zero;

        if (BossTimerSlider != null) BossTimerSlider.gameObject.SetActive(false);
        if (Hp != null) Hp.SetActive(true);
        if (Stamina != null) Stamina.SetActive(true);
        if (HandAnim != null)
        {
            HandOldMode = HandAnim.updateMode;
            HandOldSpeed = HandAnim.speed;
            HandAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
            HandAnim.speed = 1f;
        }
        if (BeltAnim != null)
        {
            BeltOldMode = BeltAnim.updateMode;
            BeltOldSpeed = BeltAnim.speed;
            BeltAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
            BeltAnim.speed = 1f;
        }

        StartCoroutine(MoveUi());
    }

    private IEnumerator MoveUi()
    {
        float t = 0f;
        while (t < 1f) { t += Time.unscaledDeltaTime; yield return null; }

        if (BeltAnim != null) BeltAnim.SetTrigger("BeltStart");

        yield return new WaitForSecondsRealtime(7f);

        yield return new WaitForSecondsRealtime(0.1f);

        if (UiBroke != null) UiBroke.SetActive(true);

        yield return new WaitForSecondsRealtime(2f);
        if (HandAnim != null) HandAnim.SetTrigger("HandStart");
        yield return new WaitForSecondsRealtime(4f);

        if (UiBroke != null) UiBroke.SetActive(false);

        yield return new WaitForSecondsRealtime(1f);
        PlayText(FullText);

        yield return new WaitForSecondsRealtime(1.7f);
        if (Ui != null) Ui.SetActive(false);

        ResumeFromCinematic();
    }

    private void ResumeFromCinematic()
    {
        Time.timeScale = 1f;
        if (HandAnim != null) { HandAnim.updateMode = HandOldMode; HandAnim.speed = HandOldSpeed; }
        if (BeltAnim != null) { BeltAnim.updateMode = BeltOldMode; BeltAnim.speed = BeltOldSpeed; }
    }

    private void AttackWayChoice()
    {
        if (_combat == null) return;

        switch (SelectMode)
        {
            case AttackSelectMode.Random:
            {
                int type = Random.Range(0, 2);
                if (type == 0) _combat.Melee_Attack_DistanceBased();
                else           _combat.Range_Attack();
                break;
            }
            case AttackSelectMode.Melee_One:    _combat.Melee_Attack(MeleeAttackType.One);     break;
            case AttackSelectMode.Melee_OneOne: _combat.Melee_Attack(MeleeAttackType.OneOne);  break;
            case AttackSelectMode.Melee_OneTwo: _combat.Melee_Attack(MeleeAttackType.OneTwo);  break;
            case AttackSelectMode.Melee_Roll: _combat.Melee_Attack(MeleeAttackType.Roll);  break;
            case AttackSelectMode.Range_Short:  _combat.Range_Attack(RangeAttackType.Short);   break;
            case AttackSelectMode.Range_Mid:    _combat.Range_Attack(RangeAttackType.Mid);     break;
            case AttackSelectMode.Range_Long:   _combat.Range_Attack(RangeAttackType.Long);    break;
        }
    }

    public void TriggerMeleeDamage()
    {
        StartCoroutine(DoMeleeDamage());
    }

    private IEnumerator OpenHitWindow()
    {
        _currentParryable = AllowParry;
        _hitAppliedThisWindow = false;
        _hitWindowOpen = true;

        TryApplyHit_OnlyIfInRange();

        yield return new WaitForSeconds(hitActiveDuration);
        _hitWindowOpen = false;
    }

    private void TryApplyHit_OnlyIfInRange()
    {
        if (Player == null) return;

        Vector2 center = transform.position;
        float radius = 1.2f;
        LayerMask playerMask = LayerMask.GetMask("Player");

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, playerMask);

        foreach (var hit in hits)
        {
            var ph = hit.GetComponent<PlayerHit>();
            if (ph != null)
            {
                Vector2 hitDir = (hit.transform.position - transform.position).normalized;
                ph.OnHit(10f, 6f, hitDir, _currentParryable, gameObject);
                _hitAppliedThisWindow = true;
            }
        }
    }

    private IEnumerator DoMeleeDamage()
    {
        if (_gm == null) yield break;

        switch (LastMeleeType)
        {
            case MeleeAttackType.One:
                yield return OpenHitWindow();
                break;

            case MeleeAttackType.OneOne:
                yield return OpenHitWindow();
                yield return new WaitForSeconds(0.1f);
                yield return OpenHitWindow();
                break;

            case MeleeAttackType.OneTwo:
                yield return OpenHitWindow();
                yield return new WaitForSeconds(0.05f);
                yield return OpenHitWindow();
                break;

            case MeleeAttackType.Roll:
                yield return OpenHitWindow();
                break;
        }
    }

    private void TryApplyHit(Collider2D other)
    {
        bool isPlayerHit = (PlayerCol != null) ? (other == PlayerCol) : other.CompareTag("Player");
        if (!isPlayerHit) return;

        if (!_hitWindowOpen)
        {
            TriggerMeleeDamage();
        }

        if (_hitWindowOpen && !_hitAppliedThisWindow)
        {
            var ph = Player != null ? Player.GetComponent<PlayerHit>() : null;
            if (ph != null)
            {
                Vector2 hitDir = (Player.position - transform.position).normalized;
                ph.OnHit(10f, 6f, hitDir, _currentParryable, gameObject);
                Debug.Log($"[BossCore] Hit applied -> parryable={_currentParryable}");

                hitCount++;

                if (hitCount >= 3 && Random.value <= 0.6f)
                {
                    Debug.Log("[BossCore] Parry triggered automatically after 3 hits.");
                    OnParried(Player.position);
                    hitCount = 0;
                    return;
                }
            }

            _hitAppliedThisWindow = true;

            if ((_combat != null && _combat.isDashing) &&
                (LastMeleeType == MeleeAttackType.One || LastMeleeType == MeleeAttackType.OneOne))
            {
                _combat.InterruptDash();
            }
        }
    }

    public void ForceNextAction()
    {
        IsActing = false;
        AttackTimer = AttackCooldown;
    }

    public Vector2 ClampInside(Vector2 p)
    {
        if (MovementArea == null) return p;
        Vector2 closest = MovementArea.ClosestPoint(p);
        Vector2 center = (Vector2)MovementArea.bounds.center;
        Vector2 inward = (center - closest).sqrMagnitude > 1e-8f ? (center - closest).normalized : Vector2.zero;
        return closest + inward * 0.14f;
    }

    public void AIMoveMent()
    {
        if (Player == null) { Rb.linearVelocity = Vector2.zero; return; }

        Vector2 toPlayer = (Vector2)Player.position - Rb.position;
        float dist = toPlayer.magnitude;

        if (dist < MinChaseDistance)
        {
            Rb.linearVelocity = Vector2.zero;
            return;
        }

        if (dist <= RecognizedArea)
        {
            Vector2 dir = toPlayer.sqrMagnitude > 1e-8f ? toPlayer.normalized : Vector2.zero;
            Rb.linearVelocity = dir * Speed;
        }
        else
        {
            Rb.linearVelocity = Vector2.zero;
        }
    }

    public void PlayText(string message)
    {
        if (TextUi == null) return;
        if (TypingCo != null) StopCoroutine(TypingCo);
        TypingCo = StartCoroutine(TypeTextRoutine(message));
    }

    private IEnumerator TypeTextRoutine(string message)
    {
        if (TextUi == null) yield break;
        TextUi.text = "";
        int count = 0;
        foreach (char c in message)
        {
            TextUi.text += c;
            count++;
            if (CamShake != null && (count % 4 == 0))
            {
            }
            yield return new WaitForSecondsRealtime(0.05f);
        }
    }

    public void OnParried(Vector3 parrySourcePosition)
    {
        Debug.Log("[BossCore] Parried by player -> stamina +10 then -5 (success only)");

        // 패링 '성공 시에만' 스태미나 처리(+10 후 -5)
        if (Player != null && Player.TryGetComponent<PlayerCombat>(out var pc))
        {
            pc.AddStamina(10f);
            pc.AddStamina(-5f);
        }

        // 넉백
        if (Rb != null)
        {
            Vector2 dir = ((Vector2)transform.position - (Vector2)parrySourcePosition).normalized;
            Rb.AddForce(dir * 2f, ForceMode2D.Impulse);
        }

        _combat?.InterruptDash();

        StartNoMoveCooldown(0.6f);

        IsActing = false;
        AttackTimer = 0f;
    }
}

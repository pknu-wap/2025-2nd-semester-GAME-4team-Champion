using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class BossCore : MonoBehaviour
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
    [SerializeField] private float TimeColTime;

    [Header("General")]
    [SerializeField] public float RecognizedArea = 10f;
    [SerializeField] public float Speed = 3.0f;

    [Header("UI & Cinematic")]
    public Text Timer;
    public GameObject Hp;
    public GameObject Stamina;
    [SerializeField] private GameObject Ui;
    [SerializeField] private GameObject UiBroke;
    [SerializeField] private CameraShaking CamShake;
    [SerializeField] private Animator BlackHoleAnim;
    [SerializeField] private Animator HandAnim;
    [SerializeField] private Animator BeltAnim;
    [SerializeField] private Text TextUi;
    [SerializeField] private string FullText = "NEW CHALLENGER";

    [Header("Rendering")]
    [SerializeField] public SpriteRenderer SpriteRenderer;
    [SerializeField] public Collider2D MovementArea;

    [Header("Runtime")]
    [SerializeField] private float AttackTimer = 0f;
    public Rigidbody2D Rb { get; private set; }
    public bool IsActing { get; set; }
    private Collider2D PlayerCol;
    private AnimatorUpdateMode BhOldMode, HandOldMode, BeltOldMode;
    private float BhOldSpeed, HandOldSpeed, BeltOldSpeed;
    private Coroutine TypingCo;

    private BossFight _combat;

    // ==================== 공격 타입/선택 모드 ====================
    public enum MeleeAttackType { One, OneOne, OneTwo }   // 근 3종
    public enum RangeAttackType { Short, Mid, Long }      // 원 3종
    public enum AttackSelectMode
    {
        Random,
        Melee_One,
        Melee_OneOne,
        Melee_OneTwo,
        Range_Short,
        Range_Mid,
        Range_Long
    }

    [Header("Attack Select (Debug)")]
    public AttackSelectMode SelectMode = AttackSelectMode.Random;
    public bool UseDebugHotkeys = true;

    public MeleeAttackType LastMeleeType { get; set; }

    [Header("Hit Window")]
    [SerializeField] private float hitActiveDuration = 0.08f; // 각 타의 판정 지속 시간
    private bool _hitWindowOpen = false;
    private bool _hitAppliedThisWindow = false;

    // GameManager로 실제 플레이어 데미지 반영
    [SerializeField] private GameManager _gm;

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

        if (_gm == null)
        {
#if UNITY_2022_3_OR_NEWER
            _gm = FindFirstObjectByType<GameManager>();
#else
            _gm = FindObjectOfType<GameManager>();
#endif
        }

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

        // 소환 전 카운트다운
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

        // 자동 공격 쿨다운
        if (!IsActing)
        {
            AttackTimer += Time.deltaTime;
            if (AttackTimer >= AttackCooldown)
            {
                AttackWayChoice();
                AttackTimer = 0f;
            }
        }

        // 디버그 핫키 (선택 확인용)
        if (UseDebugHotkeys && !IsActing && Summoned)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) { _combat.Melee_Attack(MeleeAttackType.One);     AttackTimer = 0f; }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { _combat.Melee_Attack(MeleeAttackType.OneOne);  AttackTimer = 0f; }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { _combat.Melee_Attack(MeleeAttackType.OneTwo);  AttackTimer = 0f; }

            if (Input.GetKeyDown(KeyCode.Alpha4)) { _combat.Range_Attack(RangeAttackType.Short);   AttackTimer = 0f; }
            if (Input.GetKeyDown(KeyCode.Alpha5)) { _combat.Range_Attack(RangeAttackType.Mid);     AttackTimer = 0f; }
            if (Input.GetKeyDown(KeyCode.Alpha6)) { _combat.Range_Attack(RangeAttackType.Long);    AttackTimer = 0f; }
        }
    }

    private void FixedUpdate()
    {
        if (!Summoned)
        {
            Rb.linearVelocity = Vector2.zero;
            return;
        }

        if (!IsActing)
        {
            AIMoveMent();
        }
    }

    private void OnDisable()  => ResumeFromCinematic();
    private void OnDestroy()  => ResumeFromCinematic();

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

        if (BlackHoleAnim != null)
        {
            BhOldMode = BlackHoleAnim.updateMode;
            BhOldSpeed = BlackHoleAnim.speed;
            BlackHoleAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
            BlackHoleAnim.speed = 1f;
        }
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
                StartCoroutine(CamShake.ImpulseMoveMent());
            }
            yield return new WaitForSecondsRealtime(0.05f);
        }
    }

    private IEnumerator MoveUi()
    {
        float t = 0f;
        while (t < 1f) { t += Time.unscaledDeltaTime; yield return null; }

        if (BeltAnim != null) BeltAnim.SetTrigger("BeltStart");

        yield return new WaitForSecondsRealtime(7f);

        if (CamShake != null) StartCoroutine(CamShake.ImpulseMoveMent());
        yield return new WaitForSecondsRealtime(0.1f);

        if (BlackHoleAnim != null) BlackHoleAnim.SetBool("BlackHoleStart", true);
        if (UiBroke != null) UiBroke.SetActive(true);

        yield return new WaitForSecondsRealtime(2f);
        if (HandAnim != null) HandAnim.SetTrigger("HandStart");
        yield return new WaitForSecondsRealtime(4f);

        if (BlackHoleAnim != null) BlackHoleAnim.SetBool("BlackHoleStart", false);
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
        if (BlackHoleAnim != null) { BlackHoleAnim.updateMode = BhOldMode; BlackHoleAnim.speed = BhOldSpeed; }
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
                if (type == 0)
                {
                    // ✅ 근거리: 거리 기반 규칙으로 실행
                    _combat.Melee_Attack_DistanceBased();
                }
                else
                {
                    // 원거리: 기존 랜덤
                    _combat.Range_Attack();
                }
                break;
            }
            case AttackSelectMode.Melee_One:    _combat.Melee_Attack(MeleeAttackType.One);     break;
            case AttackSelectMode.Melee_OneOne: _combat.Melee_Attack(MeleeAttackType.OneOne);  break;
            case AttackSelectMode.Melee_OneTwo: _combat.Melee_Attack(MeleeAttackType.OneTwo);  break;
            case AttackSelectMode.Range_Short:  _combat.Range_Attack(RangeAttackType.Short);   break;
            case AttackSelectMode.Range_Mid:    _combat.Range_Attack(RangeAttackType.Mid);     break;
            case AttackSelectMode.Range_Long:   _combat.Range_Attack(RangeAttackType.Long);    break;
        }
    }

    // ===== BossFight에서 호출: 각 타격 시점에 히트 윈도우를 열기 =====
    public void TriggerMeleeDamage()
    {
        StartCoroutine(DoMeleeDamage());
    }

    private IEnumerator OpenHitWindow()
    {
        _hitAppliedThisWindow = false;
        _hitWindowOpen = true;
        yield return new WaitForSeconds(hitActiveDuration);
        _hitWindowOpen = false;
    }

    // 각 패턴은 "히트 윈도우"를 열고, 그 시간 동안 트리거 겹치면 1회만 데미지 적용
    private IEnumerator DoMeleeDamage()
    {
        if (_gm == null) yield break;

        switch (LastMeleeType)
        {
            case MeleeAttackType.One:
                yield return OpenHitWindow();          // 1타
                break;

            case MeleeAttackType.OneOne:
                yield return OpenHitWindow();          // 1타
                yield return new WaitForSeconds(0.1f);
                yield return OpenHitWindow();          // 2타
                break;

            case MeleeAttackType.OneTwo:
                yield return OpenHitWindow();          // 1타
                yield return new WaitForSeconds(0.05f);
                yield return OpenHitWindow();          // 2타
                break;
        }
    }

    // 트리거에서만 실제 데미지 적용 (히트 윈도우 내 1회)
    private void TryApplyHit(Collider2D other)
    {
        bool isPlayerHit = (PlayerCol != null) ? (other == PlayerCol) : other.CompareTag("Player");
        if (!isPlayerHit) return;

        if (_hitWindowOpen && !_hitAppliedThisWindow)
        {
            _gm.TakePlayerDamage(10f);
            _hitAppliedThisWindow = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other) { TryApplyHit(other); }
    private void OnTriggerStay2D(Collider2D other)  { TryApplyHit(other); }

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
        Vector2 toPlayer = (Vector2)Player.position - Rb.position;
        float dist = toPlayer.magnitude;

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
}

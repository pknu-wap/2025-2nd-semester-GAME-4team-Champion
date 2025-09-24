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


    [Header("Contact Damage")]
    [SerializeField] private float damageInterval = 0.7f;
    private int _playerContactCount = 0;
    private bool _playerInRange = false;
    private Coroutine _damageCoroutine;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        if (!_combat) _combat = GetComponent<BossFight>();
        if (!_combat) _combat = gameObject.AddComponent<BossFight>();
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

        if (!IsActing)
        {
            AIMoveMent();
        }
    }

    private void OnDisable()
    {
        ResumeFromCinematic();
        StopDamageLoop();
    }

    private void OnDestroy()
    {
        ResumeFromCinematic();
        StopDamageLoop();
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
        
        CamShake.power = 4f;
        if (CamShake != null) StartCoroutine(CamShake.ImpulseMoveMent());

        yield return new WaitForSecondsRealtime(0.1f);

        CamShake.power = 0.4f;
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
        int type = Random.Range(0, 2);
        switch (type)
        {
            case 0: _combat.Melee_Attack(); break;
            case 1: _combat.Range_Attack(); break;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Summoned) return;

        bool isPlayerHit = (PlayerCol != null) ? (other == PlayerCol) : other.CompareTag("Player");
        if (!isPlayerHit) return;

        _playerContactCount++;
        if (_playerContactCount == 1)
        {
            Debug.Log("Damage = 10");
            _combat.StopInFrontOfPlayer();

            _playerInRange = true;
            if (_damageCoroutine == null)
                _damageCoroutine = StartCoroutine(DoDamageOverTime());
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        bool isPlayerHit = (PlayerCol != null) ? (other == PlayerCol) : other.CompareTag("Player");
        if (!isPlayerHit) return;

        _playerContactCount = Mathf.Max(0, _playerContactCount - 1);
        if (_playerContactCount == 0)
        {
            StopDamageLoop();
        }
    }

    private IEnumerator DoDamageOverTime()
    {
        while (_playerInRange)
        {
            yield return new WaitForSeconds(damageInterval);
            if (!_playerInRange) break;

            Debug.Log("Damage = 10");
            _combat.StopInFrontOfPlayer();
        }
        _damageCoroutine = null;
    }

    private void StopDamageLoop()
    {
        _playerInRange = false;
        if (_damageCoroutine != null)
        {
            StopCoroutine(_damageCoroutine);
            _damageCoroutine = null;
        }
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

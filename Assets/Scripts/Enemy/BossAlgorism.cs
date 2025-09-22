using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class BossAlgorism : MonoBehaviour
{
    [Header("Common")]
    [SerializeField] private float AttackCooldown;
    public float SummonTimer;
    [SerializeField] private bool Summoned = false;
    public Slider BossTimerSlider;
    [SerializeField] private Transform player;

    [Header("Health")]
    public float MaxHp;
    public float CurrentHp;
    public float MaxStamina;
    public float currentStamina;

    [Header("Dash (Melee)")]
    [SerializeField] private float dashSpeed;
    private float dashDuration;
    private float preWindup;

    [Header("Stop In Front Of Player")]
    [SerializeField] private float stopoffset;
    [SerializeField] private LayerMask playerLayer;

    [Header("Dash (Range)")]
    [SerializeField] private float retreatSpeed;
    [SerializeField] private float retreatDuration;
    private float desiredDistance;
    private float rangePreWindup;

    [Header("Range Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    private int volleyCount;
    [SerializeField] private float volleyInterval;

    [Header("Remain")]
    public Text Timer;
    public GameObject HP;
    public GameObject Stamina;
    [SerializeField] private float AttackTimer;
    private Rigidbody2D rb;
    private bool isActing;
    private Collider2D PlayerCol;
    [SerializeField] private float RecognizedArea;
    [SerializeField] private GameObject UI;
    [SerializeField] private RectTransform UIImage;
    [SerializeField] private GameObject UIBelt;
    [SerializeField] private GameObject UIBroke;
    [SerializeField] private CameraShaking camShake;
    [SerializeField] private Animator blackHoleAnim;
    [SerializeField] private Animator handAnim;

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Collider2D movementArea;

    private AnimatorUpdateMode _bhOldMode, _handOldMode;
    private float _bhOldSpeed, _handOldSpeed;
    [SerializeField] private TMP_Text textUI;

    private string fullText = "NEW CHALLENGER";
    private Coroutine typingCo;

    private void Start()
    {
        AttackTimer = 0f;
        rb = GetComponent<Rigidbody2D>();

        if (camShake == null)
        {
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                camShake = mainCam.GetComponent<CameraShaking>();
            }
        }

        if (player != null)
        {
            PlayerCol = player.GetComponent<Collider2D>();
        }

        if (!Summoned)
        {
            rb.linearVelocity = Vector2.zero;
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
            }
            if (TryGetComponent<Collider2D>(out var selfCol))
            {
                selfCol.enabled = false;
            }
        }

        if (BossTimerSlider != null)
        {
            BossTimerSlider.maxValue = Mathf.Max(SummonTimer, 0f);
            BossTimerSlider.value = SummonTimer;
        }

        rb.position = ClampInside(rb.position);
    }

    private void Update()
    {
        if (!Summoned)
        {
            if (SummonTimer > 0f)
            {
                SummonTimer -= Time.deltaTime;

                if (Timer != null)
                {
                    Timer.text = Mathf.CeilToInt(SummonTimer).ToString();
                }

                if (BossTimerSlider != null)
                {
                    BossTimerSlider.value = SummonTimer;
                }
            }

            if (SummonTimer <= 0f)
            {
                ActivateBoss();
            }
            return;
        }

        if (!isActing)
        {
            AttackTimer += Time.deltaTime;

            if (AttackTimer >= AttackCooldown)
            {
                Attack_Way_Choice();
                AttackTimer = 0f;
            }
        }
    }

    private void FixedUpdate()
    {
        if (!Summoned)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (!isActing)
        {
            AIMoveMent();
        }
    }

    private void ActivateBoss()
    {
        Time.timeScale = 0f;
        Summoned = true;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        if (TryGetComponent<Collider2D>(out var selfCol))
        {
            selfCol.enabled = true;
        }

        rb.linearVelocity = Vector2.zero;

        if (BossTimerSlider != null)
        {
            BossTimerSlider.gameObject.SetActive(false);
        }

        if (HP != null)
        {
            HP.SetActive(true);
        }
        if (Stamina != null)
        {
            Stamina.SetActive(true);
        }

        if (blackHoleAnim != null)
        {
            _bhOldMode = blackHoleAnim.updateMode;
            _bhOldSpeed = blackHoleAnim.speed;
            blackHoleAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
            blackHoleAnim.speed = 1f;
        }
        if (handAnim != null)
        {
            _handOldMode = handAnim.updateMode;
            _handOldSpeed = handAnim.speed;
            handAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
            handAnim.speed = 1f;
        }

        if (UIImage != null)
        {
            StartCoroutine(MoveUI());
        }
    }

    public void PlayText(string message)
    {
        if (typingCo != null) StopCoroutine(typingCo);
        typingCo = StartCoroutine(TypeTextRoutine(message));
    }

    private IEnumerator TypeTextRoutine(string message)
    {
        textUI.text = "";
        foreach (char c in message)
        {
            textUI.text += c;
            if (camShake != null)
            {
                StartCoroutine(camShake.ImpulseMoveMent());
            }
            yield return new WaitForSecondsRealtime(0.05f);
        }
    }

    private IEnumerator MoveUI()
    {
        Vector2 startPos = UIImage.anchoredPosition;
        Vector2 targetPos = new Vector2(0f, startPos.y);

        float t = 0f;
        const float duration = 1f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(t / duration);
            UIImage.anchoredPosition = Vector2.Lerp(startPos, targetPos, normalized);
            yield return null;
        }
        UIImage.anchoredPosition = targetPos;

        UIBelt.SetActive(true);

        yield return new WaitForSecondsRealtime(2f);

        if (blackHoleAnim != null)
        {
            blackHoleAnim.SetBool("BlackHoleStart", true);
        }
        if (UIBroke != null)
        {
            UIBroke.SetActive(true);
        }
        if (camShake != null)
        {
            StartCoroutine(camShake.ImpulseMoveMent());
        }

        yield return new WaitForSecondsRealtime(2f);

        if (handAnim != null)
        {
            handAnim.SetTrigger("HandStart");
        }

        yield return new WaitForSecondsRealtime(2f);

        UIBelt.SetActive(false);

        yield return new WaitForSecondsRealtime(2f);

        if (UIImage != null)
        {
            UIImage.gameObject.SetActive(false);
        }
        if (blackHoleAnim != null)
        {
            blackHoleAnim.SetBool("BlackHoleStart", false);
        }
        if (UIBroke != null)
        {
            UIBroke.SetActive(false);
        }

        yield return new WaitForSecondsRealtime(1f);

        PlayText(fullText);

        yield return new WaitForSecondsRealtime(1.7f);
        if (UI != null)
        {
            UI.SetActive(false);
        }

        ResumeFromCinematic();
    }

    private void ResumeFromCinematic()
    {
        Time.timeScale = 1f;

        if (blackHoleAnim != null)
        {
            blackHoleAnim.updateMode = _bhOldMode;
            blackHoleAnim.speed = _bhOldSpeed;
        }
        if (handAnim != null)
        {
            handAnim.updateMode = _handOldMode;
            handAnim.speed = _handOldSpeed;
        }
    }

    private void Attack_Way_Choice()
    {
        int Attack_Way_Type = Random.Range(0, 2);

        switch (Attack_Way_Type)
        {
            case 0:
                Melee_Attack();
                break;
            case 1:
                Range_Attack();
                break;
        }
    }

    private void Melee_Attack()
    {
        int Melee_Attack_Type = Random.Range(0, 3);

        switch (Melee_Attack_Type)
        {
            case 0:
                preWindup = 2f;
                dashDuration = 0.5f;
                StartCoroutine(DashBurstStopInFront());
                break;
            case 1:
                preWindup = 3f;
                dashDuration = 1.0f;
                StartCoroutine(DashBurstStopInFront());
                break;
            case 2:
                preWindup = 4f;
                dashDuration = 1.5f;
                StartCoroutine(DashBurstStopInFront());
                break;
        }
    }

    private void Range_Attack()
    {
        int Range_Attack_Type = Random.Range(0, 3);

        switch (Range_Attack_Type)
        {
            case 0:
                desiredDistance = Mathf.Max(desiredDistance, 5f);
                rangePreWindup = 1.0f;
                volleyCount = 1;
                StartCoroutine(RetreatThenFire());
                break;
            case 1:
                desiredDistance = Mathf.Max(desiredDistance, 7f);
                rangePreWindup = 1.3f;
                volleyCount = 2;
                StartCoroutine(RetreatThenFire());
                break;
            case 2:
                desiredDistance = Mathf.Max(desiredDistance, 9f);
                rangePreWindup = 1.5f;
                volleyCount = 3;
                StartCoroutine(RetreatThenFire());
                break;
        }
    }

    private IEnumerator DashBurstStopInFront()
    {
        isActing = true;
        rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(preWindup);

        Vector2 startPosition = rb.position;
        Vector2 dir = ((Vector2)player.position - startPosition).normalized;
        Vector2 target = ComputeFrontTarget(startPosition, dir);
        target = ClampInside(target);

        float timer = 0f;
        while (timer < dashDuration)
        {
            Vector2 toTarget = target - rb.position;
            float dist = toTarget.magnitude;

            if (dist <= 0.02f)
            {
                break;
            }

            Vector2 toTargetDir = (dist > 1e-6f) ? toTarget / dist : Vector2.zero;
            float step = dashSpeed * Time.fixedDeltaTime;
            Vector2 nextPos = (step >= dist) ? target : rb.position + toTargetDir * step;
            nextPos = ClampInside(nextPos);

            rb.MovePosition(nextPos);
            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.linearVelocity = Vector2.zero;
        isActing = false;
    }

    private Vector2 ComputeFrontTarget(Vector2 start, Vector2 dir)
    {
        float maxDist = 50f;
        Vector2 fallback = (Vector2)player.position - dir * stopoffset;

        RaycastHit2D hitPlayer = Physics2D.Raycast(start, dir, maxDist, playerLayer);

        if (hitPlayer.collider != null)
        {
            return hitPlayer.point - dir * stopoffset;
        }

        return fallback;
    }

    private void FireOneProjectile()
    {
        if (projectilePrefab != null && firePoint != null)
        {
            Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        }
    }

    private IEnumerator RetreatThenFire()
    {
        isActing = true;

        float t = 0f;
        while (t < retreatDuration)
        {
            Vector2 toPlayer = (Vector2)player.position - rb.position;
            float dist = toPlayer.magnitude;

            Vector2 dirAway = (-toPlayer).normalized;
            float step = retreatSpeed * Time.fixedDeltaTime;

            Vector2 nextPos = rb.position + dirAway * step;
            nextPos = ClampInside(nextPos);
            rb.MovePosition(nextPos);

            if (desiredDistance > 0f && dist >= desiredDistance)
            {
                break;
            }

            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.linearVelocity = Vector2.zero;

        if (rangePreWindup > 0f)
        {
            yield return new WaitForSeconds(rangePreWindup);
        }

        for (int i = 0; i < volleyCount; i++)
        {
            FireOneProjectile();

            if (i < volleyCount - 1 && volleyInterval > 0f)
            {
                yield return new WaitForSeconds(volleyInterval);
            }
        }

        isActing = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            StopInFrontOfPlayer();
        }
    }

    private void StopInFrontOfPlayer()
    {
        Vector2 dir = ((Vector2)player.position - rb.position).normalized;
        Vector2 stopPos = (Vector2)player.position - dir * stopoffset;
        rb.position = ClampInside(stopPos);
        rb.linearVelocity = Vector2.zero;
        isActing = false;
    }

    private Vector2 ClampInside(Vector2 p)
    {
        if (movementArea == null)
        {
            return p;
        }

        Vector2 closest = movementArea.ClosestPoint(p);
        Vector2 center = (Vector2)movementArea.bounds.center;
        Vector2 inward = (center - closest).sqrMagnitude > 1e-8f ? (center - closest).normalized : Vector2.zero;

        return closest + inward * 0.14f;
    }

    public void AIMoveMent()
    {
        Vector2 toPlayer = (Vector2)player.position - rb.position;
        float dist = toPlayer.magnitude;

        if (dist <= RecognizedArea)
        {
            Vector2 dir = toPlayer.sqrMagnitude > 1e-8f ? toPlayer.normalized : Vector2.zero;
            float speed = 3.0f;
            rb.linearVelocity = dir * speed;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
}

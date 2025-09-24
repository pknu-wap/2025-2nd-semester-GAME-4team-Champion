using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class NormalAlgorism : MonoBehaviour
{
    [Header("Common")]
    [SerializeField] private float AttackCooldown = 2f;
    public float SummonTimer = 5f;
    [SerializeField] private bool Summoned = false;
    public Slider BossTimerSlider;
    [SerializeField] private Transform Player;

    [Header("Health / Stamina")]
    public float MaxHp = 100f;
    public float CurrentHp = 100f;
    public float MaxStamina = 100f;
    public float CurrentStamina = 100f;
    [SerializeField] private float TimeColTime;

    [Header("General")]
    [SerializeField] private float RecognizedArea = 10f;
    [SerializeField] private float Speed = 3.0f;

    [Header("Dash (Melee)")]
    [SerializeField] private float DashSpeed = 12f;
    [SerializeField] private float SlowApproachSpeed = 1.5f;
    [SerializeField] private float PreDashSlowDuration = 0.5f;
    private float PreWindup;

    [Header("Stop In Front Of Player")]
    [SerializeField] private float StopOffset = 1.0f;
    [SerializeField] private LayerMask PlayerLayer;

    [Header("Dash (Range)")]
    [SerializeField] private float RetreatSpeed = 6f;
    [SerializeField] private float RetreatDuration = 1.25f;
    private float DesiredDistance;
    private float RangePreWindup;

    [Header("Projectile (Ranged Attack)")]
    [SerializeField] private GameObject ProjectilePrefab;
    [SerializeField] private Transform FirePoint;
    private int VolleyCount;
    [SerializeField] private float VolleyInterval = 0.2f;

    [Header("UI & Cinematic")]
    public Text Timer;
    public GameObject Hp;
    public GameObject Stamina;

    [Header("Rendering")]
    [SerializeField] private SpriteRenderer SpriteRenderer;
    [SerializeField] private Collider2D MovementArea;

    [Header("Runtime")]
    [SerializeField] private float AttackTimer = 0f;
    private Rigidbody2D Rb;
    private bool IsActing;
    private Collider2D PlayerCol;
    private Coroutine TypingCo;

    private void Start()
    {
        AttackTimer = 0f;
        Rb = GetComponent<Rigidbody2D>();

        if (Player != null)
        {
            PlayerCol = Player.GetComponent<Collider2D>();
        }

        Rb.position = ClampInside(Rb.position);
    }

    private void Update()
    {
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
            if (Rb != null) Rb.linearVelocity = Vector2.zero;
            return;
        }

        if (!IsActing)
        {
            AIMoveMent();
        }
    }

    private void AttackWayChoice()
    {
        int type = Random.Range(0, 2);
        switch (type)
        {
            case 0: Melee_Attack(); break;
            case 1: Range_Attack(); break;
        }
    }

    private void Melee_Attack()
    {
        if (IsActing) return;

        int meleeType = Random.Range(0, 3);
        switch (meleeType)
        {
            case 0:
                Debug.Log("근거리: 짧은 준비 후 돌진");
                PreWindup = 2f;
                break;
            case 1:
                Debug.Log("근거리: 보통 준비 후 돌진");
                PreWindup = 3f;
                break;
            case 2:
                Debug.Log("근거리: 긴 준비 후 돌진");
                PreWindup = 4f;
                break;
        }

        IsActing = true;
        StartCoroutine(MeleeDash());
    }

    private IEnumerator MeleeDash()
    {
        // 준비
        Rb.linearVelocity = Vector2.zero;
        if (SpriteRenderer) SpriteRenderer.color = Color.yellow;
        if (PreWindup > 0f) yield return new WaitForSeconds(PreWindup);

        // 느린 접근 (0.5초)
        float t = 0f;
        while (t < PreDashSlowDuration)
        {
            if (Player == null) break;
            Vector2 toPlayer = (Vector2)Player.position - Rb.position;
            Vector2 dirSlow = toPlayer.sqrMagnitude > 1e-8f ? toPlayer.normalized : Vector2.zero;
            Rb.linearVelocity = dirSlow * SlowApproachSpeed;
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        Rb.linearVelocity = Vector2.zero;

        // 빠른 돌진 (방향 고정, 직선)
        if (SpriteRenderer) SpriteRenderer.color = Color.red;
        Vector2 startPos = Rb.position;
        Vector2 lockedDir = ((Vector2)Player.position - startPos).normalized;
        if (lockedDir.sqrMagnitude < 1e-8f) lockedDir = Vector2.right;

        float dashTime = 0.3f;
        float elapsed = 0f;
        while (elapsed < dashTime)
        {
            Rb.linearVelocity = lockedDir * DashSpeed;
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // 종료
        Rb.linearVelocity = Vector2.zero;
        if (SpriteRenderer) SpriteRenderer.color = Color.white;
        yield return new WaitForSeconds(0.5f);

        IsActing = false;
    }

    private void Range_Attack()
    {
        if (IsActing) return;

        int rangeType = Random.Range(0, 3);
        switch (rangeType)
        {
            case 0:
                Debug.Log("원거리 공격: 후퇴 후 단발 (차징 1.0)");
                DesiredDistance = 5f;
                RangePreWindup = 1.0f;
                VolleyCount = 1;
                break;
            case 1:
                Debug.Log("원거리 공격: 후퇴 후 2연발 (차징 1.3)");
                DesiredDistance = 7f;
                RangePreWindup = 1.3f;
                VolleyCount = 2;
                break;
            case 2:
                Debug.Log("원거리 공격: 후퇴 후 3연발 (차징 1.5)");
                DesiredDistance = 9f;   
                RangePreWindup = 1.5f;
                VolleyCount = 3;
                break;
        }

        IsActing = true;
        StartCoroutine(RetreatThenFire());
    }

    private void FireOneProjectile()
    {
        if (ProjectilePrefab && FirePoint)
            Instantiate(ProjectilePrefab, FirePoint.position, Quaternion.identity);
    }

    private IEnumerator RetreatThenFire()
    {
        IsActing = true;
        if (SpriteRenderer) SpriteRenderer.color = Color.green;

        float t = 0f;
        while (t < RetreatDuration)
        {
            if (Player == null || Rb == null) break;

            Vector2 toPlayer = (Vector2)Player.position - Rb.position;
            float dist = toPlayer.magnitude;

            Vector2 dirAway = (-toPlayer).normalized;
            float step = RetreatSpeed * Time.fixedDeltaTime;

            Vector2 nextPos = Rb.position + dirAway * step;
            nextPos = ClampInside(nextPos);
            Rb.MovePosition(nextPos);

            if (DesiredDistance > 0f && dist >= DesiredDistance)
                break;

            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (Rb) Rb.linearVelocity = Vector2.zero;
        if (RangePreWindup > 0f) yield return new WaitForSeconds(RangePreWindup);

        if (SpriteRenderer) SpriteRenderer.color = Color.blue;
        for (int i = 0; i < VolleyCount; i++)
        {
            FireOneProjectile();
            if (i < VolleyCount - 1 && VolleyInterval > 0f)
                yield return new WaitForSeconds(VolleyInterval);
        }

        if (SpriteRenderer) SpriteRenderer.color = Color.red;
        IsActing = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!enabled) return;
        if (other.CompareTag("Player"))
        {
            float original = TimeColTime;
            Debug.Log("Damage = 10");
            TimeColTime = original;
            StopInFrontOfPlayer();
        }
    }

    private void StopInFrontOfPlayer()
    {
        if (Player == null || Rb == null) return;

        Vector2 dir = ((Vector2)Player.position - Rb.position).normalized;
        Vector2 stopPos = (Vector2)Player.position - dir * StopOffset;
        Rb.position = ClampInside(stopPos);
        Rb.linearVelocity = Vector2.zero;
        IsActing = false;
    }

    private Vector2 ComputeFrontTarget(Vector2 start, Vector2 dir)
    {
        float maxDist = 50f;
        Vector2 fallback = (Vector2)Player.position - dir * StopOffset;

        RaycastHit2D hitPlayer = Physics2D.Raycast(start, dir, maxDist, PlayerLayer);
        if (hitPlayer.collider != null)
            return hitPlayer.point - dir * StopOffset;

        return fallback;
    }

    private Vector2 ClampInside(Vector2 p)
    {
        if (MovementArea == null) return p;

        Vector2 closest = MovementArea.ClosestPoint(p);
        Vector2 center = (Vector2)MovementArea.bounds.center;
        Vector2 inward = (center - closest).sqrMagnitude > 1e-8f ? (center - closest).normalized : Vector2.zero;

        return closest + inward * 0.14f;
    }

    public void AIMoveMent()
    {
        Vector2 ToPlayer = (Vector2)Player.position - Rb.position;
        float Dist = ToPlayer.magnitude;

        if (Dist <= RecognizedArea)
        {
            Vector2 Dir = ToPlayer.sqrMagnitude > 1e-8f ? ToPlayer.normalized : Vector2.zero;
            Rb.linearVelocity = Dir * Speed;
        }
        else
        {
            Rb.linearVelocity = Vector2.zero;
        }
    }
}

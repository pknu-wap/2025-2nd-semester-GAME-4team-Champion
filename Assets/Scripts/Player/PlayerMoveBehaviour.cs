using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMoveBehaviour : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider Hp;
    [SerializeField] private Slider Stamina;
    [Header("Speed")]
    [SerializeField] private float moveSpeed = 7f;     // 최대 이동 속도 (u/s)
    [SerializeField] private bool normalizeDiagonal = true; // 대각선 보정

    [Header("Smoothing")]
    [SerializeField] private float accelTime = 0.06f;  // 가속 스무딩
    [SerializeField] private float decelTime = 0.08f;  // 감속 스무딩

    [Header("Input")]
    [SerializeField, Range(0f, 0.3f)] private float inputDeadzone = 0.05f; // 아주 미세한 입력 무시

    // ── 내부 상태 ───────────────────────────────────────────────────────────────
    private PlayerMove actions;          // 자동생성 InputActions
    private InputAction moveAction;      // Movement/Move (Vector2)
    private Rigidbody2D rb;

    private Vector2 desiredDir;          // 입력으로 원하는 방향(정규화/비정규화 이전)
    private Vector2 currentVel;          // 현재 속도 (월드 u/s)
    private Vector2 smoothVelRef;        // SmoothDamp용 참조 (ref 변수)

    // 성능: 미리 캐싱해두는 정적 상수
    private static readonly Vector2 V2_ZERO = Vector2.zero;

    // Move 방식을 고르고 싶다면 여기서 전환 (true=velocity, false=MovePosition)
    private const bool USE_RB_VELOCITY = true;

    private void Awake()
    {
        actions = new PlayerMove();
        moveAction = actions.Movement.Move;

        rb = GetComponent<Rigidbody2D>();
        // 물리적 보간 추천(카메라 떨림 방지). 프로젝트 상황에 맞춰 선택.
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void OnEnable() => actions.Movement.Enable();
    private void OnDisable() => actions.Movement.Disable();

    private void OnDestroy() => actions.Dispose();

    private void Update()
    {
        // 입력 읽기 (할당 없음)
        Vector2 raw = moveAction.ReadValue<Vector2>();

        // 데드존
        if (raw.sqrMagnitude < inputDeadzone * inputDeadzone) raw = V2_ZERO;

        // 대각선 보정 (정규화)
        if (normalizeDiagonal && raw != V2_ZERO)
            raw = raw.normalized;

        desiredDir = raw;
    }

    private void FixedUpdate()
    {
        // 목표 속도: 방향 * 최대속도
        Vector2 targetVel = desiredDir * moveSpeed;

        // 가속/감속 시간 분리 스무딩 (SmoothDamp: 프레임 독립적)
        float smooth = targetVel == V2_ZERO ? decelTime : accelTime;
        currentVel = Vector2.SmoothDamp(currentVel, targetVel, ref smoothVelRef, smooth, Mathf.Infinity, Time.fixedDeltaTime);

        if (USE_RB_VELOCITY)
        {
            // 물리엔진과 잘 어울림: 연속 충돌 모드일 때 특히 유리
            rb.linearVelocity = currentVel;
        }
        else
        {
            // MovePosition 사용 시에도 물리 일관성 유지
            rb.MovePosition(rb.position + currentVel * Time.fixedDeltaTime);
        }
    }
}

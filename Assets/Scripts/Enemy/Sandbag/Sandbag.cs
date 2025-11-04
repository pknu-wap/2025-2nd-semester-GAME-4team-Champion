using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using Unity.Cinemachine;

public class Sandbag : MonoBehaviour
{
    private int hitCount = 0;
    private bool isShaking = false;
    private bool isFlying = false;
    private bool hasFinished = false;
    private Rigidbody2D rb;

    public CameraLockOn camFollow;
    public Text scoreText;
    public Transform MainPotal;
    public SandBagPotal SandPotal;

    private Vector2 startPos;
    private Vector2 endPos;
    private Vector2 originalPos;

    public GameManager GameManager;
    public LevelManage LevelManage;

    private float lastClickTime = 0f;
    private bool isReturning = false;
    public GameObject MiniGame;
    [SerializeField] private CinemachineImpulseSource hitImpulse;

    void OnEnable()
    {
        rb = GetComponent<Rigidbody2D>();
        originalPos = transform.position;

        hitCount = 0;
        isShaking = false;
        isFlying = false;
        hasFinished = false;
        isReturning = false;
        lastClickTime = 0f;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = originalPos;

        if (scoreText != null)
            scoreText.text = "Score: 0";
    }
    
    void Update()
    {
        if (Input.GetMouseButtonDown(0) && !hasFinished && !isFlying)
        {
            hitCount++;
            Debug.Log("HitCount: " + hitCount);

            rb.MovePosition((Vector2)transform.position + new Vector2(0.01f, 0f));
            lastClickTime = Time.time;
            isReturning = false;

            hitImpulse.GenerateImpulse();

            if (!isFlying && !isShaking)
                StartCoroutine(ShakeAndFly());
        }

        if (!isReturning && Time.time - lastClickTime > 0.2f && !isFlying)
        {
            StartCoroutine(ReturnToOriginal());
        }

        if (isFlying && camFollow != null)
            camFollow.player = transform;
    }

    IEnumerator ShakeAndFly()
    {
        isShaking = true;
        yield return new WaitForSeconds(5f);

        isFlying = true;

        startPos = transform.position;
        float force = 8f * hitCount;
        rb.AddForce(Vector2.right * force, ForceMode2D.Impulse);

        yield return StartCoroutine(TrackDistance());

        isShaking = false;
        isFlying = false;
        hasFinished = true;

        StartCoroutine(MoveAfterDelay());
    }
    public void SetMainPotal(Transform portal)
    {
        MainPotal = portal;
    }

    IEnumerator TrackDistance()
    {
        yield return new WaitForSeconds(3f);
        endPos = transform.position;
        float distance = Vector2.Distance(startPos, endPos);
        int score = Mathf.RoundToInt(distance * 10);

        scoreText.text = "Score: " + score + "\nHitCount: " + hitCount;

        LevelManage.GetExp((int)(score * 0.7f));
    }

    IEnumerator ReturnToOriginal()
    {
        isReturning = true;

        float t = 0f;
        Vector2 currentPos = transform.position;
        while (t < 1f)
        {
            t += Time.deltaTime * 5f;
            rb.MovePosition(Vector2.Lerp(currentPos, originalPos, t));
            yield return null;
        }

        rb.MovePosition(originalPos);
        isReturning = false;
    }

    private IEnumerator MoveAfterDelay()
    {
        yield return StartCoroutine(GameManager.FadeOut(1.5f));

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        player.transform.position = MainPotal.position;

        SandPotal.EndSandbagMiniGame();
        
        yield return new WaitForSeconds(1.5f);
        yield return StartCoroutine(GameManager.FadeIn(1.5f));
        MiniGame.SetActive(false);
    }
}

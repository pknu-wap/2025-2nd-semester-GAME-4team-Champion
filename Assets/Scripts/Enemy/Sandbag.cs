using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class Sandbag : MonoBehaviour
{
    private int hitCount = 0;
    private bool isShaking = false;
    private bool isFlying = false;
    private bool hasFinished = false;
    private Rigidbody2D rb;

    public CameraFollow camFollow;
    public Text scoreText;
    public Transform MainPotal;
    public SandBagPotal SandPotal;

    private Vector2 startPos;
    private Vector2 endPos;
    private Vector2 originalPos;

    private float lastClickTime = 0f;
    private bool isReturning = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        originalPos = transform.position;

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

            if (CameraShaking.Instance != null && !CameraShaking.Instance.IsShaking)
                StartCoroutine(CameraShaking.Instance.Shake(0.5f, 0.2f));

            if (!isFlying && !isShaking)
                StartCoroutine(ShakeAndFly());
        }

        if (!isReturning && Time.time - lastClickTime > 0.2f && !isFlying)
        {
            StartCoroutine(ReturnToOriginal());
        }

        if (isFlying && camFollow != null)
            camFollow.target = transform;
    }

    IEnumerator ShakeAndFly()
    {
        isShaking = true;
        yield return new WaitForSeconds(5f);

        isFlying = true;

        startPos = transform.position;
        float force = 15f * hitCount;
        rb.AddForce(Vector2.right * force, ForceMode2D.Impulse);

        yield return StartCoroutine(TrackDistance());

        isShaking = false;
        isFlying = false;
        hasFinished = true;

        Debug.Log("🎯 샌드백 미니게임 종료! 3초 후 이동합니다...");
        SandPotal.EndSandbagMiniGame();
        StartCoroutine(MoveAfterDelay());
    }

    IEnumerator TrackDistance()
    {
        yield return new WaitForSeconds(3f);
        endPos = transform.position;
        float distance = Vector2.Distance(startPos, endPos);
        int score = Mathf.RoundToInt(distance * 10);

        if (scoreText != null)
            scoreText.text = "Score: " + score + "\nHitCount: " + hitCount;
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

    IEnumerator MoveAfterDelay()
    {
        yield return new WaitForSeconds(3f);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && MainPotal != null)
        {
            player.transform.position = MainPotal.position;
        }
    }
}

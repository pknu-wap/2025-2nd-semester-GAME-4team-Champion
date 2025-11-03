using UnityEngine;
using System.Collections;

public class RhythmPotal : MonoBehaviour
{
    public string playerTag = "Player";
    public Transform target;
    public GameObject Rhythm;
    public GameObject RhythmCanvas;
    public CameraLockOn camFollow;
    public Transform player;
    public GameManager GameManager;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            player.GetComponent<PlayerMoveBehaviour>().enabled = false;
            player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            StartCoroutine(FadeAndTeleport());
        }
    }

    private IEnumerator FadeAndTeleport()
    {
        yield return StartCoroutine(GameManager.FadeOut(1.5f));

        player.position = target.position;
        camFollow.player = Rhythm.transform;

        player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        player.GetComponent<SpriteRenderer>().flipX = true;

        Animator anim = player.GetComponent<Animator>();
        anim.SetFloat("moveY", 0);
        anim.SetFloat("moveX", 0);

        RhythmCanvas.SetActive(true);
    }

    public void ClickStartBTN()
    {
        RhythmCanvas.SetActive(false);
        StartCoroutine(StartRhythmWithFade());
    }

    private IEnumerator StartRhythmWithFade()
    {
        Rhythm.SetActive(true);
        yield return StartCoroutine(GameManager.FadeIn(1.0f));
    }

    public void EndRhythmMiniGame()
    {
        camFollow.player = player;
        player.GetComponent<PlayerMoveBehaviour>().enabled = true;
        player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.None;
    }
}
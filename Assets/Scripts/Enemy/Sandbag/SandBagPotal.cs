using UnityEngine;
using System.Collections;

public class SandBagPotal : MonoBehaviour
{
    public string playerTag = "Player";
    public Transform target;
    public GameObject sandbag;
    public GameObject Sandbag;
    public GameObject SandBagCanvas;
    public CameraLockOn camFollow;
    public Transform player;

    private Rigidbody2D rb;
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
        camFollow.player = Sandbag.transform;

        Transform mainPortal = transform.Find("SandBagBackPotal"); 
        Sandbag.GetComponent<Sandbag>().SetMainPotal(mainPortal);

        player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        player.GetComponent<SpriteRenderer>().flipX = false;

        Animator anim = player.GetComponent<Animator>();
        anim.SetFloat("moveY", 0);
        anim.SetFloat("moveX", 0);

        camFollow.followLerp = 100f;
        SandBagCanvas.SetActive(true);
    }

    public void ClickStartBTN()
    {
        SandBagCanvas.SetActive(false);
        StartCoroutine(StartSandBagWithFade());
    }

    private IEnumerator StartSandBagWithFade()
    {
        sandbag.SetActive(true);
        yield return StartCoroutine(GameManager.FadeIn(1.0f));
    }

    public void EndSandbagMiniGame()
    {
        camFollow.player = player;
        player.GetComponent<PlayerMoveBehaviour>().enabled = true;
        player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.None;
        camFollow.followLerp = 12f;
    }
}

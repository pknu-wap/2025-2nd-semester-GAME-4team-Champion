using UnityEngine;
using System.Collections;

public class SandBagPotal : MonoBehaviour
{
    public string playerTag = "Player";
    public Transform target;
    public GameObject sandbag;
    public GameObject SandBagCanvas;
    public CameraLockOn camFollow;
    public Transform player;

    private Rigidbody2D rb;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            other.transform.position = target.position;
            SandBagCanvas.SetActive(true);
            player.GetComponent<PlayerMoveBehaviour>().enabled = false;
            player.GetComponent<SpriteRenderer>().flipX = false;
            Animator anim = player.GetComponent<Animator>();
            player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            anim.SetFloat("moveY", 0);
            anim.SetFloat("moveX", 0);

            camFollow.followLerp = 100f;
        }
    }

    public void ClickStartBTN()
    {
        sandbag.SetActive(true);
        SandBagCanvas.SetActive(false);
    }

    public void EndSandbagMiniGame()
    {
        camFollow.player = player;
        player.GetComponent<PlayerMoveBehaviour>().enabled = true;
        player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.None;
        sandbag.SetActive(false);
        camFollow.followLerp = 12f;
    }
}

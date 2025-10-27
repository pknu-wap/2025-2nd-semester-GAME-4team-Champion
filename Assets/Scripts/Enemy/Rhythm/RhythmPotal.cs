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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            other.transform.position = target.position;
            camFollow.player = Rhythm.transform;
            player.GetComponent<PlayerMoveBehaviour>().enabled = false;
            RhythmCanvas.SetActive(true);
            player.GetComponent<SpriteRenderer>().flipX = true;
            Animator anim = player.GetComponent<Animator>();
            player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            anim.SetFloat("moveY", 0);
            anim.SetFloat("moveX", 0);
        }
    }

    public void ClickStartBTN()
    {
        Rhythm.SetActive(true);
        RhythmCanvas.SetActive(false);
    }

    public void EndRhythmMiniGame()
    {
        camFollow.player = player;
        player.GetComponent<PlayerMoveBehaviour>().enabled = true;
        player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.None;
        Rhythm.SetActive(false);
    }
}

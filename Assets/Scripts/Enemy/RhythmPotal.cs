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
            StartRhythmMiniGame();
        }
    }

    private void StartRhythmMiniGame()
    {
        if (camFollow != null && Rhythm != null)
        {
            camFollow.player = Rhythm.transform;
        }
        player.GetComponent<PlayerMoveBehaviour>().enabled = false;
        RhythmCanvas.SetActive(true);
    }

    public void ClickStartBTN()
    {
        Rhythm.SetActive(true);
        RhythmCanvas.SetActive(false);
    }

    public void EndRhythmMiniGame()
    {
        if (camFollow != null && player != null)
        {
            camFollow.player = player;
            player.GetComponent<PlayerMoveBehaviour>().enabled = true;
        }
    }
}
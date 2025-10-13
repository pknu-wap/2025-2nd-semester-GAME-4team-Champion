using UnityEngine;
using System.Collections;

public class RhythmPotal : MonoBehaviour
{
    public string playerTag = "Player";
    public Transform target;
    public GameObject Rhythm;
    public CameraFollow camFollow;
    public Transform player;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            other.transform.position = target.position;
            StartCoroutine(StartRhythmMiniGame());
        }
    }

    IEnumerator StartRhythmMiniGame()
    {
        if (camFollow != null && Rhythm != null)
        {
            camFollow.target = Rhythm.transform;
        }
        Debug.Log("잠시 후 게임을 시작합니다.");
        yield return new WaitForSeconds(3f);

        if (Rhythm != null)
        {
            Rhythm.SetActive(true);
        }
    }

    public void EndRhythmMiniGame()
    {
        if (camFollow != null && player != null)
        {
            camFollow.target = player;
        }
    }
}
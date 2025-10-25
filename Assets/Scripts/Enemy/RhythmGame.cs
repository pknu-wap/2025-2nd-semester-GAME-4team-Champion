using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class RhythmGame : MonoBehaviour
{
    [Header("Prefabs & Spawn")]
    public GameObject AttackPrefab, DefensePrefab, ChargePrefab;
    public Transform spawnPoint, targetPoint, rhythmParent, MainPotal;
    public RhythmPotal RhythmPotal;
    public float noteSpeed = 3f;

    [Header("UI")]
    public Text scoreText;

    private int lastNoteType = 0;
    private int score = 0;
    private List<RhythmNote> activeNotes = new List<RhythmNote>();
    private bool isCharging = false;
    private float chargeValue = 0f;
    private bool isGameEnded = false;
    private bool allNotesSpawned = false; // ✅ 스폰 완료 여부 플래그

    void Start()
    {
        if (rhythmParent == null)
        {
            GameObject parentObj = GameObject.Find("MiniGame_Rhythm");
            if (parentObj != null)
                rhythmParent = parentObj.transform;
        }

        UpdateScoreText();
        StartCoroutine(SpawnRoutine());
    }

    void Update()
    {
        HandleInput();

        // ✅ 모든 노트가 사라졌고, 스폰도 끝났고, 아직 종료되지 않았다면
        if (allNotesSpawned && !isGameEnded && activeNotes.Count == 0)
        {
            Debug.Log("🎯 모든 노트가 처리됨! 게임 종료로 이동");
            EndGame();
        }
    }

    private IEnumerator SpawnRoutine()
    {
        for (int i = 0; i < 20; i++)
        {
            SpawnNote();
            yield return new WaitForSeconds(Random.Range(0.4f, 1f));
        }

        allNotesSpawned = true; // ✅ 스폰 완료 플래그 설정
        Debug.Log("🎵 모든 노트 스폰 완료");
    }

    private void SpawnNote()
    {
        int noteType = (lastNoteType == 2 && Random.value <= 0.8f) ? 1 : Random.Range(1, 4);
        GameObject prefab = (noteType == 1) ? AttackPrefab : (noteType == 2) ? DefensePrefab : ChargePrefab;

        GameObject noteObj = Instantiate(prefab, spawnPoint.position, Quaternion.identity, rhythmParent);
        RhythmNote rhythmNote = noteObj.GetComponent<RhythmNote>();

        if (rhythmNote != null)
            rhythmNote.Initialize(targetPoint.position, noteSpeed, noteType);

        activeNotes.Add(rhythmNote);
        lastNoteType = noteType;
    }

    private void HandleInput()
    {
        if (isGameEnded) return;

        if (Input.GetMouseButtonDown(0)) TryHit(1);
        if (Input.GetMouseButtonDown(1)) TryHit(2);

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            foreach (var note in activeNotes)
            {
                if (note != null && note.NoteType == 3 && note.CanBeHit)
                {
                    isCharging = true;
                    chargeValue = 0f;
                    note.StartShrink();
                }
            }
        }

        if (Input.GetKey(KeyCode.Tab) && isCharging)
        {
            chargeValue += Time.deltaTime * 25f;
            chargeValue = Mathf.Clamp(chargeValue, 0f, 100f);
        }

        if (Input.GetKeyUp(KeyCode.Tab))
        {
            if (isCharging)
            {
                int gainedScore = Mathf.RoundToInt(chargeValue * 0.2f);
                score += gainedScore;
                Debug.Log($"⚡ Charge Success! {chargeValue:F1}% → +{gainedScore}점");
                UpdateScoreText();

                // ✅ 차지 노트 제거
                for (int i = activeNotes.Count - 1; i >= 0; i--)
                {
                    RhythmNote note = activeNotes[i];
                    if (note == null)
                    {
                        activeNotes.RemoveAt(i);
                        continue;
                    }

                    if (note.NoteType == 3 && note.IsShrinking && note.CanBeHit)
                    {
                        Destroy(note.gameObject);
                        activeNotes.RemoveAt(i);
                    }
                }

                chargeValue = 0f;
                isCharging = false;
            }
        }
    }

    private void TryHit(int type)
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            RhythmNote note = activeNotes[i];
            if (note == null)
            {
                activeNotes.RemoveAt(i);
                continue;
            }

            if (note.CanBeHit && note.NoteType == type)
            {
                score += 3;
                UpdateScoreText();
                Debug.Log($"✅ Hit! Type:{type}, +3점");
                Destroy(note.gameObject);
                activeNotes.RemoveAt(i);
                break;
            }

            if (note.NoteType == 3 && note.IsShrinking && note.CanBeHit)
            {
                Destroy(note.gameObject);
                activeNotes.RemoveAt(i);
            }
        }
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score}";
    }

    private void EndGame()
    {
        if (isGameEnded) return;
        isGameEnded = true;

        Debug.Log("🎮 리듬게임 종료! 3초 후 이동합니다...");

        if (RhythmPotal != null)
            RhythmPotal.EndRhythmMiniGame();

        StartCoroutine(MoveAfterDelay());
    }

    private IEnumerator MoveAfterDelay()
    {
        yield return new WaitForSeconds(3f);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && MainPotal != null)
        {
            player.transform.position = MainPotal.position;
            Debug.Log("✅ 플레이어가 MainPotal로 이동했습니다!");
        }
    }
}

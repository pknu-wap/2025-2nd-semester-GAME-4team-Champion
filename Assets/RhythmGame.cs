using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class RhythmGame : MonoBehaviour
{
    [Header("Prefabs & Spawn")]
    public GameObject AttackPrefab;
    public GameObject DefensePrefab;
    public GameObject ChargePrefab;
    public Transform spawnPoint;
    public Transform targetPoint;
    public Transform rhythmParent;
    public float noteSpeed = 3f;

    [Header("UI")]
    public Text scoreText;

    private int lastNoteType = 0;
    private int score = 0;
    private List<RhythmNote> activeNotes = new List<RhythmNote>();
    private bool isCharging = false;
    private float chargeValue = 0f;

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
    }

    private IEnumerator SpawnRoutine()
    {
        for (int i = 0; i < 20; i++)
        {
            SpawnNote();
            yield return new WaitForSeconds(Random.Range(0.4f, 1f));
        }
    }

    private void SpawnNote()
    {
        int noteType;

        if (lastNoteType == 2 && Random.value <= 0.8f)
            noteType = 1;
        else
            noteType = Random.Range(1, 4);

        GameObject prefab = null;
        switch (noteType)
        {
            case 1: prefab = AttackPrefab; break;
            case 2: prefab = DefensePrefab; break;
            case 3: prefab = ChargePrefab; break;
        }

        GameObject noteObj = Instantiate(prefab, spawnPoint.position, Quaternion.identity, rhythmParent);
        RhythmNote rhythmNote = noteObj.GetComponent<RhythmNote>();

        if (rhythmNote != null)
            rhythmNote.Initialize(targetPoint.position, noteSpeed, noteType);

        activeNotes.Add(rhythmNote);
        lastNoteType = noteType;
    }

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
            TryHit(1);

        if (Input.GetMouseButtonDown(1))
            TryHit(2);

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
}

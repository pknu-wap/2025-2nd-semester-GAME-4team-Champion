using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;
public class TutorialManager : MonoBehaviour
{
    public TMP_Text TutorialText;
    public GameObject Enemy;
    public GameObject Player;
    public Transform Position;

    private bool PressedW, PressedA, PressedS, PressedD;
    private bool MoveCompleted = false;
    private bool AttackCompleted = false;
    private bool GuardCompleted = false;
    private bool ParryCompleted = false;
    private bool ParryAttackCompleted = false;

    void Start()
    {
        TutorialText.text = "움직임 : WASD";
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.W)) PressedW = true;
        if (Input.GetKeyDown(KeyCode.A)) PressedA = true;
        if (Input.GetKeyDown(KeyCode.S)) PressedS = true;
        if (Input.GetKeyDown(KeyCode.D)) PressedD = true;

        if (!MoveCompleted && PressedW && PressedA && PressedS && PressedD)
        {
            MoveCompleted = true;
            TutorialText.text = "움직임 : WASD (성공)\n공격 : 좌클릭";
        }

        if (MoveCompleted && !AttackCompleted && Input.GetMouseButtonDown(0))
        {
            AttackCompleted = true;
            TutorialText.text = "움직임 : WASD (성공)\n공격 : 좌클릭 (성공)\n가드 : 우클릭";
        }

        if (AttackCompleted && !GuardCompleted && Input.GetMouseButtonDown(1))
        {
            GuardCompleted = true;
            TutorialText.text = "움직임 : WASD (성공)\n공격 : 좌클릭 (성공)\n가드 : 우클릭 (성공)";
            StartCoroutine(Parry());
        }

        if (GuardCompleted && Enemy.GetComponent<EnemyFight_01>().Tutorial_Checker3)
        {
            Enemy.transform.position = Position.position;
        }

        if (GuardCompleted && !ParryCompleted && Player.GetComponent<PlayerHit>().Tutorial_Checker)
        {
            ParryCompleted = true;
            StartCoroutine(SuccessParry());
        }

        if (ParryCompleted && !ParryAttackCompleted && Player.GetComponent<PlayerHit>().Tutorial_Checker2)
        {
            Enemy.SetActive(false);
            StartCoroutine(SuccessParryAttack());
        }
    }

    private IEnumerator Parry()
    {
        yield return new WaitForSeconds(2f);
        TutorialText.text = "우클릭을 꾹 눌러 공격을 방어하세요";
        Enemy.SetActive(true);
    }
    private IEnumerator SuccessParry()
    {
        yield return new WaitForSeconds(2f);
        TutorialText.text = "공격 타이밍에 맞춰 우클릭을 눌러 패링하세요";
    }
    private IEnumerator SuccessParryAttack()
    {
        yield return new WaitForSeconds(2f);
        TutorialText.text = "패링한 후에는 바로 공격하여 카운터를 날릴 수 있습니다";
        yield return new WaitForSeconds(4f);
        TutorialText.text = "";
        SceneManager.LoadScene("Ingame");
    }
}

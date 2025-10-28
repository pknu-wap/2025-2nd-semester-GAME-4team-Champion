using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
public class TutorialManager : MonoBehaviour
{
    public Text TutorialText;
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
        TutorialText.text = "Move : WASD ☐";
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
            TutorialText.text = "Move : WASD ☑\nPunch : Left-click ☐";
        }

        if (MoveCompleted && !AttackCompleted && Input.GetMouseButtonDown(0))
        {
            AttackCompleted = true;
            TutorialText.text = "Move : WASD ☑\nPunch : Left-click ☑\nGuard : Right-click ☐";
        }

        if (AttackCompleted && !GuardCompleted && Input.GetMouseButtonDown(1))
        {
            GuardCompleted = true;
            TutorialText.text = "Move : WASD ☑\nPunch : Left-click ☑\nGuard : Right-click ☑";
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
        TutorialText.text = "Continuously Right-click to guard";
        Enemy.SetActive(true);
    }
    private IEnumerator SuccessParry()
    {
        yield return new WaitForSeconds(2f);
        TutorialText.text = "Right-click to parry\nafter parrying immediately";
    }
    private IEnumerator SuccessParryAttack()
    {
        yield return new WaitForSeconds(2f);
        TutorialText.text = "After parrying\nyou can counterattack by Left-click";
        yield return new WaitForSeconds(2f);
        TutorialText.text = "";
        SceneManager.LoadScene("Ingame");
    }
}

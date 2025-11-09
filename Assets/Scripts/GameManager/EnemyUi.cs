using UnityEngine;
using UnityEngine.UI;
using TMPro;

//enemy 기절시 !보여주는 코드
public class EnemyUi : MonoBehaviour
{
    /*[SerializeField] private EnemyCore_01 enemycore01;
    [SerializeField] private EnemyCore_02 enemycore02;
    [SerializeField] private BossCore mbosscore;*/

    [SerializeField] private GameObject mark;

    void Start()
    {
        HideMark();
    }

    public void ShowMark()
    {
        mark.SetActive(true);
    }

    public void HideMark()
    {
        mark.SetActive(false);
    }
}

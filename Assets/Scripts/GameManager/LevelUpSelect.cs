using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

public class LevelUpSelect : MonoBehaviour
{
    public GameManager gamemanager;
    public LevelManage levelmanage;
    public DayTimer daytimer;

    public TextMeshProUGUI[] selectButtonsText;
    
    public List<string> allselectTitle = new List<string> {"체력 증가","스테미나 증가","가드시 스테미나 증가량 다운", "위빙 성공시 체력 회복", "스테미나 회복 속도 증가", 
                                                            "기합 회복량 증가", " 기합 횟수 증가", "공격시 적 스테미나 감소율 증가", " 적 그로기 성공시 체력 회복", "적 그로기 시간 증가", 
                                                            "차지 속도 증가", "기합 횟수 증가", "공격시 적 스테미나 감소율 증가"};
    public List<string> unselectedTitle;
    public List<string> randomTitle = new List<string> {"a","a","a"};
    public List<string> selectedList = new List<string>();

    private Dictionary<string, Action> upgradeActions;

    public GameObject[] gameui;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        InitializeUpgradeActions();
        settingRandom();
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < 3; i++)
        {
            selectButtonsText[i].text = randomTitle[i];
        }
    }

    public void RandomSelect()     //선택지 랜덤
    {
        for (int i = 0; i < 3; i++)
        {
            int rand = UnityEngine.Random.Range(0, unselectedTitle.Count);
            randomTitle[i] = unselectedTitle[rand];

            unselectedTitle.Remove(randomTitle[i]);     //중복제거
        } 
    }

    public void settingRandom()     //중복 없이 선택지 띄우기 위한 세팅
    {
        unselectedTitle = new List<string>(allselectTitle);
        unselectedTitle.RemoveAll(x => selectedList.Contains(x));
    }

    public void selectrandomvalue(int index)    //선택지 중 하나 선택
    {
        selectedList.Add(randomTitle[index]);
        levelmanage.levelselectcount -= 1;
        
        if (upgradeActions.ContainsKey(randomTitle[index]))
        {
            upgradeActions[randomTitle[index]].Invoke();
        }
        

        if (levelmanage.levelselectcount >= 1)
        {
            settingRandom();
            RandomSelect();
            showLevelUp();
        }
        else
        {
            daytimer.StartTimer();
        }

    }

    public void showLevelUp()
    {
        //levelupui.SetActive(true);
        gameui[0].SetActive(true);
    }

    public void exitLevelUp()
    {
        //levelupui.SetActive(false);
        gameui[0].SetActive(false);
    }

     private void InitializeUpgradeActions()
    {
        upgradeActions = new Dictionary<string, Action>
        {
            { "체력 증가", () => { gamemanager.maxhp += 20; gamemanager.currenthp += 20; } },
            { "스테미나 증가", () => { gamemanager.maxstamina += 20; } },
            { "가드시 스테미나 증가량 다운", () => { gamemanager.reducestamina += 10; } },
            { "위빙 성공시 체력 회복", () => { gamemanager.gainhp += 10; } },
            { "스테미나 회복 속도 증가", () => { gamemanager.playerstaminaregen += 8; } },

        };
    }
}

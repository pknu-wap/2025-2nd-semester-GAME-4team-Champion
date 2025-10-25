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
                                                            "기합 회복량 증가", "기합 횟수 증가", "공격시 적 스테미나 감소율 증가", "적 그로기 성공시 체력 회복", "적 그로기 시간 증가", 
                                                            "차지 속도 증가", "위빙 성공시 스킬 쿨감", "공격 성공시 스킬 쿨감"};
    public List<string> unselectedTitle;
    public List<string> randomTitle = new List<string> {"a","a","a"};
    public List<string> selectedList = new List<string>();

    private Dictionary<string, Action> upgradeActions; 
    public List<int> selectCountList = new List<int>(); //선택 횟수

    public GameObject[] gameui; //레벨업 창

    public class CombinationEffect  //선택지 콤보
    {
        public string[] titles;       // 조합 조건
        public Action effect;         // 발동 효과
    }
    public List<CombinationEffect> combinationEffects = new List<CombinationEffect>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        for (int i = 0; i < allselectTitle.Count; i++)
        {
            selectCountList.Add(0);
        }

        InitializeUpgradeActions();
        settingRandom();
        RandomSelect();
        InitializeCombinationEffects();
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < 3; i++)
        {
            int cnt = allselectTitle.IndexOf(randomTitle[i]);
            if (cnt >= 0)
                selectButtonsText[i].text = $"{randomTitle[i]} ({selectCountList[cnt]}/2)";
            else
                selectButtonsText[i].text = $"{randomTitle[i]} (0/2)";        
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
        unselectedTitle = new List<string>();
        for (int i = 0; i < allselectTitle.Count; i++)
        {
            if (selectCountList[i] < 2)
            {
                unselectedTitle.Add(allselectTitle[i]);
            }
        }
    }

    public void selectrandomvalue(int index)    //선택지 중 하나 선택
    {
    
        selectedList.Add(randomTitle[index]);
        levelmanage.levelselectcount -= 1;

        int titleIndex = allselectTitle.IndexOf(randomTitle[index]);    
        if (titleIndex >= 0)    //카운트 세기
            {
                selectCountList[titleIndex] += 1;
            }

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
            //daytimer.StartTimer();
            Time.timeScale = 1f;    //시간 재개
        }

        CheckCombinationEffects();
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
            { "기합 회복량 증가", () => { gamemanager.playerstaminaregen += 8; } },
            { " 기합 횟수 증가", () => { gamemanager.playerstaminaregen += 8; } },
            { "공격시 적 스테미나 감소율 증가", () => { gamemanager.playerstaminaregen += 8; } },
            { " 적 그로기 성공시 체력 회복", () => { gamemanager.playerstaminaregen += 8; } },
            { "적 그로기 시간 증가", () => { gamemanager.playerstaminaregen += 8; } },
            { "차지 속도 증가", () => { gamemanager.playerstaminaregen += 8; } },
            { "공격 성공시 스킬 쿨감", () => { gamemanager.playerstaminaregen += 8; } },
            { "위빙 성공시 스킬 쿨감", () => { gamemanager.playerstaminaregen += 8; } },            
        };
    }

    public void CheckCombinationEffects()
    {
        foreach (var combo in combinationEffects)
        {
            bool allMet = true;
            foreach (var title in combo.titles)
            {
                int idx = allselectTitle.IndexOf(title);
                if (idx < 0 || selectCountList[idx] < 2) // 2번 선택 확인
                {
                    allMet = false;
                    break;
                }
            }
            if (allMet)
            {
                combo.effect.Invoke();  // 조건 만족하면 즉시 발동
                Debug.Log($"콤보 효과 발동: {string.Join(" + ", combo.titles)}");
            }
        }
    }

    private void InitializeCombinationEffects() //콤보 값 설정
    {
        combinationEffects = new List<CombinationEffect>
        {
            new CombinationEffect
            {
                titles = new string[] { "체력 증가", "스테미나 증가" },
                effect = () => { gamemanager.maxhp += 50; gamemanager.currenthp += 50; }
            },

        };
    }

}

using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class LevelUpSelect : MonoBehaviour
{
    public GameManager gamemanager;

    public TextMeshProUGUI[] selectButtonsText;
    
    public List<string> allselectTitle = new List<string> {"hp", "fast_stamina", "guard_more_enemy_stamina", "guard", "stamina", "f", "g"};
    public List<string> unselectedTitle;

    public List<string> randomTitle = new List<string> {"a","a","a"};

    public List<string> selectedList = new List<string>();
    public List<int> selectedid;

    public GameObject[] gameui;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
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
        
        if (randomTitle[index] == allselectTitle[0])
        {
            gamemanager.maxhp += 20;
            gamemanager.currenthp += 20;
        }
        if (randomTitle[index] == allselectTitle[1])
        {
            gamemanager.playerstaminaregen += 8;
        }
        if (randomTitle[index] == allselectTitle[2])
        {
            gamemanager.playerpower[0] += 10;
        }
        if (randomTitle[index] == allselectTitle[3])
        {
            gamemanager.playerpower[1] -= 10;
        }
        if (randomTitle[index] == allselectTitle[4])
        {
            gamemanager.maxstamina += 20;
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
}

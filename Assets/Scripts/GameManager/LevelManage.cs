using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class LevelManage : MonoBehaviour
{
    public Slider expbar;
    public int exp = 0;         //현재 경험치
    public int maxexp = 100;    //최대 경험치(도달시 레벨업)

    public TextMeshProUGUI leveltext;
    public int level = 1;
    public int levelselectcount = 1;    //레벨업시 레벨업 창 뜨는 횟수 카운트

    public LevelUpSelect levelupselect; 

    void Start()
    {
        resetexp();
        if (level == 1 && exp == 0)
        {
            levelupselect.settingRandom();
            levelupselect.RandomSelect();
            levelupselect.showLevelUp();
        }
    }

    void Update()
    {   
        leveltext.text = $"LV.{level}";
    }

    public void GetExp(int addexp)  //경험치 휙득 및 레벨업
    {
        exp += addexp;
        resetexp();
        /*if (exp >= maxexp)
        {
            level += exp/maxexp;
            exp %= maxexp;
        }*/
        if (exp >= maxexp)
        {
            while (exp >= maxexp)
            {
                exp -= maxexp;
                level += 1;
                if (level % 5 == 0)
                {   
                    levelselectcount += 1;
                }
                if (levelselectcount >= 1)
                {
                    levelupselect.settingRandom();
                    levelupselect.RandomSelect();
                    levelupselect.showLevelUp();
        
                }
                
            }
            /*for (int i = 0; i <= (exp/maxexp); i++)
            {
                level += 1;
                exp -= maxexp;
                if (level % 5 == 0)
                {
                    levelupselect.settingRandom();
                    levelupselect.RandomSelect();
                    levelupselect.showLevelUp();
                }
            }*/
        }
        
        resetexp();
    }



    public void resetexp()  //화면에 보이는 경험치 바 갱신
    {
        expbar.value = (float)exp/maxexp;
    }
    
}

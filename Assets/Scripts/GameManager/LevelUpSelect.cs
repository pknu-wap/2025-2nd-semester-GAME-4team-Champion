using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class LevelUpSelect : MonoBehaviour
{
    public GameManager gamemanager;

    public TextMeshProUGUI[] selectButtonsText;

    public List<string> allselectTitle = new List<string>
    {
        "hp", "fast_stamina", "guard_more_enemy_stamina", "guard", "stamina", "f", "g"
    };

    public List<string> unselectedTitle;
    public List<string> randomTitle = new List<string> { "", "", "" };
    public List<string> selectedList = new List<string>();
    public GameObject[] gameui;

    [SerializeField] private PlayerCombat player;

    void Start()
    {
        if (player == null)
        {
            player = FindObjectOfType<PlayerCombat>();
        }

        settingRandom();
        RandomSelect();
        RefreshOptionTexts();
    }

    void RefreshOptionTexts()
    {
        if (selectButtonsText == null) { return; }
        for (int i = 0; i < selectButtonsText.Length && i < 3; i++)
        {
            if (selectButtonsText[i] != null)
            {
                selectButtonsText[i].text = randomTitle[i];
            }
        }
    }

    public void RandomSelect()
    {
        if (unselectedTitle == null || unselectedTitle.Count < 3)
        {
            settingRandom();
        }

        if (unselectedTitle.Count < 3)
        {
            unselectedTitle = new List<string>(allselectTitle);
        }

        for (int i = 0; i < 3; i++)
        {
            int rand = UnityEngine.Random.Range(0, unselectedTitle.Count);
            randomTitle[i] = unselectedTitle[rand];
            unselectedTitle.RemoveAt(rand);
        }

        RefreshOptionTexts();
    }

    public void settingRandom()
    {
        unselectedTitle = new List<string>(allselectTitle);
        if (selectedList != null && selectedList.Count > 0)
        {
            unselectedTitle.RemoveAll(x => selectedList.Contains(x));
        }
    }

    public void selectrandomvalue(int index)
    {
        if (index < 0 || index >= 3) { return; }
        string pick = randomTitle[index];
        if (string.IsNullOrEmpty(pick)) { return; }

        selectedList.Add(pick);

        // if (player != null)
        // {
        //     if (pick == "hp")
        //     {
        //         player.AddMaxHp(20f, true);
        //     }
        //     if (pick == "fast_stamina")
        //     {
        //         player.AddStaminaRegen(8f);
        //     }
        //     if (pick == "guard_more_enemy_stamina")
        //     {
        //         player.AddJustGuardEnemyStaminaBonus(10f);
        //     }
        //     if (pick == "guard")
        //     {
        //         player.AddGuardStaminaGainBonus(-10f);
        //     }
        //     if (pick == "stamina")
        //     {
        //         player.AddMaxStamina(20f);
        //     }
        // }

        settingRandom();
        RandomSelect();
        RefreshOptionTexts();
    }

    public void showLevelUp()
    {
        if (gameui != null && gameui.Length > 0 && gameui[0] != null)
        {
            gameui[0].SetActive(true);
        }
    }

    public void exitLevelUp()
    {
        if (gameui != null && gameui.Length > 0 && gameui[0] != null)
        {
            gameui[0].SetActive(false);
        }
    }
}

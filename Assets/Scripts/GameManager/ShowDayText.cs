using UnityEngine;
using TMPro;

public class ShowDayText : MonoBehaviour
{
    public TextMeshProUGUI timertext;
    private int min = 30;
    
    public void TextAlpha()
    {
        Color c = timertext.color;
        c.a = 1f;
        timertext.color = c;
    }

    public void ShowAlpha()
    {   
        min -= 1;

        Color c = timertext.color;
        c.a = 0f;
        timertext.color = c;

        timertext.text = $"D-{min}\n(M)";
    }
}

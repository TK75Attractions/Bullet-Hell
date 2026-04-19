using UnityEngine;
using TMPro;

public class UIHead : MonoBehaviour
{
    private TMP_Text sceneText;
    private TMP_Text timerText;

    public void Init()
    {
        sceneText = transform.Find("SceneText").GetComponent<TMP_Text>();
        timerText = transform.Find("TimerText").GetComponent<TMP_Text>();
    }

    public void UpdateUI(float time) => timerText.text = time.ToString("F2");

    public void SetSceneText(string text) => sceneText.text = text;
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class DiagnosisOptionView : MonoBehaviour
{
    public Button button;
    public TMP_Text label;
    public Image background;

    public void Bind(string text, bool selected, UnityAction action)
    {
        label.text = selected ? $"<b>{text}</b>\n<size=14>Seleccionado</size>" : text;
        background.color = selected ? new Color(0.75f, 0.87f, 1f) : Color.white;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }
}

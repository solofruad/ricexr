using UnityEngine;
using UnityEngine.UI;

public class OptionsController : MonoBehaviour
{
    [Header("Botones")]
    [SerializeField] private Button skipTutorialButton;
    [SerializeField] private Button disableVoicesButton;
    [SerializeField] private Button returnToMainMenuButton;

    [Header("Sliders")]
    [SerializeField] private Slider voiceVelocitySlider;
}

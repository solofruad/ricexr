using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// MENÚ PRINCIPAL
/// 
/// Controla la pantalla de inicio que se muestra al arrancar la aplicación.
/// Tiene dos botones:
///   1. "Iniciar Pruebas"  ? oculta el menú, inicia la sesión de métricas y activa el SceneInteractionManager
///   2. "Ver Leaderboard"  ? muestra el panel del leaderboard (LeaderboardController lo llena)
/// 
/// Cómo configurar en Unity:
///   - Crear un Canvas WorldSpace (o Screen Space) con dos botones y asignarlos aquí
///   - Asegurarse de que SceneInteractionManager NO llame a nada en su Start/Awake
///     (ya se modificó para esperar WaitForStartSignal)
/// </summary>
public class MainMenuController : MonoBehaviour
{
[Header("Paneles")]
    [Tooltip("El panel raíz del menú principal (se oculta al iniciar)")]
 [SerializeField] private GameObject mainMenuPanel;

    [Tooltip("El panel del leaderboard")]
    [SerializeField] private GameObject leaderboardPanel;

    [Header("Botones")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button leaderboardButton;
    [SerializeField] private Button leaderboardBackButton;   // botón "Volver" dentro del leaderboard

    [Header("Referencias")]
    [Tooltip("El GameObject que tiene el SceneInteractionManager (para activarlo al iniciar)")]
    [SerializeField] private SceneInteractionManager sceneInteractionManager;

  [Tooltip("El LeaderboardController que llena la lista")]
    [SerializeField] private LeaderboardController leaderboardController;

    // ?? Lifecycle ????????????????????????????????????????????????????????
    void Start()
    {
        // Asegurarse de que el leaderboard esté oculto al inicio
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
if (mainMenuPanel    != null) mainMenuPanel.SetActive(true);

     // Suscribir botones
  if (startButton       != null) startButton.onClick.AddListener(OnStartClicked);
        if (leaderboardButton != null) leaderboardButton.onClick.AddListener(OnLeaderboardClicked);
      if (leaderboardBackButton != null) leaderboardBackButton.onClick.AddListener(OnLeaderboardBackClicked);
    }

    // ?? Callbacks ????????????????????????????????????????????????????????

    private void OnStartClicked()
    {
   // 1. Ocultar el menú
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);

// 2. Iniciar el registro de métricas
        if (SessionMetricsTracker.Instance != null)
            SessionMetricsTracker.Instance.StartSession();

        // 3. Dar la señal al SceneInteractionManager para que procese el primer nivel
        if (sceneInteractionManager != null)
        sceneInteractionManager.WaitForStartSignal();
        else
  Debug.LogWarning("[MainMenu] No se encontró SceneInteractionManager.");
    }

    private void OnLeaderboardClicked()
    {
        if (mainMenuPanel    != null) mainMenuPanel.SetActive(false);
        if (leaderboardPanel != null) leaderboardPanel.SetActive(true);

        // Pedir al leaderboard que llene la lista
     if (leaderboardController != null)
    leaderboardController.Populate();
}

    private void OnLeaderboardBackClicked()
    {
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
  if (mainMenuPanel    != null) mainMenuPanel.SetActive(true);
    }

    // ?? Limpieza ?????????????????????????????????????????????????????????
    void OnDestroy()
    {
   if (startButton    != null) startButton.onClick.RemoveListener(OnStartClicked);
        if (leaderboardButton     != null) leaderboardButton.onClick.RemoveListener(OnLeaderboardClicked);
        if (leaderboardBackButton != null) leaderboardBackButton.onClick.RemoveListener(OnLeaderboardBackClicked);
    }
}

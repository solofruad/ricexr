using System; // Necesario para el evento Action
using UnityEngine;
using UnityEngine.UIElements;
using DG.Tweening;
using UnityEngine.Events;

public class LoadingWaveController : MonoBehaviour
{
    [Header("Configuración de Tiempos")]
    [Tooltip("Segundos que tarda la barra en ir de 0% a 100%.")]
    [SerializeField] private float loadDuration = 5f;
    
    [Tooltip("Segundos que tarda la barra en volverse invisible al reiniciar (y las olas en reaparecer).")]
    [SerializeField] private float fadeDuration = 1f;

    [Tooltip("Tiempo de un ciclo completo para el efecto visual de las olas.")]
    [SerializeField] private float waveCycleDuration = 2f;

    // --- EL EVENTO QUE EMITE LA SEÑAL ---
    // Cualquier otro script puede suscribirse a este evento para saber cuándo terminó
    [Header("Eventos de UI")]
    [SerializeField] private UnityEvent OnLoadComplete;

    // Referencias de UI Toolkit
    private VisualElement loadingBar;
    private VisualElement[] waves;
    
    // Variables de control de estado
    private float currentProgress = 0f;
    private float globalWavesOpacity = 1f;
    private float barOpacity = 1f;

    // Referencias a los Tweens para controlarlos
    private Tween progressTween;
    private Tween barFadeTween;
    private Tween wavesFadeTween;
    private Tween[] waveTweens; // Para guardar la animación de cada ola

    void OnEnable()
    {
        var uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;

        var root = uiDocument.rootVisualElement;

        loadingBar = root.Q<VisualElement>("LoadingBar");
        
        waves = new VisualElement[3];
        waves[0] = root.Q<VisualElement>("Wave1");
        waves[1] = root.Q<VisualElement>("Wave2");
        waves[2] = root.Q<VisualElement>("Wave3");

        // Valores iniciales
        loadingBar.style.width = Length.Percent(0);
        loadingBar.style.opacity = 1f;

        StartWavesAnimation();
    }

    private void StartWavesAnimation()
    {
        // Los porcentajes de desfase para cada ola (0%, 33%, 66%)
        float[] startOffsets = new float[] { 0f, 0.33f, 0.66f };
        waveTweens = new Tween[3];

        for (int i = 0; i < waves.Length; i++)
        {
            if (waves[i] == null) continue;

            int index = i; // Capturamos el índice para usarlo dentro de la función lambda de DOTween

            // Animamos un valor flotante de 0 a 1
            waveTweens[i] = DOTween.To(() => 0f, t =>
            {
                // Matemáticas de la ola
                float currentScale = Mathf.Lerp(1f, 0.66f, t);
                float individualOpacity = Mathf.Lerp(1f, 0f, t);

                // Multiplicamos por la opacidad global para poder ocultarlas desde otra función
                float finalOpacity = individualOpacity * globalWavesOpacity;

                // Aplicar a UI Toolkit
                waves[index].style.scale = new StyleScale(new Scale(new Vector3(currentScale, currentScale, 1)));
                waves[index].style.opacity = finalOpacity;

            }, 1f, waveCycleDuration)
            .SetEase(Ease.Linear) // Lineal para que el bucle sea perfecto
            .SetLoops(-1, LoopType.Restart); // Bucle infinito que se reinicia al llegar a 1

            // ADELANTAMOS LA ANIMACIÓN: Esto genera el efecto de estela desfasada
            waveTweens[i].Goto(waveCycleDuration * startOffsets[i], true);
        }
    }

    public void StartLoading()
    {
        // 1. Interrumpir transiciones previas
        progressTween?.Kill();
        barFadeTween?.Kill();
        wavesFadeTween?.Kill();

        // 2. Mostrar barra
        barOpacity = 1f;
        loadingBar.style.opacity = barOpacity;

        // 3. Calcular tiempo restante
        float timeRemaining = loadDuration * ((100f - currentProgress) / 100f);

        // 4. Animar ancho de la barra
        progressTween = DOTween.To(() => currentProgress, x =>
        {
            currentProgress = x;
            loadingBar.style.width = Length.Percent(x);
        }, 100f, timeRemaining)
        .SetEase(Ease.Linear)
        .OnComplete(() => 
        {
            // === LA SEÑAL ===
            // Cuando DOTween termina de llegar a 100, disparamos el evento (si hay alguien escuchando)
            OnLoadComplete?.Invoke();
        });

        // 5. Ocultar olas suavemente
        wavesFadeTween = DOTween.To(() => globalWavesOpacity, x => globalWavesOpacity = x, 0f, fadeDuration);
    }

    public void ResetLoading()
    {
        // 1. Detener el crecimiento y evitar que se dispare el OnComplete
        progressTween?.Kill();
        barFadeTween?.Kill();
        wavesFadeTween?.Kill();

        // 2. Desvanecer la barra
        barFadeTween = DOTween.To(() => barOpacity, x =>
        {
            barOpacity = x;
            loadingBar.style.opacity = x;
        }, 0f, fadeDuration).OnComplete(() =>
        {
            // 3. Reiniciar tamaño de forma invisible
            currentProgress = 0f;
            loadingBar.style.width = Length.Percent(0);
        });

        // 4. Mostrar olas de nuevo
        wavesFadeTween = DOTween.To(() => globalWavesOpacity, x => globalWavesOpacity = x, 1f, fadeDuration);
    }

    private void OnDisable()
    {
        // Limpieza de seguridad: Si se apaga el GameObject, matamos todos los tweens para liberar memoria
        progressTween?.Kill();
        barFadeTween?.Kill();
        wavesFadeTween?.Kill();
        
        if (waveTweens != null)
        {
            foreach (var tween in waveTweens)
            {
                tween?.Kill();
            }
        }
    }
}
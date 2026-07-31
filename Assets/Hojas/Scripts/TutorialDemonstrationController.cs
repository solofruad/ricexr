using Meta.XR.MRUtilityKit;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UIElements;

/// <summary>
/// Reproduce la demostración 3D previa al tutorial interactivo.
///
/// El contenido es puramente visual: el prop de hoja no debe incluir Leaf,
/// Grabbable ni componentes capaces de publicar eventos de gameplay.
/// Las señales de Timeline llaman a los métodos Signal... de este componente.
/// </summary>
[DisallowMultipleComponent]
public class TutorialDemonstrationController : MonoBehaviour
{
    [Header("Timeline y contenido")]
    [SerializeField] private PlayableDirector playableDirector;
    [SerializeField] private GameObject presentationRoot;
    [SerializeField] private Transform placementRoot;
    [SerializeField] private Animator avatarAnimator;
    [SerializeField] private TutorialDemonstrationSkipButton skipButton;

    [Header("Prop visual de hoja")]
    [SerializeField] private Transform demoLeaf;
    [SerializeField] private Transform leafRestSocket;
    [SerializeField] private Transform handSocket;
    [SerializeField] private Vector3 handLocalPosition;
    [SerializeField] private Vector3 handLocalEulerAngles;

    [Header("UI visual simulada")]
    [SerializeField] private UIDocument mockDiagnosisDocument;
    [SerializeField] private string panelElementName = "tutorial-demo-diagnosis";
    [SerializeField] private string diseaseElementName = "demo-disease-option";
    [SerializeField] private string severityElementName = "demo-severity-option";
    [SerializeField] private string confirmElementName = "demo-confirm-button";
    [SerializeField] private string successElementName = "demo-success";

    [Header("Colocación a escala real")]
    [Min(0.5f)]
    [SerializeField] private float distanceFromViewer = 2f;
    [Min(0.5f)]
    [SerializeField] private float fallbackEyeHeight = 1.65f;
    [SerializeField] private float lateralOffset;
    [SerializeField] private float verticalOffset;
    [SerializeField] private float yawOffset;

    private bool _isPlaying;
    private bool _completionSent;

    private Transform _leafOriginalParent;
    private Vector3 _leafOriginalLocalPosition;
    private Quaternion _leafOriginalLocalRotation;
    private Vector3 _leafOriginalLocalScale;

    public bool IsPlaying => _isPlaying;

    public bool CanPlay =>
        playableDirector != null &&
        playableDirector.playableAsset != null &&
        presentationRoot != null &&
        placementRoot != null &&
        avatarAnimator != null &&
        demoLeaf != null &&
        leafRestSocket != null &&
        handSocket != null &&
        mockDiagnosisDocument != null &&
        skipButton != null;

    private void Awake()
    {
        if (playableDirector == null)
            playableDirector = GetComponent<PlayableDirector>();

        CacheLeafRestPose();
        ResetState();
    }

    private void OnEnable()
    {
        if (playableDirector != null)
            playableDirector.stopped += HandleDirectorStopped;
    }

    private void OnDisable()
    {
        if (playableDirector != null)
            playableDirector.stopped -= HandleDirectorStopped;

        ResetState();
    }

    /// <summary>
    /// Coloca el set frente al usuario y reproduce la Timeline desde cero.
    /// Devuelve false si faltan referencias y el flujo debe continuar sin demo.
    /// </summary>
    public bool Play()
    {
        if (!CanPlay)
        {
            Debug.LogWarning(
                "[TutorialDemo] Configuración incompleta. Revise Timeline, avatar, hoja visual, " +
                "sockets, UI simulada, botón XR y raíces de presentación.");
            return false;
        }

        if (!ValidateDemoLeaf())
            return false;

        ResetState();

        if (!TryPlacePresentation())
        {
            Debug.LogWarning("[TutorialDemo] No hay una cámara válida para colocar la demostración.");
            return false;
        }

        _completionSent = false;
        _isPlaying = true;
        presentationRoot.SetActive(true);
        ResetMockDiagnosis();

        playableDirector.time = 0d;
        playableDirector.Evaluate();
        playableDirector.Play();
        return true;
    }

    /// <summary>Llamado por el InteractableUnityEventWrapper del botón world-space Omitir.</summary>
    public void Skip()
    {
        if (!_isPlaying) return;
        Complete(skipped: true, stopDirector: true);
    }

    /// <summary>Detiene y oculta la demo sin publicar una finalización.</summary>
    public void ResetState()
    {
        _isPlaying = false;
        _completionSent = true;

        if (playableDirector != null)
        {
            playableDirector.Stop();
            playableDirector.time = 0d;
        }

        ReturnLeafToRest();
        ResetMockDiagnosis();

        if (presentationRoot != null)
            presentationRoot.SetActive(false);
    }

    // Métodos públicos para SignalReceiver / UnityEvent en la Timeline.

    public void SignalGrabStep()
    {
        if (!_isPlaying) return;
        GameEventBus.PublishTutorialDemonstrationStepChanged(TutorialDemonstrationStep.Grab);
    }

    public void SignalInspectStep()
    {
        if (!_isPlaying) return;
        GameEventBus.PublishTutorialDemonstrationStepChanged(TutorialDemonstrationStep.Inspect);
    }

    public void SignalDiagnoseStep()
    {
        if (!_isPlaying) return;
        ShowDiagnosisPanel();
        GameEventBus.PublishTutorialDemonstrationStepChanged(TutorialDemonstrationStep.Diagnose);
    }

    public void AttachLeafToHand()
    {
        if (!_isPlaying || demoLeaf == null || handSocket == null) return;

        demoLeaf.SetParent(handSocket, worldPositionStays: false);
        demoLeaf.localPosition = handLocalPosition;
        demoLeaf.localRotation = Quaternion.Euler(handLocalEulerAngles);
    }

    public void ReturnLeafToRest()
    {
        if (demoLeaf == null) return;

        Transform targetParent = leafRestSocket != null ? leafRestSocket : _leafOriginalParent;
        demoLeaf.SetParent(targetParent, worldPositionStays: false);

        if (leafRestSocket != null)
        {
            demoLeaf.localPosition = Vector3.zero;
            demoLeaf.localRotation = Quaternion.identity;
        }
        else
        {
            demoLeaf.localPosition = _leafOriginalLocalPosition;
            demoLeaf.localRotation = _leafOriginalLocalRotation;
        }

        demoLeaf.localScale = _leafOriginalLocalScale;
    }

    public void ShowDiagnosisPanel()
    {
        VisualElement panel = GetElement(panelElementName);
        if (panel != null) panel.style.display = DisplayStyle.Flex;
    }

    public void HighlightDisease()
    {
        SetSelected(GetElement(diseaseElementName));
    }

    public void HighlightSeverity()
    {
        SetSelected(GetElement(severityElementName));
    }

    public void HighlightConfirm()
    {
        SetSelected(GetElement(confirmElementName));
    }

    public void ShowDiagnosisSuccess()
    {
        VisualElement success = GetElement(successElementName);
        if (success != null) success.style.display = DisplayStyle.Flex;
    }

    private void HandleDirectorStopped(PlayableDirector director)
    {
        if (!_isPlaying || _completionSent) return;
        Complete(skipped: false, stopDirector: false);
    }

    private void Complete(bool skipped, bool stopDirector)
    {
        if (_completionSent) return;

        _completionSent = true;
        _isPlaying = false;

        if (stopDirector && playableDirector != null)
            playableDirector.Stop();

        ReturnLeafToRest();
        ResetMockDiagnosis();

        if (presentationRoot != null)
            presentationRoot.SetActive(false);

        GameEventBus.PublishTutorialDemonstrationCompleted(skipped);
    }

    private bool TryPlacePresentation()
    {
        Camera camera = Camera.main;
        if (camera == null || placementRoot == null) return false;

        Transform cameraTransform = camera.transform;
        Vector3 flatForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.ProjectOnPlane(cameraTransform.up, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.forward;
        flatForward.Normalize();

        Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward).normalized;
        Vector3 desired = cameraTransform.position
                          + flatForward * distanceFromViewer
                          + flatRight * lateralOffset;

        MRUKRoom room = MRUK.Instance != null ? MRUK.Instance.GetCurrentRoom() : null;
        MRUKAnchor floorAnchor = room != null ? room.FloorAnchor : null;

        if (floorAnchor != null)
        {
            Vector3 floorNormal = floorAnchor.transform.forward;
            if (Vector3.Dot(floorNormal, Vector3.up) < 0f)
                floorNormal = -floorNormal;

            desired -= floorNormal *
                       Vector3.Dot(desired - floorAnchor.transform.position, floorNormal);
        }
        else
        {
            desired.y = cameraTransform.position.y - fallbackEyeHeight;
            Debug.LogWarning(
                $"[TutorialDemo] FloorAnchor no disponible. Se estima el suelo con " +
                $"fallbackEyeHeight={fallbackEyeHeight:F2} m.");
        }

        desired += Vector3.up * verticalOffset;
        placementRoot.position = desired;

        Vector3 toViewer = Vector3.ProjectOnPlane(
            cameraTransform.position - placementRoot.position,
            Vector3.up);
        if (toViewer.sqrMagnitude > 0.0001f)
        {
            placementRoot.rotation =
                Quaternion.LookRotation(toViewer.normalized, Vector3.up) *
                Quaternion.Euler(0f, yawOffset, 0f);
        }

        return true;
    }

    private void CacheLeafRestPose()
    {
        if (demoLeaf == null) return;

        _leafOriginalParent = demoLeaf.parent;
        _leafOriginalLocalPosition = demoLeaf.localPosition;
        _leafOriginalLocalRotation = demoLeaf.localRotation;
        _leafOriginalLocalScale = demoLeaf.localScale;
    }

    private bool ValidateDemoLeaf()
    {
        if (demoLeaf == null) return true;

        if (demoLeaf.GetComponentInChildren<Leaf>(includeInactive: true) != null)
        {
            Debug.LogError(
                "[TutorialDemo] demoLeaf contiene un componente Leaf. Use un prop visual sin gameplay.");
            return false;
        }

        if (demoLeaf.GetComponentsInChildren<Collider>(includeInactive: true).Length > 0)
        {
            Debug.LogWarning(
                "[TutorialDemo] demoLeaf contiene colliders. Se recomienda quitarlos del prop visual.");
        }

        return true;
    }

    private void ResetMockDiagnosis()
    {
        VisualElement panel = GetElement(panelElementName);
        if (panel != null) panel.style.display = DisplayStyle.None;

        SetUnselected(GetElement(diseaseElementName));
        SetUnselected(GetElement(severityElementName));
        SetUnselected(GetElement(confirmElementName));

        VisualElement success = GetElement(successElementName);
        if (success != null) success.style.display = DisplayStyle.None;
    }

    private VisualElement GetElement(string elementName)
    {
        if (mockDiagnosisDocument == null ||
            mockDiagnosisDocument.rootVisualElement == null ||
            string.IsNullOrWhiteSpace(elementName))
        {
            return null;
        }

        return mockDiagnosisDocument.rootVisualElement.Q<VisualElement>(elementName);
    }

    private static void SetSelected(VisualElement element)
    {
        if (element == null) return;
        element.style.borderTopColor = new Color(0.47f, 1f, 0.44f, 1f);
        element.style.borderRightColor = new Color(0.47f, 1f, 0.44f, 1f);
        element.style.borderBottomColor = new Color(0.47f, 1f, 0.44f, 1f);
        element.style.borderLeftColor = new Color(0.47f, 1f, 0.44f, 1f);
        element.style.backgroundColor = new Color(0.08f, 0.24f, 0.16f, 1f);
    }

    private static void SetUnselected(VisualElement element)
    {
        if (element == null) return;
        Color border = new Color(1f, 1f, 1f, 0.14f);
        element.style.borderTopColor = border;
        element.style.borderRightColor = border;
        element.style.borderBottomColor = border;
        element.style.borderLeftColor = border;
        element.style.backgroundColor = new Color(1f, 1f, 1f, 0.04f);
    }
}

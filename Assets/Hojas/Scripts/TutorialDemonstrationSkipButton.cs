using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;

/// <summary>
/// Configura una superficie de omision compatible con Poke y Ray de Meta Interaction SDK.
/// El elemento visual puede vivir en UI Toolkit; esta superficie recibe la interaccion XR.
/// </summary>
[DisallowMultipleComponent]
public class TutorialDemonstrationSkipButton : MonoBehaviour
{
    [SerializeField] private TutorialDemonstrationController demonstrationController;
    [SerializeField] private Vector2 interactionSize = new Vector2(0.46f, 0.14f);

    private InteractableUnityEventWrapper _pokeEvents;
    private InteractableUnityEventWrapper _rayEvents;

    private void Awake()
    {
        ConfigureInteraction();
    }

    private void OnDestroy()
    {
        if (_pokeEvents != null)
            _pokeEvents.WhenSelect.RemoveListener(HandleSelected);

        if (_rayEvents != null)
            _rayEvents.WhenSelect.RemoveListener(HandleSelected);
    }

    private void ConfigureInteraction()
    {
        PlaneSurface planeSurface = GetOrAdd<PlaneSurface>();
        planeSurface.InjectAllPlaneSurface(
            PlaneSurface.NormalFacing.Backward,
            doubleSided: true);

        BoundsClipper boundsClipper = GetOrAdd<BoundsClipper>();
        boundsClipper.Position = Vector3.zero;
        boundsClipper.Size = new Vector3(
            Mathf.Max(0.05f, interactionSize.x),
            Mathf.Max(0.05f, interactionSize.y),
            0.02f);

        ClippedPlaneSurface clippedSurface = GetOrAdd<ClippedPlaneSurface>();
        clippedSurface.InjectAllClippedPlaneSurface(
            planeSurface,
            new IBoundsClipper[] { boundsClipper });

        PokeInteractable pokeInteractable = GetOrAdd<PokeInteractable>();
        pokeInteractable.InjectAllPokeInteractable(clippedSurface);

        RayInteractable rayInteractable = GetOrAdd<RayInteractable>();
        rayInteractable.InjectAllRayInteractable(clippedSurface);

        _pokeEvents = gameObject.AddComponent<InteractableUnityEventWrapper>();
        _pokeEvents.InjectAllInteractableUnityEventWrapper(pokeInteractable);
        _pokeEvents.WhenSelect.AddListener(HandleSelected);

        _rayEvents = gameObject.AddComponent<InteractableUnityEventWrapper>();
        _rayEvents.InjectAllInteractableUnityEventWrapper(rayInteractable);
        _rayEvents.WhenSelect.AddListener(HandleSelected);
    }

    private void HandleSelected()
    {
        demonstrationController?.Skip();
    }

    private T GetOrAdd<T>() where T : Component
    {
        T component = GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }
}

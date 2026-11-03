using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;

/// <summary>
/// Filtro de interactores para un <see cref="PokeInteractable"/> que solo acepta
/// pokes que llegan por la cara frontal de la superficie.
///
/// El PokeInteractor del SDK puede seleccionar cuando el dedo está detrás del
/// plano, a menos de EnterHoverNormal, y se aleja del jugador. Con el pinning
/// activo eso deja la mano "pegada" al panel al atravesarlo desde atrás.
///
/// Cada dedo queda armado al estar delante de la superficie y se desarma al
/// pasar detrás sin estar ya interactuando con ella. Solo un dedo armado puede
/// entrar en hover o seleccionar, así que un poke normal (que se hunde a través
/// de la superficie) sigue funcionando, y uno que viene desde atrás la atraviesa.
///
/// Se asigna en la lista Interactor Filters del PokeInteractable.
/// </summary>
[DisallowMultipleComponent]
public sealed class PokeFrontSideFilter : MonoBehaviour, IGameObjectFilter
{
    [Tooltip("PokeInteractable cuya superficie define la cara frontal. Si se deja vacío, se busca en este GameObject.")]
    [SerializeField] private PokeInteractable interactable;

    [Tooltip("Margen en metros detrás de la superficie antes de desarmar el dedo. Absorbe el temblor del hand tracking.")]
    [Min(0f)] [SerializeField] private float backTolerance = 0.005f;

    private readonly Dictionary<PokeInteractor, bool> _armed = new Dictionary<PokeInteractor, bool>();

    private void Awake()
    {
        if (interactable == null)
            interactable = GetComponent<PokeInteractable>();
    }

    private void OnDisable() => _armed.Clear();

    /// <summary>
    /// Devuelve true si el interactor puede interactuar con la superficie:
    /// ya está interactuando con ella o su dedo entró por la cara frontal.
    /// </summary>
    public bool Filter(GameObject gameObject)
    {
        if (interactable == null || interactable.SurfacePatch == null) return true;

        PokeInteractor poke = gameObject.GetComponent<PokeInteractor>();
        if (poke == null) return true;

        bool engaged = poke.Interactable == interactable;

        if (!interactable.ClosestBackingSurfaceHit(poke.Origin, out SurfaceHit hit))
            return engaged;

        float signedDistance = Vector3.Dot(poke.Origin - hit.Point, hit.Normal);

        _armed.TryGetValue(poke, out bool armed);
        if (signedDistance > 0f)
            armed = true;
        else if (signedDistance < -backTolerance && !engaged)
            armed = false;
        _armed[poke] = armed;

        return armed || engaged;
    }
}

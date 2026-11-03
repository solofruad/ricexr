using RiceXR.Core;
using UnityEngine;

/// <summary>
/// Adaptador de Unity para <see cref="GuidancePlacement"/>: convierte la cabeza del jugador y la superficie
/// seleccionada de <see cref="SceneInteractionManager"/> en los tipos de la lógica pura, y devuelve la
/// posición de un panel de guía fuera del volumen de juego.
/// </summary>
public static class GuidancePanelPlacer
{
    /// <summary>
    /// Posición del panel delante de la cabeza evitando el volumen de juego. Devuelve false, sin tocar
    /// <paramref name="position"/>, si no hay cámara o plano seleccionado.
    /// </summary>
    /// <param name="distance">Distancia horizontal deseada a la cabeza.</param>
    /// <param name="heightOffset">Desplazamiento vertical respecto a la cabeza.</param>
    /// <param name="leafHeight">Altura de las hojas sobre la superficie.</param>
    /// <param name="margin">Holgura alrededor del volumen de juego.</param>
    /// <param name="position">Posición resultante en el mundo.</param>
    public static bool TryResolve(float distance, float heightOffset, float leafHeight, float margin, out Vector3 position)
    {
        position = default;

        Camera cam = Camera.main;
        SceneInteractionManager manager = SceneInteractionManager.Instance;
        if (cam == null || manager == null || !manager.HasSelectedPlane) return false;

        Vector3 surface = manager.SelectedSurfacePosition;
        Vector2 size = manager.SelectedSurfaceSize;

        var volume = new GuidanceVolume
        {
            SurfaceCenter = new GuidancePoint(surface.x, surface.y, surface.z),
            YawDegrees = manager.SelectedPlaneRotation.eulerAngles.y,
            SizeX = size.x,
            SizeZ = size.y,
            LeafHeight = leafHeight
        };

        var options = new GuidancePlacementOptions
        {
            Distance = distance,
            HeightOffset = heightOffset,
            Margin = margin,
            MinDistance = Mathf.Min(0.6f, distance),
            MaxDistance = Mathf.Max(2.5f, distance * 2f)
        };

        Vector3 head = cam.transform.position;
        Vector3 forward = cam.transform.forward;

        GuidancePoint result = GuidancePlacement.Resolve(
            new GuidancePoint(head.x, head.y, head.z),
            new GuidancePoint(forward.x, forward.y, forward.z),
            volume,
            options);

        position = new Vector3(result.X, result.Y, result.Z);
        return true;
    }
}

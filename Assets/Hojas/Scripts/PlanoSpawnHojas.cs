using System;
using System.Collections.Generic;
using Meta.XR.Util;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Random = System.Random;

namespace Meta.XR.MRUtilityKit
{
    public class PlanoSpawnHojas : AnchorPrefabSpawner
    {
        public float SeparacionVertical = 0.1f;
        /// <summary>
        /// Custom logic for scaling a prefab's volume. Scales the prefab to match the volume's width and depth,
        /// while maintaining the prefab's original height.
        /// </summary>
        /// <param name="localScale">The local scale vector.</param>
        /// <returns>The adjusted scale vector with X and Z scaled to match volume, Y unchanged.</returns>
        public override Vector3 CustomPrefabScaling(Vector3 localScale)
        {
            // Mantener la escala Y (altura) original del plano
            // Las escalas X y Z ya vienen calculadas para coincidir con el volumen
            // Solo necesitamos retornar el localScale con Y = 1 para mantener la altura original del plano
            return new Vector3(localScale.x, 1f, localScale.z);
        }

        /// <summary>
        /// Custom logic for aligning a prefab within a volume. Positions the prefab on top of the volume,
        /// aligned with its upper surface.
        /// </summary>
        /// <param name="anchorVolumeBounds">The bounds of the anchor volume.</param>
        /// <param name="prefabBounds">The optional bounds of the prefab.</param>
        /// <returns>The position vector placing the prefab on top of the volume.</returns>
        public override Vector3 CustomPrefabAlignment(Bounds anchorVolumeBounds, Bounds? prefabBounds)
        {
            // Posición base: centro del volumen en X y Y
            return new Vector3(
                anchorVolumeBounds.center.x,
                anchorVolumeBounds.center.y,
                SeparacionVertical
            );
        }
    }
}
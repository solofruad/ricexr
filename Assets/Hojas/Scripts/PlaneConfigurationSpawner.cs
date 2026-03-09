using System;
using System.Collections.Generic;
using Meta.XR.Util;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Random = System.Random;

namespace Meta.XR.MRUtilityKit
{
    /// <summary>
    /// Sistema especializado para spawnear planos azules con olas sobre superficies detectadas por el MRUtilityKit.
    /// 
    /// Funcionalidades principales:
    /// 1. Hereda de AnchorPrefabSpawner para integración con el sistema de MRUtilityKit
    /// 2. Implementa lógica personalizada para escalado y alineación de prefabs de planos
    /// 3. Mantiene la altura original del plano mientras ajusta ancho y profundidad al volumen del anchor
    /// 4. Posiciona los planos con una separación vertical configurable para evitar clipping
    /// 
    /// Uso típico:
    /// - Detecta superficies planas en el entorno de realidad mixta
    /// - Spawnea planos sobre la CARA SUPERIOR de estas superficies
    /// - Ajusta automáticamente el tamaño del plano al tamaño de la superficie detectada
    /// - Mantiene una separación vertical para evitar problemas de z-fighting
    /// 
    /// Configuración:
    /// - El prefab debe ser un plano orientado horizontalmente
    /// - SeparaciónVertical controla la distancia entre el plano y la superficie real
    /// - Las escalas X y Z se ajustan automáticamente al volumen del anchor
    /// </summary>
    public class PlaneConfigurationSpawner : AnchorPrefabSpawner
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

        /// <summary>
        /// Expone el spawn de prefabs para poder llamarlo desde SceneInteractionManager
        /// cuando el usuario presiona "Iniciar" en el menú principal.
        /// </summary>
        /// <param name="room">La habitación para la cual se spawnearán los prefabs.</param>
        public void SpawnForRoom(MRUKRoom room)
        {
            SpawnPrefabs(room);
        }
    }
}
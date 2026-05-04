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
    /// 1. Hereda de AnchorPrefabSpawner para integracion con el sistema de MRUtilityKit
    /// 2. Implementa logica personalizada para escalado y alineacion de prefabs de planos
    /// 3. Mantiene la altura original del plano mientras ajusta ancho y profundidad al volumen del anchor
    /// 4. Posiciona los planos con una separacion vertical configurable para evitar clipping
    /// 
    /// Uso tpico:
    /// - Detecta superficies planas en el entorno de realidad mixta
    /// - Spawnea planos sobre la CARA SUPERIOR de estas superficies
    /// - Ajusta automaticamente el tamao del plano al tamao de la superficie detectada
    /// - Mantiene una separacion vertical para evitar problemas de z-fighting
    /// 
    /// Configuracion:
    /// - El prefab debe ser un plano orientado horizontalmente
    /// - SeparacionVertical controla la distancia entre el plano y la superficie real
    /// - Las escalas X y Z se ajustan automaticamente al volumen del anchor
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
        /// Logica personalizada para alinear el prefab sobre el volumen del anchor. Posiciona el prefab centrado en X y Y,
        /// y con una separacion vertical configurable en Z para evitar clipping con la superficie real.
        /// </summary>
        /// <param name="anchorVolumeBounds">El volumen del anchor.</param>
        /// <param name="prefabBounds">Las limites del prefab.</param>
        /// <returns>La posicion del prefab.</returns>
        public override Vector3 CustomPrefabAlignment(Bounds anchorVolumeBounds, Bounds? prefabBounds)
        {
            // Posicion base: centro del volumen en X y Y
            return new Vector3(
                anchorVolumeBounds.center.x,
                anchorVolumeBounds.center.y,
                SeparacionVertical
            );
        }

        /// <summary>
        /// Expone el spawn de prefabs para poder llamarlo desde SceneInteractionManager
        /// cuando el usuario presiona "Iniciar" en el menu principal.
        /// </summary>
        /// <param name="room">La habitacion para la cual se spawnearon los prefabs.</param>
        public void SpawnForRoom(MRUKRoom room)
        {
            SpawnPrefabs(room);
        }
    }
}
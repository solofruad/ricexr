using System.Collections;
using UnityEngine;

/// <summary>
/// Hace que el UI siempre mire a la c�mara.
/// </summary>
public class BillboardUI : MonoBehaviour
{
    private Transform cameraTransform;

    [Tooltip("Velocidad de suavizado al girar.")]
    [SerializeField] private float smoothSpeed = 5f;

    [Tooltip("Pivota solo sobre el eje Y global (ignora rotaci�n arriba/abajo).")]
    [SerializeField] private bool lockYAxis = true;

    [Tooltip("Alinea la UI con el giro de la cabeza para que el texto no quede de cabeza al mirar hacia abajo.")]
    [SerializeField] private bool matchCameraUpVector = true;

    void Start()
    {
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    void LateUpdate()
    {
        if (cameraTransform == null) return;

        // Dirección desde el UI hacia la cámara
        Vector3 targetDirection = cameraTransform.position - transform.position;

        if (lockYAxis)
        {
            targetDirection.y = 0;
            // Si bloqueamos el eje Y, el 'up' siempre debe ser el Y del mundo
            if (targetDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(-targetDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothSpeed * Time.deltaTime);
            }
        }
        else
        {
            // Movimiento libre
            if (targetDirection.sqrMagnitude > 0.001f)
            {
                Vector3 upVector = Vector3.up;

                if (matchCameraUpVector)
                {
                    upVector = cameraTransform.up;

                    // IMPORTANTE: Prevenir crasheos de Unity.
                    // Si targetDirection y upVector son paralelos, LookRotation lanza excepción o falla.
                    // Verificamos el producto cruzado. Si es casi cero, son paralelos.
                    if (Vector3.Cross(targetDirection.normalized, upVector).sqrMagnitude < 0.001f)
                    {
                        // Son colineales (mirando totalmente hacia abajo o arriba).
                        // Fallback más seguro: usar el 'up' que estaba usando antes o el 'forward' de la cámara.
                        upVector = -cameraTransform.forward; 
                    }
                }

                Quaternion targetRotation = Quaternion.LookRotation(-targetDirection, upVector);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothSpeed * Time.deltaTime);
            }
        }
    }
}

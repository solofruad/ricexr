using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple clase que hace que el UI siempre mire a la cámara.
/// 
/// Es utilizada tanto en el mensaje mostrado por los planos azules con olas cuando se quiere elegir una superfie 
/// para desplegar el cultivo, como en los tooltips de las hojas.
/// 
/// </summary>

public class BillboardUI : MonoBehaviour
{
    private Transform cameraTransform;
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private bool lockYAxis = true; 

    void Start()
    {
        cameraTransform = Camera.main.transform;
    }

    void LateUpdate()
    {
        Vector3 targetDirection = cameraTransform.position - transform.position;

        if (lockYAxis)
        {
            targetDirection.y = 0; 
        }

        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(-targetDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                smoothSpeed * Time.deltaTime
            );
        }
    }
}

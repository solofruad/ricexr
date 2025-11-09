using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

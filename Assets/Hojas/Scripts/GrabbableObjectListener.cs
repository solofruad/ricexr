using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrabbableObjectListener : MonoBehaviour
{
    public static GrabbableObjectListener Instance { get; private set; }
    [HideInInspector]
    public Leaf ActualLeafGrabbed { get; set; } = null;
    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

}

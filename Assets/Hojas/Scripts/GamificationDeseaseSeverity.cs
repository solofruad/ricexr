using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GamificationDeseaseSeverity : MonoBehaviour
{
    public static GamificationDeseaseSeverity Instance { get; private set; }
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
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

}

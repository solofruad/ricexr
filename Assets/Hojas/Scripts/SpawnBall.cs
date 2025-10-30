using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnBall : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject prefab;
    public float ballSpeed = 5;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
        {
            GameObject spawnedBall = Instantiate(prefab, transform.position, Quaternion.identity);
            Rigidbody spawnedBallRB = spawnedBall.GetComponent<Rigidbody>();
            spawnedBallRB.velocity = transform.forward * ballSpeed;

        }
    }
}

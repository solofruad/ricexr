using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class SeleccionarSuperficieSpawn : MonoBehaviour
{
    public Transform raystartPoint;
    public float rayLenght = 5;
    public MRUKAnchor.SceneLabels labelFilter; 

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Ray ray = new Ray(raystartPoint.position, raystartPoint.forward);

        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        bool hasHit = room.Raycast(ray, rayLenght, new LabelFilter(labelFilter) , out RaycastHit hit, out MRUKAnchor anchor);


        if (hasHit) {

            Vector3 hitPoint = hit.point;
            Vector3 normal = hit.normal;

            string label =  anchor.Label.ToString();
            ;

        }
    }
}

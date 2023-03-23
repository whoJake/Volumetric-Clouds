using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class Volume : MonoBehaviour
{
    public Shader shader;

    public bool update;

    void Start()
    {
        //Get minmax bounds
        Vector3 boxmin = transform.position - (transform.lossyScale / 2);
        Vector3 boxmax = transform.position + (transform.lossyScale / 2);

        Material material = new Material(shader);

        material.SetVector("boxmin", boxmin);
        material.SetVector("boxmax", boxmax);

        GetComponent<MeshRenderer>().material = material;
    }

    void OnValidate() {
        if(update) {
            update = false;
            Start();
        }
    }

}

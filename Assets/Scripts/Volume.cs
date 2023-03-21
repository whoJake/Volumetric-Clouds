using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class Volume : MonoBehaviour
{
    public Shader shader;

    void Start()
    {
        Material material = new Material(shader);
        GetComponent<MeshRenderer>().material = material;
    }
}

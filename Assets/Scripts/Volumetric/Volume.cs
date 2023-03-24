using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class Volume : MonoBehaviour
{
    public Shader shader;
    Material material;

    public bool setup;
    public bool update;
    bool invert;

    void Start()
    {
        Camera.main.depthTextureMode = DepthTextureMode.Depth;
        GetComponent<MeshFilter>().sharedMesh = DefaultCube();

        material = new Material(shader);

        GetComponent<MeshRenderer>().material = material;
    }

    void SetMaterialProperties() {

        //Get minmax bounds
        Vector3 boxmin = transform.position - (transform.localScale / 2f);
        Vector3 boxmax = transform.position + (transform.localScale / 2f);

        material.SetVector("boxmin", boxmin);
        material.SetVector("boxmax", boxmax);
    }

    void InvertMesh() {
        MeshFilter filter = GetComponent<MeshFilter>();
        filter.sharedMesh.triangles = filter.sharedMesh.triangles.Reverse().ToArray();
    }

    Mesh DefaultCube() {
        Mesh mesh = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        for(float x = -0.5f; x <= 0.5f; x++) {
            for(float y = -0.5f; y <= 0.5f; y++) {
                for(float z = -0.5f; z <= 0.5f; z++) {
                    verts.Add(new Vector3(x, y, z));
                }
            }
        }

        int[] tris = new int[]{
        2,0,1, 1,3,2,
        6,7,5, 5,4,6,
        4,5,1, 1,0,4,
        6,2,3, 3,7,6,
        2,6,4, 4,0,2,
        5,7,3, 3,1,5
        };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    void OnValidate() {
        if (setup) {
            setup = false;
            Start();
        }
        if(update) {
            update = false;
            SetMaterialProperties();
        }
        if (invert) {
            invert = false;
            InvertMesh();
        }
    }

}

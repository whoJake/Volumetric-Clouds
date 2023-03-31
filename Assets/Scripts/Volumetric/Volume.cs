using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class Volume : MonoBehaviour
{
    public Shader shader;
    public ComputeShader copyShader;
    public RenderTexture cloudTexture;
    Material material;

    public Vector3 cloudScale = Vector3.one;
    public Vector3 cloudOffset;
    [Range(0f, 1f)]
    public float cloudCoverage;
    public int cloudResolution;
    public int seed;
    public int octaves;
    public float frequency;
    public float persistance;
    public float lacunarity;

    public int steps;

    [Header("Lighting")]
    public int lightSteps;
    public Color lightColor;
    [Range(0f, 1f)]
    public float lightStrength;
    [Range(0f, 1f)]
    public float maxShadowValue;

    public bool setup;
    public bool update;
    public bool auto;
    bool invert;

    void Start()
    {
        Camera.main.depthTextureMode = DepthTextureMode.Depth;
        GetComponent<MeshFilter>().sharedMesh = DefaultCube();

        float[,,] cloudNoise = WorleyGen.Generate3DFractal(cloudResolution, cloudResolution, cloudResolution, seed, octaves, frequency, persistance, lacunarity);
        cloudTexture = TextureHelper.FloatArrayToTexture3D(copyShader, cloudNoise, cloudResolution);

        material = new Material(shader);

        GetComponent<MeshRenderer>().material = material;
        SetMaterialProperties();
    }

    void SetMaterialProperties() {

        //Get minmax bounds
        Vector3 boxmin = transform.position - (transform.localScale / 2f);
        Vector3 boxmax = transform.position + (transform.localScale / 2f);

        material.SetVector("cloud_scale", cloudScale);
        material.SetVector("cloud_offset", cloudOffset);
        material.SetFloat("cloud_coverage_threshold", cloudCoverage);
        material.SetVector("boxmin", boxmin);
        material.SetVector("boxmax", boxmax);
        material.SetInt("viewSteps", steps);
        material.SetInt("lightSteps", lightSteps);
        material.SetColor("lightColor", lightColor);
        material.SetFloat("lightStrength", lightStrength);
        material.SetFloat("maxShadowValue", maxShadowValue);
        material.SetTexture("cloudTexture", cloudTexture);
        material.SetFloat("worldTexSize", Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z));
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
        if (auto) {
            SetMaterialProperties();
        }
    }

}

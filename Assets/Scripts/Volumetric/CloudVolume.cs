using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class CloudVolume : MonoBehaviour
{
    public Shader shader;
    RenderTexture cloudTexture;
    Material material;

    [Header("Generation")]
    public Vector3 cloudScale = Vector3.one;
    public Vector3 cloudOffset;
    [Range(0f, 1f)]
    public float cloudCoverage;
    public int cloudResolution;
    public int seed;
    public int octaves;
    public int frequency;
    [Range(0f, 1f)]
    public float persistance;

    [Header("Rendering")]
    public int steps;
    public float stepIncrement;

    [Header("Noise")]
    public Texture2D blueNoise;
    [Range(0f, 1f)]
    public float noiseStrength;

    [Header("Lighting")]
    public int lightSteps;
    public Color shadowColor;
    [Range(0f, 2f)]
    public float lightStrength;
    [Range(0f, 1f)]
    public float shadowCutoffThreshold;
    [Range(0, 1f)]
    public float inScatterWeight;
    [Range(0, 1f)]
    public float outScatterWeight;
    [Range(0, 1f)]
    public float scatterBlend;

    [Header("Toggles")]
    public bool setup;
    public bool update;
    public bool auto;

    void Start()
    {
        Camera.main.depthTextureMode = DepthTextureMode.Depth;
        GetComponent<MeshFilter>().sharedMesh = DefaultCube();
        
        //Generate cloud texture
        cloudTexture = PerlinGen.Generate3DFractalGPU(cloudResolution, seed, octaves, frequency, persistance);

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
        material.SetInt("view_steps", steps);
        material.SetInt("light_steps", lightSteps);
        material.SetFloat("step_inc", stepIncrement);
        material.SetColor("_ShadowColor", shadowColor);
        material.SetFloat("light_strength", lightStrength);
        material.SetFloat("shadow_cutoff", shadowCutoffThreshold);
        material.SetFloat("in_scatter_g", inScatterWeight);
        material.SetFloat("out_scatter_g", outScatterWeight);
        material.SetFloat("scatter_blend", scatterBlend);
        material.SetTexture("_CloudTexture", cloudTexture);
        material.SetTexture("_BlueNoise", blueNoise);
        material.SetFloat("noise_strength", noiseStrength);
        material.SetInt("noise_size", Mathf.Max(blueNoise.width, blueNoise.height));
        material.SetFloat("world_tex_size", Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z));
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
        if (auto) {
            SetMaterialProperties();
        }
    }

}

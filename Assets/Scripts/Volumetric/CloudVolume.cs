using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class CloudVolume : MonoBehaviour
{
    public Shader shader;
    Material material;

    [Space]
    
    [Header("Cloud Texture Creation")]
    public int detailResolution;
    public int seed;
    [Header("Perlin-Worley R Channel")]
    [Header("Perlin")]
    public int r_frequency_perlin;
    public int r_octaves_perlin;
    public float r_persistance_perlin;
    [Header("Worley")]
    public int r_frequency_worley;
    public int r_octaves_worley;
    public float r_persistance_worley;
    [Header("Worley G Channel")]
    public int g_frequency;
    public int g_octaves;
    public float g_persistance;
    [Header("Worley B Channel")]
    public int b_frequency;
    public int b_octaves;
    public float b_persistance;
    [Header("Worley A Channel")]
    public int a_frequency;
    public int a_octaves;
    public float a_persistance;

    private CloudDetailGenerator.DetailSettings detailSettings;
    private RenderTexture cloudDetailTexture;

    [Space]
    [Space]

    [Header("Cloud Textures")]
    public Texture2D cloudInfo;

    [Header("Rendering")]
    public Vector3 cloudScale = Vector3.one;
    public Vector3 cloudOffset;

    [Header("Movement")]
    public Vector3 windSpeed;
    public Vector3 disturbance;

    [Header("Detail")]
    public Vector3 cloudDetailScale = Vector3.one;
    public Vector3 cloudDetailOffset = Vector3.one;
    [Range(0f, 1f)]
    public float noiseToDrawnBlend;
    [Min(0f)]
    public float densityModifier;
    [Range(-1f, 1f)]
    public float coverageModifier;
    [Range(0f, 1f)]
    public float shapeModifier;

    public int steps;
    public float stepIncrement;

    [Header("Noise")]
    public Texture2D blueNoise;
    [Range(0f, 1f)]
    public float noiseStrength;

    [Header("Lighting")]
    public int lightSteps;
    public Color shadowColor;
    [Range(0f, 3f)]
    public float lightStrength;
    [Range(0f, 1f)]
    public float shadowCutoffThreshold;
    [Range(0, 1f)]
    public float inScatterWeight;
    [Range(0, 1f)]
    public float outScatterWeight;
    [Range(0, 1f)]
    public float scatterBlend;
    [Range(0f, 1f)]
    public float lightBanding;

    [Header("Toggles")]
    public bool setup;
    public bool update;
    public bool auto;

    void Start()
    {
        Camera.main.depthTextureMode = DepthTextureMode.Depth;
        GetComponent<MeshFilter>().sharedMesh = DefaultCube();

        //Generate cloud detail texture
        InitializeDetailSettings();
        cloudDetailTexture = CloudDetailGenerator.CreateDetailTexture(detailSettings);

        material = new Material(shader);

        GetComponent<MeshRenderer>().sharedMaterial = material;
        SetMaterialProperties();
    }

    void SetMaterialProperties() {

        //Get minmax bounds
        Vector3 boxmin = transform.position - (transform.localScale / 2f);
        Vector3 boxmax = transform.position + (transform.localScale / 2f);

        material.SetVector("cloud_scale", cloudScale);
        material.SetVector("cloud_detail_scale", cloudDetailScale);
        material.SetVector("cloud_offset", cloudOffset);
        material.SetVector("cloud_detail_offset", cloudDetailOffset);
        material.SetVector("wind_speed", windSpeed);
        material.SetVector("disturbance_speed", disturbance);
        material.SetFloat("noise_to_drawn_blend", noiseToDrawnBlend);
        material.SetFloat("density_modifier", densityModifier);
        material.SetFloat("coverage_modifier", coverageModifier);
        material.SetFloat("shape_modifier", 1 - shapeModifier);
        material.SetVector("boxmin", boxmin);
        material.SetVector("boxmax", boxmax);
        material.SetInt("view_steps", steps);
        material.SetInt("light_steps", lightSteps);
        material.SetFloat("step_inc", stepIncrement);
        material.SetColor("_ShadowColor", shadowColor);
        material.SetFloat("light_strength", 1/lightStrength);
        material.SetFloat("shadow_cutoff", shadowCutoffThreshold);
        material.SetFloat("in_scatter_g", inScatterWeight);
        material.SetFloat("out_scatter_g", outScatterWeight);
        material.SetFloat("scatter_blend", scatterBlend);
        material.SetFloat("light_banding", lightBanding);
        material.SetTexture("_CloudTexture", cloudDetailTexture);
        material.SetTexture("_BlueNoise", blueNoise);
        material.SetFloat("noise_strength", noiseStrength);
        material.SetInt("noise_size", Mathf.Max(blueNoise.width, blueNoise.height));
        material.SetFloat("world_tex_size", Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z));

        material.SetTexture("_CloudInfoTexture", cloudInfo);
    }

    void InitializeDetailSettings() {
        detailSettings = new CloudDetailGenerator.DetailSettings {
            detailResolution = this.detailResolution,
            seed = this.seed,
            r_frequency_perlin = this.r_frequency_perlin,
            r_octaves_perlin = this.r_octaves_perlin,
            r_persistance_perlin = this.r_persistance_perlin,
            r_frequency_worley = this.r_frequency_worley,
            r_octaves_worley = this.r_octaves_worley,
            r_persistance_worley = this.r_persistance_worley,
            g_frequency = this.g_frequency,
            g_octaves = this.g_octaves,
            g_persistance = this.g_persistance,
            b_frequency = this.b_frequency,
            b_octaves = this.b_octaves,
            b_persistance = this.b_persistance,
            a_frequency = this.a_frequency,
            a_octaves = this.a_octaves,
            a_persistance = this.a_persistance
        };
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

    Mesh DefaultCube() {
        Mesh mesh = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        for (float x = -0.5f; x <= 0.5f; x++) {
            for (float y = -0.5f; y <= 0.5f; y++) {
                for (float z = -0.5f; z <= 0.5f; z++) {
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

}

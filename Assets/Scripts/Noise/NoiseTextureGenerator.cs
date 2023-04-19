using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class NoiseTextureGenerator : MonoBehaviour { 

    public RenderTexture target;

    public int detailResolution;
    public int seed;
    //Perlin-Worley Perlin R
    public int r_frequency_perlin;
    public int r_octaves_perlin;
    public float r_persistance_perlin;
    //Perlin-Worley Worley R
    public int r_frequency_worley;
    public int r_octaves_worley;
    public float r_persistance_worley;

    public float multiply;

    public bool generate = false;
    public bool save = false;

    private ComputeShader additiveCompute;

    private void Awake() {
        additiveCompute = Resources.Load<ComputeShader>("AdditiveCombine");
    }

    private void Start() {
        RebuildTextures(detailResolution);
        Graphics.CopyTexture(GeneratePerlinWorley(), target);
    }

    void RebuildTextures(int textureSize) {
        if (target.width != textureSize || target.height != textureSize) {
            target.Release();
            target.width = textureSize;
            target.height = textureSize;
            target.format = RenderTextureFormat.RFloat;
            target.depth = 0;
            target.Create();
        }
    }

    RenderTexture GeneratePerlinWorley() {
        Awake();
        RenderTexture perlin = PerlinGen.Generate2DFractalGPU(detailResolution, seed, r_octaves_perlin, r_frequency_perlin, r_persistance_perlin);
        RenderTexture worley = WorleyGen.Generate2DFractalGPU(detailResolution, seed + 1, r_octaves_worley, r_frequency_worley, r_persistance_worley);

        RenderTexture perlinWorley = AdditiveCombine2D(perlin, worley, 0.5f);
        perlinWorley = CloudDetailGenerator.AdjustRange2D(perlinWorley);
        return perlinWorley;
    }

    void SaveTexture(RenderTexture image) {
        //https://answers.unity.com/questions/37134/is-it-possible-to-save-rendertextures-into-png-fil.html
        RenderTexture.active = image;
        Texture2D virtualTexture = new Texture2D(detailResolution, detailResolution, TextureFormat.RFloat, false);
        virtualTexture.ReadPixels(new Rect(0, 0, detailResolution, detailResolution), 0, 0);
        RenderTexture.active = null;

        byte[] pngBytes;
        pngBytes = virtualTexture.EncodeToPNG();
        string path = Application.persistentDataPath + "/" + seed + "perlinWorley" + detailResolution + "x" + detailResolution + ".png";
        print("Saved to " + path);
        System.IO.File.WriteAllBytes(path, pngBytes);
    }

    public RenderTexture AdditiveCombine2D(RenderTexture source, RenderTexture destination, float blend) {

        additiveCompute.SetTexture(1, "_Source2D", source);
        additiveCompute.SetTexture(1, "_Destination2D", destination);
        additiveCompute.SetFloat("blend", blend);
        additiveCompute.SetFloat("multiply", multiply);

        int threads = Mathf.CeilToInt(source.width / 8f);

        additiveCompute.Dispatch(1, threads, threads, 1);

        return destination;
    }


    private void OnValidate() {
        if (generate) {
            generate = false;
            Start();
        }
        if (save) {
            save = false;
            SaveTexture(target);
        }
    }
}

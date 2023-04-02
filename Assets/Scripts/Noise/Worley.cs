using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class Worley : Generator {

    public int textureSize;

    public bool invert;
    public int seed;
    [Min(1)]
    public int frequency;
    [Min(0)]
    public int octaves;
    [Range(0f, 1f)]
    public float persistance;
    [Range(1f, 3f)]
    public float lacunarity;

    public bool generate = false;
    public bool refresh;

    private void Start() {
        RebuildTextures(textureSize);

        RenderTexture noise = WorleyGen.Generate2DGPU(textureSize, seed, frequency);
        Graphics.Blit(noise, target);
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

    void Update() {
        if (refresh) {
            refresh = false;
            Start();
        }
    }

    private void OnValidate() {
        if (generate) {
            generate = false;
            Start();
        }
    }
}

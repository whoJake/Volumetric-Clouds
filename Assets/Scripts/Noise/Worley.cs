using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Worley : Generator {

    public Vector2Int outputDimensions;

    public bool invert;
    public int seed;
    public float frequency;
    [Min(0)]
    public int octaves;
    [Range(0f, 1f)]
    public float persistance;
    [Range(1f, 3f)]
    public float lacunarity;

    public bool refresh;

    private Texture2D noiseImage;

    void Start() {

        RebuildTextures(new Vector2Int(outputDimensions.x, outputDimensions.y));

        float[,] noise = PerlinGen.Generate2DFractal(outputDimensions.x, outputDimensions.y, seed, octaves, frequency, persistance, lacunarity);
        //float[,] noise = PerlinGen.Generate2D(outputDimensions.x, outputDimensions.y, seed, frequency);
        for(int x = 0; x < outputDimensions.x; x++) {
            for(int y = 0; y < outputDimensions.y; y++) {
                float noiseVal = invert ? 1 - noise[x, y] : noise[x, y];
                Color color = new Color(noiseVal, noiseVal, noiseVal, 1);
                noiseImage.SetPixel(x, y, color);
            }
        }

        Apply();
    }

    void RebuildTextures(Vector2Int outputDimensions) {
        if(target.width != outputDimensions.x || target.height != outputDimensions.y) {
            target.Release();
            target.width = outputDimensions.x;
            target.height = outputDimensions.y;
            target.depth = 0;
            target.Create();
        }

        if(noiseImage == null || noiseImage.width != outputDimensions.x || noiseImage.height != outputDimensions.y) {
            noiseImage = new Texture2D(outputDimensions.x, outputDimensions.y);
        }
    }

    void Apply() {
        noiseImage.Apply();
        Graphics.Blit(noiseImage, target);
    }

    void Update() {
        if (refresh) {
            refresh = false;
            Start();
        }
    }
}

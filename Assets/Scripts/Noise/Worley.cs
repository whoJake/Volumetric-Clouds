using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Worley : Generator {

    public Vector2Int outputDimensions;

    public bool invert;
    public int seed;
    public float frequency;

    public bool refresh;

    private Texture2D noiseImage;

    class WrappingGrid {
        public WrappingGrid(int width, int height) {
            values = new Vector2[width, height];
        }

        private Vector2[,] values;

        //Needed to achieve correct % implementation so that eg. -1 % 3 = 2
        int mod(int x, int m) {
            return (x % m + m) % m;
        }

        public Vector2 this[int x, int y]{
            get{
                return values[mod(x, values.GetLength(0)), mod(y, values.GetLength(1))];
            }
            set{ 
                values[x % values.GetLength(0), y % values.GetLength(1)] = value; 
            }
        }
    }

    void Start() {

        RebuildTextures(outputDimensions);
        float[,] buffer1 = Generate(outputDimensions, seed, frequency);
        float[,] buffer2 = Generate(outputDimensions, seed, frequency * 2);
        float[,] buffer3 = Generate(outputDimensions, seed, frequency * 4);

        for (int x = 0; x < outputDimensions.x; x++) {
            for(int y = 0; y < outputDimensions.y; y++) {

                //Temporary fractal implementation
                float val1 = invert ? 1 - buffer1[x, y] : buffer1[x, y];
                float val2 = invert ? 1 - buffer2[x, y] : buffer2[x, y];
                float val3 = invert ? 1 - buffer3[x, y] : buffer3[x, y];

                val1 *= 1f;
                val2 *= 0.5f;
                val3 *= 0.25f;

                float val = (val1 + val2 + val3) / 1.75f;

                Color input = new Color(val, val, val, 1);
                noiseImage.SetPixel(x, y, input);
            }
        }

        Apply();
    }

    float[,] Generate(Vector2Int imageSize, int seed, float frequency) {

        float[,] imgbuffer = new float[imageSize.x, imageSize.y];

        //Frequency of 1 converts to a 2x2 grid
        int gridSize = Mathf.CeilToInt(frequency * 2);
        WrappingGrid grid = new WrappingGrid(gridSize, gridSize);

        Random.InitState(seed);
        //Create grid with random points inside
        for(int x = 0; x < gridSize; x++) {
            for(int y = 0; y < gridSize; y++) {
                grid[x, y] = new Vector2(Random.Range(0f, 1f), Random.Range(0f, 1f));
            }
        }

        //Loop every pixel and find closest point on grid
        Vector2 gridPixelSize = outputDimensions / gridSize;

        for(int x = 0; x < outputDimensions.x; x++) {
            for(int y = 0; y < outputDimensions.y; y++) {

                //Find grid that pixel is in
                int pgx = Mathf.FloorToInt(x / gridPixelSize.x);
                int pgy = Mathf.FloorToInt(y / gridPixelSize.y);

                float closestDistance = float.PositiveInfinity;

                //Search all neighbours (wraps around)
                for(int gx = pgx - 1; gx <= pgx + 1; gx++) {
                    for(int gy = pgy - 1; gy <= pgy + 1; gy++) {
                        Vector2 pointInGrid = grid[gx, gy] * gridPixelSize;
                        Vector2 point = new Vector2(gx * gridPixelSize.x + pointInGrid.x, gy * gridPixelSize.y + pointInGrid.y);

                        float dist = Vector2.Distance(new Vector2(x, y), point);
                        if (dist < closestDistance) {
                            closestDistance = dist;
                        }
                    }
                }

                //Normalize the distance
                float val = closestDistance / (gridPixelSize.x > gridPixelSize.y ? gridPixelSize.x : gridPixelSize.y);
                imgbuffer[x, y] = val;
            }
        }

        return imgbuffer;
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

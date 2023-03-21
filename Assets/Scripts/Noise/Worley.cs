using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;

public class Worley : Generator {

    public Vector2Int outputDimensions;

    public bool invert;
    public int seed;
    public float frequency;

    public bool refresh;
    public bool auto;

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

    void Start()
    {
        Generate();
    }

    void Generate() {
        RebuildTextures(outputDimensions);

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
                val = invert ? 1 - val : val;
                noiseImage.SetPixel(x, y, new UnityEngine.Color(val, val, val, 1));
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
            Generate();
        }
    }

    void OnValidate() {
        if (auto) {
            Generate();
        }
    }

}

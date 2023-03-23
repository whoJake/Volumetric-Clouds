using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PerlinGen
{
    public static float[,] Generate2D(int sizeX, int sizeY, int seed, float frequency) {
        float[,] buffer = new float[sizeX, sizeY];

        //Find number of intervals that need to be calculated
        //"integer" interval arbitrarily set to 128 pixels
        int cellSize = Mathf.CeilToInt(128 / frequency);

        int gridSizeX = Mathf.CeilToInt(sizeX / (float)cellSize);
        int gridSizeY = Mathf.CeilToInt(sizeY / (float)cellSize);

        //Generate random vectors on these intervals
        Random.InitState(seed);
        WrappingGrid2D grid = new WrappingGrid2D(gridSizeX, gridSizeY);
        for(int curX = 0; curX < gridSizeX; curX++) {
            for(int curY = 0; curY < gridSizeY; curY++) {
                //grid[curX, curY] = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));

                //Original perlin noise used only diagonal gradients rather than random directions
                int x = Random.Range(-1f, 1f) < 0 ? -1 : 1;
                int y = Random.Range(-1f, 1f) < 0 ? -1 : 1;
                grid[curX, curY] = new Vector2(x, y);
            }
        }

        //Loop every pixel
        //Find which intervals the pixel lands between
        //Sample the 4 intervals around the pixel
        //Calculate value of pixel
        //Normalize
        for(int x = 0; x < sizeX; x++) {
            for(int y = 0; y < sizeY; y++) {
                Vector2 pixel = new Vector2(x, y);

                //Find grid
                int gridLocX = Mathf.FloorToInt(x / (float)cellSize);
                int gridLocY = Mathf.FloorToInt(y / (float)cellSize);

                //0-1 vector inside grid
                Vector2 pixelFloat = (pixel - new Vector2(gridLocX, gridLocY)*cellSize) / cellSize;

                float[] dots = new float[4];
                int count = 0;

                //Loop the 4 corners
                //Calculate their dot product values
                for(int gridX = gridLocX; gridX <= gridLocX + 1; gridX++) {
                    for(int gridY = gridLocY; gridY <= gridLocY + 1; gridY++) {
                        //Calculate vector from corner to pixel
                        Vector2 cornerOrigin = new Vector2(gridX - gridLocX, gridY - gridLocY);
                        Vector2 vecToCorner = pixelFloat - cornerOrigin;

                        //Dot product this vector and the random vector of that corner
                        float dot = Vector2.Dot(grid[gridX, gridY], vecToCorner);
                        //Add to output (un-normalized)
                        dots[count] = dot;
                        count++;
                    }
                }

                //Interpolate between dots
                Vector2 transformedPixelFloat = new Vector2(Fade(pixelFloat.x), Fade(pixelFloat.y));

                float value = Mathf.Lerp(Mathf.Lerp(dots[0], dots[1], transformedPixelFloat.y),
                                         Mathf.Lerp(dots[2], dots[3], transformedPixelFloat.y),
                                         transformedPixelFloat.x);

                //Transform from (-1 -> 1) to (0 -> 1)
                buffer[x, y] = (value + 1) / 2;
            }
        }

        return buffer;

    }

    public static float[,] Generate2DFractal(int sizeX, int sizeY, int seed, int octaves, float frequency, float persistance, float lacunarity) {
        float[,] noise = new float[sizeX, sizeY];

        float freq = frequency;
        float amplitude = 1;
        float totalAmplitude = 0;

        //Calculate octaves
        for (int octave = 0; octave < octaves; octave++) {
            totalAmplitude += amplitude;

            float[,] octaveNoise = Generate2D(sizeX, sizeY, seed, freq);

            for (int x = 0; x < sizeX; x++) {
                for (int y = 0; y < sizeY; y++) {
                    noise[x, y] += octaveNoise[x, y] * amplitude;
                }
            }

            amplitude *= persistance;
            freq *= lacunarity;
        }

        //Renormalize results
        for (int x = 0; x < sizeX; x++) {
            for (int y = 0; y < sizeY; y++) {
                noise[x, y] /= totalAmplitude;
            }
        }

        return noise;
    }

    //https://rtouti.github.io/graphics/perlin-noise-algorithm
    static float Fade(float t) {
        return ((6 * t - 15) * t + 10) * t * t * t;
    }

    class WrappingGrid2D {
        public WrappingGrid2D(int width, int height) {
            values = new Vector2[width, height];
        }

        private Vector2[,] values;

        //Needed to achieve correct % implementation so that eg. -1 % 3 = 2
        int mod(int x, int m) {
            return (x % m + m) % m;
        }

        public Vector2 this[int x, int y] {
            get {
                return values[mod(x, values.GetLength(0)),
                              mod(y, values.GetLength(1))];
            }
            set {
                values[mod(x, values.GetLength(0)),
                       mod(y, values.GetLength(1))] = value;
            }
        }
    }

    class WrappingGrid3D {
        public WrappingGrid3D(int width, int height, int depth) {
            values = new Vector3[width, height, depth];
        }

        private Vector3[,,] values;

        //Needed to achieve correct % implementation so that eg. -1 % 3 = 2
        int mod(int x, int m) {
            return (x % m + m) % m;
        }

        public Vector3 this[int x, int y, int z] {
            get {
                return values[mod(x, values.GetLength(0)),
                              mod(y, values.GetLength(1)),
                              mod(z, values.GetLength(2))];
            }
            set {
                values[mod(x, values.GetLength(0)),
                       mod(y, values.GetLength(1)),
                       mod(z, values.GetLength(2))] = value;
            }
        }
    }
}

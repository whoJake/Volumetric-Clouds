using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class WorleyGen
{
    
    public static float[,] Generate2D(int sizeX, int sizeY, int seed, float frequency) {
        float[,] buffer = new float[sizeX, sizeY];

        //Find size of grid that needs to be created
        //frequency will arbitrarily translate to 128 pixels
        float cellSize = 128 / frequency;

        //Find how many cells need to be calculated on each dimension
        int gridSizeX = Mathf.CeilToInt(sizeX / cellSize);
        int gridSizeY = Mathf.CeilToInt(sizeY / cellSize);

        //Calculate a point inside each of these cells
        //A point is a vector that points from the top left corner of the cell to the point
        Random.InitState(seed);
        WrappingGrid2D grid = new WrappingGrid2D(gridSizeX, gridSizeY);
        for(int curX = 0; curX < gridSizeX; curX++) {
            for(int curY = 0; curY < gridSizeY; curY++) {
                grid[curX, curY] = new Vector2(Random.Range(0f, 1f), Random.Range(0f, 1f));
            }
        }

        //Loop every pixel
        //Find what grid it is in
        //Check its neighbours points
        //Set value to the closest point that it detects
        //Normalize value from range 0-cellSize -> 0-1
        for(int x = 0; x < sizeX; x++) {
            for(int y = 0; y < sizeY; y++) {
                Vector2 pixel = new Vector2(x, y);

                //Find grid
                int gridLocX = Mathf.FloorToInt(x / cellSize);
                int gridLocY = Mathf.FloorToInt(y / cellSize);


                float nearestDist = float.PositiveInfinity;
                //Check neighbours
                for(int gridX = gridLocX - 1; gridX <= gridLocX + 1; gridX++) {
                    for(int gridY = gridLocY - 1; gridY <= gridLocY + 1; gridY++) {
                        Vector2 gridOrigin = new Vector2(gridX * cellSize, gridY * cellSize);
                        Vector2 point = gridOrigin + (grid[gridX, gridY] * cellSize);

                        float dist = Vector2.Distance(pixel, point);

                        if (dist < nearestDist) nearestDist = dist;
                    }
                }

                //Normalize
                float value = nearestDist / cellSize;

                buffer[x, y] = value;
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

    public static float[,,] Generate3D(int sizeX, int sizeY, int sizeZ, int seed, float frequency) {
        float[,,] buffer = new float[sizeX, sizeY, sizeZ];

        //Find size of grid that needs to be created
        //frequency will arbitrarily translate to 128 pixels
        float cellSize = 128 / frequency;

        //Find how many cells need to be calculated on each dimension
        int gridSizeX = Mathf.CeilToInt(sizeX / cellSize);
        int gridSizeY = Mathf.CeilToInt(sizeY / cellSize);
        int gridSizeZ = Mathf.CeilToInt(sizeZ / cellSize);

        //Calculate a point inside each of these cells
        //A point is a vector that points from the top left corner of the cell to the point
        Random.InitState(seed);
        WrappingGrid3D grid = new WrappingGrid3D(gridSizeX, gridSizeY, gridSizeZ);
        for (int curX = 0; curX < gridSizeX; curX++) {
            for (int curY = 0; curY < gridSizeY; curY++) {
                for(int curZ = 0; curZ < gridSizeZ; curZ++) {
                    grid[curX, curY, curZ] = new Vector3(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
                }
            }
        }

        //Loop every pixel
        //Find what grid it is in
        //Check its neighbours points
        //Set value to the closest point that it detects
        //Normalize value from range 0-cellSize -> 0-1
        for (int x = 0; x < sizeX; x++) {
            for (int y = 0; y < sizeY; y++) {
                for (int z = 0; z < sizeZ; z++) {
                    Vector3 pixel = new Vector3(x, y, z);

                    //Find grid
                    int gridLocX = Mathf.FloorToInt(x / cellSize);
                    int gridLocY = Mathf.FloorToInt(y / cellSize);
                    int gridLocZ = Mathf.FloorToInt(z / cellSize);

                    float nearestDist = float.PositiveInfinity;
                    //Check neighbours
                    for (int gridX = gridLocX - 1; gridX <= gridLocX + 1; gridX++) {
                        for (int gridY = gridLocY - 1; gridY <= gridLocY + 1; gridY++) {
                            for (int gridZ = gridLocZ - 1; gridZ <= gridLocZ + 1; gridZ++) {
                                Vector3 gridOrigin = new Vector3(gridX * cellSize, gridY * cellSize, gridZ * cellSize);
                                Vector3 point = gridOrigin + (grid[gridX, gridY, gridZ] * cellSize);

                                float dist = Vector3.Distance(pixel, point);

                                if (dist < nearestDist) nearestDist = dist;
                            }
                        }
                    }

                    //Normalize
                    float value = nearestDist / cellSize;

                    buffer[x, y, z] = value;
                }
            }
        }
        return buffer;
    }

    public static float[,,] Generate3DFractal(int sizeX, int sizeY, int sizeZ, int seed, int octaves, float frequency, float persistance, float lacunarity) {
        float[,,] noise = new float[sizeX, sizeY, sizeZ];

        float freq = frequency;
        float amplitude = 1;
        float totalAmplitude = 0;

        //Calculate octaves
        for (int octave = 0; octave < octaves; octave++) {
            totalAmplitude += amplitude;

            float[,,] octaveNoise = Generate3D(sizeX, sizeY, sizeZ, seed, freq);

            for (int x = 0; x < sizeX; x++) {
                for (int y = 0; y < sizeY; y++) {
                    for (int z = 0; z < sizeZ; z++) {
                        noise[x, y, z] += octaveNoise[x, y, z] * amplitude;
                    }
                }
            }

            amplitude *= persistance;
            freq *= lacunarity;
        }

        //Renormalize results
        for (int x = 0; x < sizeX; x++) {
            for (int y = 0; y < sizeY; y++) {
                for (int z = 0; z < sizeZ; z++) {
                    noise[x, y, z] /= totalAmplitude;
                }
            }
        }

        return noise;
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
            get 
            {
                return values[mod(x, values.GetLength(0)),
                              mod(y, values.GetLength(1))];
            }
            set 
            {
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
            get 
            {
                return values[mod(x, values.GetLength(0)),
                              mod(y, values.GetLength(1)),
                              mod(z, values.GetLength(2))];
            }
            set 
            {
                values[mod(x, values.GetLength(0)),
                       mod(y, values.GetLength(1)),
                       mod(z, values.GetLength(2))] = value;
            }
        }
    }

}

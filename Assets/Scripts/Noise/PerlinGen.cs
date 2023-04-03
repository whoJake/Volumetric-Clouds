using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PerlinGen
{

    static ComputeShader generatorShader = Resources.Load<ComputeShader>("PerlinCompute");
    static ComputeShader combiner = Resources.Load<ComputeShader>("CombineFractals");

    public static RenderTexture Generate2DGPU(int textureSize, int seed, int frequency) {
        Vector2[,] gradients = new Vector2[frequency, frequency];

        //Create the random gradients
        Random.InitState(seed);
        for(int y = 0; y < frequency; y++) {
            for(int x = 0; x < frequency; x++) {
                float rx = Random.Range(-1f, 1f) < 0f ? 1f : -1f;
                float ry = Random.Range(-1f, 1f) < 0f ? 1f : -1f;

                gradients[x, y] = new Vector2(rx, ry);
            }
        }

        ComputeBuffer gradientBuffer = new ComputeBuffer(frequency * frequency, sizeof(float) * 2);
        gradientBuffer.SetData(gradients);

        RenderTexture rt = new RenderTexture(textureSize, textureSize, 0);
        rt.format = RenderTextureFormat.RFloat;
        rt.enableRandomWrite = true;
        rt.Create();

        generatorShader.SetTexture(0, "_Result2D", rt);
        generatorShader.SetBuffer(0, "_RandomizedVectors2D", gradientBuffer);
        generatorShader.SetInt("texture_size", textureSize);
        generatorShader.SetInt("frequency", frequency);

        int threads = Mathf.CeilToInt(textureSize / 8f);
        generatorShader.Dispatch(0, threads, threads, 1);

        gradientBuffer.Release();
        return rt;

    }

    public static RenderTexture Generate3DGPU(int textureSize, int seed, int frequency) {
        Vector3[,,] gradients = new Vector3[frequency, frequency, frequency];

        //Create the random gradients
        Random.InitState(seed);
        for (int z = 0; z < frequency; z++) {
            for (int y = 0; y < frequency; y++) {
                for (int x = 0; x < frequency; x++) {
                    float rx = Random.Range(-1f, 1f) < 0f ? 1f : -1f;
                    float ry = Random.Range(-1f, 1f) < 0f ? 1f : -1f;
                    float rz = Random.Range(-1f, 1f) < 0f ? 1f : -1f;

                    gradients[x, y, z] = new Vector3(rx, ry, rz);
                }
            }
        }

        ComputeBuffer gradientBuffer = new ComputeBuffer(frequency * frequency * frequency, sizeof(float) * 3);
        gradientBuffer.SetData(gradients);

        RenderTexture rt = new RenderTexture(textureSize, textureSize, 0);
        rt.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        rt.volumeDepth = textureSize;
        rt.format = RenderTextureFormat.RFloat;
        rt.enableRandomWrite = true;
        rt.Create();

        generatorShader.SetTexture(1, "_Result3D", rt);
        generatorShader.SetBuffer(1, "_RandomizedVectors3D", gradientBuffer);
        generatorShader.SetInt("texture_size", textureSize);
        generatorShader.SetInt("frequency", frequency);

        int threads = Mathf.CeilToInt(textureSize / 8f);
        generatorShader.Dispatch(1, threads, threads, threads);

        gradientBuffer.Release();
        return rt;

    }

    public static RenderTexture Generate2DFractalGPU(int textureSize, int seed, int octaves, int frequency, float persistance) {
        if (octaves <= 1) return Generate2DGPU(textureSize, seed, frequency);

        RenderTexture generated;
        RenderTexture final = new RenderTexture(textureSize, textureSize, 0);

        final.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
        final.format = RenderTextureFormat.RFloat;
        final.filterMode = FilterMode.Bilinear;
        final.enableRandomWrite = true;
        final.Create();

        int threadSize = Mathf.CeilToInt(textureSize / 8f);

        float amplitude = 1;
        float magnitude = 0;
        for (int i = 0; i < octaves; i++) {
            generated = Generate2DGPU(textureSize, seed + i, frequency);

            combiner.SetFloat("source_amplitude", amplitude);
            combiner.SetTexture(0, "_Source2D", generated);
            combiner.SetTexture(0, "_Destination2D", final);
            combiner.Dispatch(0, threadSize, threadSize, 1);

            generated.Release();

            magnitude += amplitude;
            frequency *= 2;
            amplitude *= persistance;
        }

        //Im sure I could have done this normalization when adding the textures but I realy don't want to do 5 minutes of maths
        //Normalize back to 0-1
        combiner.SetTexture(2, "_Source2D", final);
        combiner.SetFloat("magnitude", magnitude);
        combiner.Dispatch(2, threadSize, threadSize, 1);

        return final;
    }

    public static RenderTexture Generate3DFractalGPU(int textureSize, int seed, int octaves, int frequency, float persistance) {
        if (octaves <= 1) return Generate3DGPU(textureSize, seed, frequency);

        RenderTexture generated;
        RenderTexture final = new RenderTexture(textureSize, textureSize, 0);

        final.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        final.volumeDepth = textureSize;
        final.format = RenderTextureFormat.RFloat;
        final.filterMode = FilterMode.Bilinear;
        final.enableRandomWrite = true;
        final.Create();

        int threadSize = Mathf.CeilToInt(textureSize / 8f);

        float amplitude = 1;
        float magnitude = 0;

        for (int i = 0; i < octaves; i++) {
            generated = Generate3DGPU(textureSize, seed + i, frequency);

            combiner.SetFloat("source_amplitude", amplitude);
            combiner.SetTexture(1, "_Source3D", generated);
            combiner.SetTexture(1, "_Destination3D", final);
            combiner.Dispatch(1, threadSize, threadSize, threadSize);

            generated.Release();

            magnitude += amplitude;
            frequency *= 2;
            amplitude *= persistance;
        }

        //Im sure I could have done this normalization when adding textures but I realy don't want to do 5 minutes of maths
        //Normalize back to 0-1
        combiner.SetTexture(3, "_Source3D", final);
        combiner.SetFloat("magnitude", magnitude);
        combiner.Dispatch(3, threadSize, threadSize, threadSize);

        return final;
    }


    #region CPU Perlin Implementation
    [System.Obsolete("Replaced by GPU Implementation")]
    public static float[,] Generate2D(int sizeX, int sizeY, int seed, float frequency) {
        float[,] buffer = new float[sizeX, sizeY];

        //Find number of intervals that need to be calculated
        //interval arbitrarily set using sizeX, in reality texture is probably square so doesn't matter
        int cellSize = Mathf.CeilToInt(sizeX / frequency);

        MonoBehaviour.print("Cell size : " + cellSize);

        int gridSizeX = Mathf.CeilToInt(sizeX / (float)cellSize);
        int gridSizeY = Mathf.CeilToInt(sizeY / (float)cellSize);

        MonoBehaviour.print("Grid coverage : " + new Vector2(cellSize * gridSizeX, cellSize * gridSizeY));

        Vector2 overshoot = new Vector2(gridSizeX * cellSize - sizeX, gridSizeY * cellSize - sizeY);
        overshoot /= 2f;
        MonoBehaviour.print("Calculated tiling error : " + overshoot);

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
                Vector2 gridPixelCoord = new Vector2(gridLocX, gridLocY) * cellSize;

                Vector2 pixelFloat = (pixel - gridPixelCoord) / (float)cellSize;

                //MonoBehaviour.print(pixelFloat);

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

    [System.Obsolete("Replaced by GPU Implementation")]
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

    [System.Obsolete("Replaced by GPU Implementation")]
    public static float[,,] Generate3D(int sizeX, int sizeY, int sizeZ, int seed, float frequency) {
        float[,,] buffer = new float[sizeX, sizeY, sizeZ];

        //Find number of intervals that need to be calculated
        //interval arbitrarily set using sizeX, in reality texture is probably square so doesn't matter
        int cellSize = Mathf.CeilToInt(sizeX / frequency);

        int gridSizeX = Mathf.CeilToInt(sizeX / (float)cellSize);
        int gridSizeY = Mathf.CeilToInt(sizeY / (float)cellSize);
        int gridSizeZ = Mathf.CeilToInt(sizeZ / (float)cellSize);

        Vector3 overshoot = new Vector3(gridSizeX * cellSize - sizeX, gridSizeY * cellSize - sizeY, gridSizeZ * cellSize - sizeZ);
        overshoot /= 2f;

        //Generate random vectors on these intervals
        Random.InitState(seed);
        WrappingGrid3D grid = new WrappingGrid3D(gridSizeX, gridSizeY, gridSizeZ);
        for (int curX = 0; curX < gridSizeX; curX++) {
            for (int curY = 0; curY < gridSizeY; curY++) {
                for (int curZ = 0; curZ < gridSizeZ; curZ++) {

                    //grid[curX, curY] = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));

                    //Original perlin noise used only diagonal gradients rather than random directions
                    int x = Random.Range(-1f, 1f) < 0 ? -1 : 1;
                    int y = Random.Range(-1f, 1f) < 0 ? -1 : 1;
                    int z = Random.Range(-1f, 1f) < 0 ? -1 : 1;
                    grid[curX, curY, curZ] = new Vector3(x, y, z);
                }
            }
        }

        //Loop every pixel
        //Find which intervals the pixel lands between
        //Sample the 8 intervals around the pixel
        //Calculate value of pixel
        //Normalize
        for (int x = 0; x < sizeX; x++) {
            for (int y = 0; y < sizeY; y++) {
                for (int z = 0; z < sizeZ; z++) {
                    Vector3 pixel = new Vector3(x, y, z);

                    //Find grid
                    int gridLocX = Mathf.FloorToInt((x) / (float)cellSize);
                    int gridLocY = Mathf.FloorToInt((y) / (float)cellSize);
                    int gridLocZ = Mathf.FloorToInt((z) / (float)cellSize);

                    //0-1 vector inside grid
                    Vector3 gridPixelCoord = new Vector3(gridLocX, gridLocY, gridLocZ) * cellSize;
                    //gridPixelCoord -= overshoot;

                    Vector3 pixelFloat = (pixel - gridPixelCoord) / (float)cellSize;

                    //MonoBehaviour.print(pixelFloat);

                    float[] dots = new float[8];
                    int count = 0;

                    //Loop the 8 corners
                    //Calculate their dot product values
                    for (int gridX = gridLocX; gridX <= gridLocX + 1; gridX++) {
                        for (int gridY = gridLocY; gridY <= gridLocY + 1; gridY++) {
                            for (int gridZ = gridLocZ; gridZ <= gridLocZ + 1; gridZ++) {
                                //Calculate vector from corner to pixel
                                Vector3 cornerOrigin = new Vector3(gridX - gridLocX, gridY - gridLocY, gridZ - gridLocZ);
                                Vector3 vecToCorner = pixelFloat - cornerOrigin;

                                //Dot product this vector and the random vector of that corner
                                float dot = Vector3.Dot(grid[gridX, gridY, gridZ], vecToCorner);
                                //Add to output (un-normalized)
                                dots[count] = dot;
                                count++;
                            }
                        }
                    }

                    //Interpolate between dots
                    Vector3 transformedPixelFloat = new Vector3(Fade(pixelFloat.x), Fade(pixelFloat.y), Fade(pixelFloat.z));

                    float value = Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(dots[0], dots[1], transformedPixelFloat.z),
                                                        Mathf.Lerp(dots[2], dots[3], transformedPixelFloat.z),
                                                        transformedPixelFloat.y),
                                             Mathf.Lerp(Mathf.Lerp(dots[4], dots[5], transformedPixelFloat.z),
                                                        Mathf.Lerp(dots[6], dots[7], transformedPixelFloat.z),
                                                        transformedPixelFloat.y),
                                             transformedPixelFloat.x);

                    //Transform from (-1 -> 1) to (0 -> 1)
                    buffer[x, y, z] = (value + 1) / 2;
                }
            }
        }

        return buffer;

    }

    [System.Obsolete("Replaced by GPU Implementation")]
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
    #endregion
}

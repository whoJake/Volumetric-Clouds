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

}

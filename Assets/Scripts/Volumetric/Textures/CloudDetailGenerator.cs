using UnityEditor;
using UnityEngine;

public static class CloudDetailGenerator {

    public struct DetailSettings {
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
        //Worley G
        public int g_frequency;
        public int g_octaves;
        public float g_persistance;
        //Worley B
        public int b_frequency;
        public int b_octaves;
        public float b_persistance;
        //Worley A
        public int a_frequency;
        public int a_octaves;
        public float a_persistance;
    }

    public static RenderTexture CreateDetailTexture(DetailSettings s) {
        RenderTexture r_Perlin = PerlinGen.Generate3DFractalGPU(s.detailResolution, s.seed, s.r_octaves_perlin, s.r_frequency_perlin, s.r_persistance_perlin);
        RenderTexture r_Worley = WorleyGen.Generate3DFractalGPU(s.detailResolution, s.seed + 1, s.r_octaves_worley, s.r_frequency_worley, s.r_persistance_worley);
        RenderTexture r_Channel = AdditiveCombine(r_Perlin, r_Worley, 0.5f);

        RenderTexture g_Channel = WorleyGen.Generate3DFractalGPU(s.detailResolution, s.seed + 2, s.g_octaves, s.g_frequency, s.g_persistance);
        RenderTexture b_Channel = WorleyGen.Generate3DFractalGPU(s.detailResolution, s.seed + 3, s.b_octaves, s.b_frequency, s.b_persistance);
        RenderTexture a_Channel = WorleyGen.Generate3DFractalGPU(s.detailResolution, s.seed + 4, s.a_octaves, s.a_frequency, s.a_persistance);

        RenderTexture detailTexture = ChannelCombine(r_Channel, g_Channel, b_Channel, a_Channel);

        //Cleanup
        r_Perlin.Release();
        r_Channel.Release();
        g_Channel.Release();
        b_Channel.Release();
        a_Channel.Release();

        return detailTexture;
    }

    private static ComputeShader additiveCompute = Resources.Load<ComputeShader>("AdditiveCombine");

    public static RenderTexture AdditiveCombine(RenderTexture source, RenderTexture destination, float blend) {

        additiveCompute.SetTexture(0, "_Source", source);
        additiveCompute.SetTexture(0, "_Destination", destination);
        additiveCompute.SetFloat("blend", blend);
        additiveCompute.SetFloat("multiply", 1f);

        int threads = Mathf.CeilToInt(source.width / 8f);

        additiveCompute.Dispatch(0, threads, threads, threads);

        return destination;
    }


    public static ComputeShader channelCompute = Resources.Load<ComputeShader>("ChannelCombine");

    public static RenderTexture ChannelCombine(RenderTexture red, RenderTexture green, RenderTexture blue, RenderTexture alpha) {
        RenderTexture destination = new RenderTexture(red.width, red.width, 0);
        destination.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        destination.format = RenderTextureFormat.ARGBFloat;
        destination.wrapMode = TextureWrapMode.Repeat;
        destination.volumeDepth = red.width;
        destination.enableRandomWrite = true;
        destination.Create();

        channelCompute.SetTexture(0, "_Destination", destination);
        channelCompute.SetTexture(0, "_Red", red);
        channelCompute.SetTexture(0, "_Green", green);
        channelCompute.SetTexture(0, "_Blue", blue);
        channelCompute.SetTexture(0, "_Alpha", alpha);

        int threads = Mathf.CeilToInt(red.width / 8f);
        channelCompute.Dispatch(0, threads, threads, threads);

        return destination;
    }

}

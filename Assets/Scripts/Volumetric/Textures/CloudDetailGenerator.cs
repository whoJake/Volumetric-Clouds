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

        detailTexture = AdjustRange3D(detailTexture);

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

    private static ComputeShader channelCompute = Resources.Load<ComputeShader>("ChannelCombine");

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

    private static ComputeShader adjustRange = Resources.Load<ComputeShader>("AdjustRange");

    public static RenderTexture AdjustRange2D(RenderTexture rt) {
        Texture2D cpuTexture = new Texture2D(rt.width, rt.height);
        RenderTexture.active = rt;
        cpuTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        RenderTexture.active = null;

        Vector3 max = Vector3.one * float.MinValue;
        Vector3 min = Vector3.one * float.MaxValue;

        //Get Max and min values to normalize them
        for(int x = 0; x < rt.width; x++) {
            for(int y = 0; y < rt.height; y++) {
                Color c = cpuTexture.GetPixel(x, y);

                max.x = Mathf.Max(max.x, c.r);
                max.y = Mathf.Max(max.y, c.g);
                max.z = Mathf.Max(max.z, c.b);

                min.x = Mathf.Min(min.x, c.r);
                min.y = Mathf.Min(min.y, c.g);
                min.z = Mathf.Min(min.z, c.b);

            }
        }

        MonoBehaviour.print(min + " " + max);

        adjustRange.SetTexture(0, "_Target2D", rt);
        adjustRange.SetVector("min", min);
        adjustRange.SetVector("max", max);

        int threads = Mathf.CeilToInt(rt.width / 8f);
        adjustRange.Dispatch(0, threads, threads, 1);
        return rt;
    }

    public static RenderTexture AdjustRange3D(RenderTexture rt) {
        //This seems like a really hacky way to get the colors onto the CPU but asfaik theres no way to easily get a 3D RenderTexture onto the CPU
        Vector4[] colors = RTToVector4Array(rt);

        Vector4 max = Vector4.one * float.MinValue;
        Vector4 min = Vector4.one * float.MaxValue;

        //Min and Max have to be calculated on the CPU and then the values can be renormalized on the GPU
        //Get Max and min values to normalize them
        for (int x = 0; x < rt.width; x++) {
            for (int y = 0; y < rt.height; y++) {
                for (int z = 0; z < rt.volumeDepth; z++) {
                    Color c = colors[x + y * rt.width + z * rt.height];

                    max.x = Mathf.Max(max.x, c.r);
                    max.y = Mathf.Max(max.y, c.g);
                    max.z = Mathf.Max(max.z, c.b);
                    max.w = Mathf.Max(max.w, c.a);

                    min.x = Mathf.Min(min.x, c.r);
                    min.y = Mathf.Min(min.y, c.g);
                    min.z = Mathf.Min(min.z, c.b);
                    min.w = Mathf.Min(min.w, c.a);
                }
            }
        }

        adjustRange.SetTexture(1, "_Target3D", rt);
        adjustRange.SetVector("min", min);
        adjustRange.SetVector("max", max);

        int threads = Mathf.CeilToInt(rt.width / 8f);
        adjustRange.Dispatch(1, threads, threads, threads);
        return rt;
    }

    private static ComputeShader texToBuffer = Resources.Load<ComputeShader>("TexToBuffer");

    public static Vector4[] RTToVector4Array(RenderTexture rt) {
        ComputeBuffer buffer = new ComputeBuffer(rt.width * rt.height * rt.volumeDepth, sizeof(float) * 4);
        texToBuffer.SetVector("dims", new Vector3(rt.width, rt.height, rt.volumeDepth));
        texToBuffer.SetBuffer(0, "_Destination", buffer);
        texToBuffer.SetTexture(0, "_Source", rt);

        int threads = Mathf.CeilToInt(rt.width / 8f);

        texToBuffer.Dispatch(0, threads, threads, threads);

        Vector4[] data = new Vector4[rt.width * rt.height * rt.volumeDepth];
        buffer.GetData(data);
        buffer.Release();

        return data;
    }

}

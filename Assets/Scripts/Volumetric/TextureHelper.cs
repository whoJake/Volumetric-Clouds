using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TextureHelper
{
    public static RenderTexture FloatArrayToTexture3D(ComputeShader shader, float[,,] array, int size) {
        RenderTexture rt = new RenderTexture(array.GetLength(0), array.GetLength(1), 0);
        rt.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        rt.filterMode = FilterMode.Bilinear;
        rt.volumeDepth = array.GetLength(2);
        rt.enableRandomWrite = true;
        rt.Create();


        ComputeBuffer buffer = new ComputeBuffer(size * size * size, sizeof(float));
        buffer.SetData(array);

        shader.SetTexture(0, "destination", rt);
        shader.SetInt("texSize", size);
        shader.SetBuffer(0, "source", buffer);

        shader.Dispatch(0, 
                        Mathf.CeilToInt(size / 8f),
                        Mathf.CeilToInt(size / 8f),
                        Mathf.CeilToInt(size / 8f));

        buffer.Release();

        return rt;
    }
}

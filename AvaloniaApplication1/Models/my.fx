cbuffer ConstantBuffer : register(b0)
{
    matrix worldViewProjection;
};

struct VertexInput
{
    float3 position : POSITION;
    float2 texcoord : TEXCOORD;
};

struct PixelInput
{
    float4 position : SV_POSITION;
    float2 texcoord : TEXCOORD;
};

// 顶点着色器
PixelInput VertexShader(VertexInput input)
{
    PixelInput output;
    
    // 计算变换后的顶点坐标
    output.position = mul(float4(input.position, 1.0f), worldViewProjection);

    // 设置纹理坐标
    // 将纹理坐标乘以一个缩放因子，来实现纹理坐标的转换
    output.texcoord = input.texcoord * float2(2.0f, 2.0f);
    
    return output;
}

#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D SpriteTexture;
float4 light_pos;

float4 light_diffuse;
//float4 light_specular;

float light_constant;
float light_linear;
float light_quadratic;

float4 AmbientColor = float4(1, 1, 1, 1);
float AmbientIntensity = 0.1;


float4x4 MatrixTransform;

int UseTexture;




sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
};


struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float2 TextureCoordinates : TEXCOORD0;
};


VertexShaderOutput MainVS(float4 position : SV_POSITION, float4 color : COLOR0, float2 texCoord : TEXCOORD0)
{
    VertexShaderOutput output;
    output.Position = mul(position, MatrixTransform);
    output.Color = color;
    output.TextureCoordinates = texCoord;
    return output;
}


float4 MainPS(VertexShaderOutput input) : COLOR
{
    float4 texColor = float4(1, 1, 1, 1);
    if (UseTexture) texColor = tex2D(SpriteTextureSampler, input.TextureCoordinates);
	return texColor * input.Color;
}

technique SpriteDrawing
{
	pass P0
	{
        VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};


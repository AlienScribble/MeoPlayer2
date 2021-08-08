#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// TEXTURES / SAMPLERS
Texture2D SpriteTexture;
sampler2D spriteTexSamp = sampler_state
{
	Texture = <SpriteTexture>;        
};

// PARAMETERS
float4x4 MatrixTransform;
int UseTexture;


// SEND TO PIXEL SHADER
struct VertexShaderOutput
{
	float4 Position           : SV_POSITION;
	float4 Color              : COLOR0;
	float2 TexCoords          : TEXCOORD0;
};



// M A I N   V S   (mose sprite calculations are done on CPU side) 
VertexShaderOutput MainVS(float3 position : SV_POSITION, float4 color : COLOR0, float2 texCoord : TEXCOORD0)
{
    VertexShaderOutput output;
    output.Position  = mul(float4(position, 1), MatrixTransform);
    output.Color     = color;
    output.TexCoords = texCoord;
    return output;
}


// S P R I T E   S H A D E R   V E R T E X   S H A D E R   S T U F F - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 
// I N P U T S ( for SpriteVS )
struct VS_SPR_IN
{
    float3 pos         : SV_POSITION1;
    float3 origin_rot  : NORMAL1;
    float4 uv_siz      : TEXCOORD1;
    float4 color       : COLOR1; 
};
// S P R I T E   V S   (most sprite calculations are done here in GPU)
VertexShaderOutput SpriteVS(VS_SPR_IN inp)
{
    VertexShaderOutput output;
    float ox  = inp.origin_rot.x, oy = inp.origin_rot.y;
    float dx = (inp.pos.x - ox) * inp.uv_siz.z;
    float dy = (inp.pos.y - oy) * inp.uv_siz.w;
    float r   = inp.origin_rot.z;
    float cs  = cos(r), sn = sin(r);     
    inp.pos.x = ox + dx * cs - dy * sn;
    inp.pos.y = oy + dx * sn + dy * cs;  
    output.Position  = mul(float4(inp.pos, 1), MatrixTransform);
    output.Color     = inp.color;
    output.TexCoords = float2(inp.uv_siz.x, inp.uv_siz.y);
    return output;
}
// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -




// P I X E L   S H A D E R :

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float4 texColor = float4(1, 1, 1, 1);
    if (UseTexture) texColor = tex2D(spriteTexSamp, input.TexCoords);
	return texColor * input.Color;
}



// T E C H N I Q U E S ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ 
#define TECHNIQUE(name, vsname, psname ) technique name { pass { VertexShader = compile VS_SHADERMODEL vsname(); PixelShader = compile PS_SHADERMODEL psname(); } }

TECHNIQUE(MainShader,   MainVS,   MainPS);  // CPU side calculations (less vertex buffer memory)
TECHNIQUE(SpriteShader, SpriteVS, MainPS);  // GPU side calculations (higher performance) 



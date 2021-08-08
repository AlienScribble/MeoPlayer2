using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MeoMo {   

    #region CUSTOM SHADER NOTES
        
    /// ********************************************
    /// !  C U S T O M   S H A D E R   N O T E S  !  (important) 
    ///*********************************************
    /// If your fx file contains a pixel shader, only the pixel shader will be swapped (default vertex shader is still used)
    /// So, if you are only using a new pixel shader (no vertex shader) in your 'fx', then you can ignore this message:
    // IF you will use your own vertex shaders in your fx file, you'll need to include a "MainShader" technique 
    // and a "SpriteShader" techinique. I suggest to copy the ones from "SpriteBlast.fx" and make modifications. 
    // These 2 different shaders handle situations where a batch of sprites require more calculations[GPU side] (using VertexSpriteData), 
    // and the other is better for handling low-calculation situations[CPU side] (using VertexPositionColorTexture) [regular]    
    #endregion


    // N E W    F A S T E R   Q U A D B A T C H   ( similar to spritebatch but allows deformations )          
    public class SpriteBlast {        
        
        //-----------------------------------------------------------------------------------------
        #region V E R T E X   T Y P E S                 
        // B Y T (adjusts byte offset for each entry in a vertex declaration)
        public struct BYT {
            public static int byt = 0;
            public static int Ini(int b_size) { b_size *= 4; byt = 0; byt += b_size; return 0; }
            public static int Off(int b_size) { b_size *= 4; byt += b_size; return byt - b_size; }
        }

        // V E R T E X   S P R I T E   D A T A  (shader ignores rot / scale if rot = 0 or scale = 1:1)
        [StructLayout(LayoutKind.Sequential)]
        public struct VertexSpriteData : IVertexType {
            public Vector3 position;       // position(3)
            public Vector3 origin_rot;     // origin(2) + rot(1)
            public Vector4 texCoord_scale; // textureCoord(2) + scale(2)
            public Color   color;
            public static VertexDeclaration VertexDeclaration = new VertexDeclaration
            (            
                new VertexElement(BYT.Ini(3), VertexElementFormat.Vector3, VertexElementUsage.Position, 1),                
                new VertexElement(BYT.Off(3), VertexElementFormat.Vector3, VertexElementUsage.Normal, 1),
                new VertexElement(BYT.Off(4), VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),                
                new VertexElement(BYT.Off(4), VertexElementFormat.Color,   VertexElementUsage.Color, 1)
            );
            VertexDeclaration IVertexType.VertexDeclaration { get { return VertexDeclaration; } }
        }
        #endregion //-----------------------------------------------------------------------------

        
        const int  VERTS_ALLOC   = 8192,  MAX_VERTS = 8188;        

        GraphicsDevice gpu;
        bool began;

        // QUAD DATA
        VertexSpriteData[]           verts_spr;
        VertexPositionColorTexture[] verts_reg;
        short[]      indices;
        VertexBuffer vertexBuffer_spr, vertexBuffer_reg;
        IndexBuffer  indexBuffer;
        int          vert_count_spr, vert_count_reg;

        // EFFECTS & TEXTURES
        Effect    default_fx, fx;              // overide pixel shader, vertex shader or both: by setting 'fx' (Suggestion: make fx by modifying QuadEffect as new fx file)  
        Texture2D tex, old_tex;        

        // COORDINATE CONTROLS
        Matrix world, view, proj, ViewProj, WorldViewProj;
        float  normalize_u,  normalize_v;      // scale tex coords for harware based on texture size

        // DEVICE STATE CHANGES
        BlendState        blendState, old_blend;
        SamplerState      samplerState;
        DepthStencilState depthStencilState;  // depth - default = off
        bool update_matrix = true;
        int  screenWidth, screenHeight;

        // PUBLIC ACCESS:
        public float z;                        // current drawing depth (change as needed while drawing)
        public bool  keep_fx = false;          // if you pass an Effect to begin, setting this to true allows you to keep effect applied to next Begin-End
        public bool  originCentered = false;   // false = tiny bit faster / true = center sprite at origin (may want to turn on for characters and off for level pieces)
        public bool  using_default_vertex_shader = true; // set this to false if you are using your own vertex shaders (for pixel shaders: it'll automatically use them if fx is not null)
        public string CustomFX_CPU_Technique = "MainShader", CustomFX_GPU_Technique = "SpriteShader", CustomFX_Particle_Technique = "ParticleInstance";
        public Texture2D pixel_tex; 



        //-----------------------
        #region C O N S T R U C T
        //-----------------------        
        public SpriteBlast(ContentManager Content, GraphicsDevice GPU, bool use3DCamera = false, bool useHalfPixelOffset = true)
        {            
            default_fx   = Content.Load<Effect>("SpriteBlast"); // overide pixel shader, vertex shader or both: by setting 'fx' (you can use QuadEffect as template to create new effects)
            gpu          = GPU;
            screenWidth  = gpu.Viewport.Width;
            screenHeight = gpu.Viewport.Height;
            int ID_ALLOC = VERTS_ALLOC / 4 * 6;

            // PREMADE verts_reg
            verts_spr = new VertexSpriteData[VERTS_ALLOC];        
            verts_reg = new VertexPositionColorTexture[VERTS_ALLOC];            
            indices   = new short[ID_ALLOC];
            int c = 0;
            for (int i = 0; i < VERTS_ALLOC; i += 4) {
                indices[c] = (short)(i + 0); c++; indices[c] = (short)(i + 1); c++; indices[c] = (short)(i + 2); c++;
                indices[c] = (short)(i + 2); c++; indices[c] = (short)(i + 3); c++; indices[c] = (short)(i + 0); c++;
            }                       
            vertexBuffer_reg = new DynamicVertexBuffer(gpu, typeof(VertexPositionColorTexture), VERTS_ALLOC, BufferUsage.WriteOnly);
            vertexBuffer_spr = new DynamicVertexBuffer(gpu, typeof(VertexSpriteData),           VERTS_ALLOC, BufferUsage.WriteOnly);
            indexBuffer      = new IndexBuffer(gpu, typeof(short), ID_ALLOC, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);

            // SET CAMERAS
            world = Matrix.Identity;
            if (useHalfPixelOffset) useHalfPixelOff = true;
            if (use3DCamera) Set3DCamera(new Vector3(screenWidth / 2, screenHeight / 2, -900f), 1f);
            else Set2DCamera();

            blendState          = BlendState.NonPremultiplied;
            samplerState        = SamplerState.LinearClamp;
            depthStencilState   = DepthStencilState.None;
            gpu.RasterizerState = RasterizerState.CullNone;

            // Create a texture for pixels/lines/fills (suggesting to have this on a sprite-sheet instead [reduce texture switching] but it's here for debug use)
            pixel_tex = new Texture2D(gpu, 1, 1, false, SurfaceFormat.Color); // (this texture would be set for use externally)
            pixel_tex.SetData(new[] { Color.White });
        }
        #endregion



        #region C A M E R A S -------------------------------------------------------------------------------------------------------------------------------

        // S E T   3 D   C A M E R A
        public void Set3DCamera(Vector3 eye_pos, float target_z)
        {
            Set3DCamera(eye_pos, new Vector3(eye_pos.X, eye_pos.Y, target_z));
        }
        public void Set3DCamera(Vector3 eye_pos, Vector3 targ_pos)
        {
            view = Matrix.CreateLookAt(eye_pos, targ_pos, Vector3.Down);
            proj = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, gpu.Viewport.AspectRatio, 0.1f, 2000f);
            if (useHalfPixelOff) { Matrix halfPixel = Matrix.CreateTranslation(-0.5f, -0.5f, 0); proj = halfPixel * proj; } // maybe not necessary
            ViewProj      = view * proj;
            WorldViewProj = world * ViewProj;  update_matrix = true;
            z = 20f;
        }


        // S E T   2 D   C A M E R A 
        public bool useHalfPixelOff;
        public void Set2DCamera()
        {
            view = Matrix.Identity;
            proj = Matrix.CreateOrthographicOffCenter(0.0f, screenWidth, screenHeight, 0.0f, -2000f, 2000f);
            if (useHalfPixelOff) { Matrix halfPixel = Matrix.CreateTranslation(-0.5f, -0.5f, 0); proj = halfPixel * proj; }  // <-- fixes half-pixel offset problem            
            ViewProj = view * proj; WorldViewProj = world * ViewProj;  update_matrix = true;
            z = 0.5f;
        }

        // SET WORLD
        public void SetWorld(Matrix World) { world = World; WorldViewProj = world * ViewProj; update_matrix = true; }
        #endregion




        #region B E G I N --------------------------------------------------------------------------------------------------------------------------------------       
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ResetBegin()
        {
            if (began) { Debug.WriteLine("SpriteBlast:  Begin already called."); return false; }
            vert_count_reg   = 0;     vert_count_spr  = 0;
            began            = true;
            if (!keep_fx) fx = null;
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void TexCoordSetup()
        {
            normalize_u = 1.0f / (float)tex.Width;
            normalize_v = 1.0f / (float)tex.Height;
        }
        // BEGIN ------------------------------------------------------------------------
        public void Begin(Texture2D texture) {
            if (!ResetBegin()) return;   tex = texture;     TexCoordSetup();
        }
        public void Begin(BlendState blend_State) {
            if (!ResetBegin()) return;   blendState = blend_State;
        }
        public void Begin(Texture2D texture, BlendState blend_State) {
            if (!ResetBegin()) return;   tex = texture;    blendState = blend_State;    TexCoordSetup();
        }
        public void Begin(Texture2D texture, BlendState blend_State, SamplerState sampler_State)
        {
            if (!ResetBegin()) return;
            tex = texture;    blendState = blend_State;    samplerState = sampler_State;    TexCoordSetup();
        }
        public void Begin(Texture2D texture, BlendState blend_State, SamplerState sampler_State, DepthStencilState depth_StencilState)
        {
            if (!ResetBegin()) return;
            tex = texture;    blendState = blend_State;    samplerState = sampler_State;    depthStencilState = depth_StencilState;    TexCoordSetup();
        }
        public void Begin(Texture2D texture, BlendState blend_state, SamplerState sampler_state, DepthStencilState depth_StencilState, Matrix World, Effect myEffect = null, bool use_default_vertex_shader = true)
        {
            if (!ResetBegin()) return;               blendState = blend_state;  tex = texture;                     samplerState  = sampler_state;
            depthStencilState = depth_StencilState;  world      = World;        WorldViewProj = world * ViewProj;  update_matrix = true;
            if (myEffect != null) fx = myEffect;     TexCoordSetup();           using_default_vertex_shader = use_default_vertex_shader;
        }
        /// <summary> Version of Begin: places sprites so they're centered at their position (depending on origin which marks the "center")</summary>        
        public void Begin_OriginCentered(Texture2D texture) { if (!ResetBegin()) return; tex = texture; TexCoordSetup(); originCentered = true;}
        /// <summary> Version of Begin: places sprites so they're centered at their position (depending on origin which marks the "center")</summary>        
        public void Begin_OriginCentered(BlendState blend_State) { if (!ResetBegin()) return; blendState = blend_State; originCentered = true; }
        /// <summary> Version of Begin: places sprites so they're centered at their position (depending on origin which marks the "center")</summary>        
        public void Begin_OriginCentered(Texture2D texture, BlendState blend_State) {
            if (!ResetBegin()) return; tex = texture; blendState = blend_State; TexCoordSetup(); originCentered = true;
        }
        /// <summary> Version of Begin: places sprites so they're centered at their position (depending on origin which marks the "center")</summary>        
        public void Begin_OriginCentered(Texture2D texture, BlendState blend_State, SamplerState sampler_State) {
            if (!ResetBegin()) return; originCentered = true; tex = texture; blendState = blend_State; samplerState = sampler_State; TexCoordSetup();
        }
        /// <summary> Version of Begin: places sprites so they're centered at their position (depending on origin which marks the "center")</summary>        
        public void Begin_OriginCentered(Texture2D texture, BlendState blend_State, SamplerState sampler_State, DepthStencilState depth_StencilState)
        {
            if (!ResetBegin()) return; originCentered = true;
            tex = texture; blendState = blend_State; samplerState = sampler_State; depthStencilState = depth_StencilState; TexCoordSetup();
        }
        /// <summary> Version of Begin: places sprites so they're centered at their position (depending on origin which marks the "center")</summary>        
        public void Begin_OriginCentered(Texture2D texture, BlendState blend_state, SamplerState sampler_state, DepthStencilState depth_StencilState, Matrix World, Effect myEffect = null, bool use_default_vertex_shader = true)
        {
            if (!ResetBegin()) return;               blendState = blend_state;   tex           = texture;            samplerState   = sampler_state;
            depthStencilState = depth_StencilState;  world      = World;         WorldViewProj = world * ViewProj;   originCentered = true;     update_matrix = true;
            if (myEffect != null) fx = myEffect;     TexCoordSetup();            using_default_vertex_shader = use_default_vertex_shader;
        }
        #endregion


        
        // ( Use these if you'll return to the same texture & blend-state after )        
        public void SwitchTex(Texture2D newTex) {
            old_tex = tex;   old_blend = blendState;
            if ((tex == null) || (newTex.Equals(tex) == false)) { if (began) End(); Begin(newTex); }
            else { TexCoordSetup(); began = true; } 
        }
        public void SwitchTex(Texture2D newTex, BlendState newBlend) {
            old_tex = tex;   old_blend = blendState;
            if ((tex == null) || (newTex.Equals(tex) == false) || (newBlend != blendState)) { if (began) End(); Begin(newTex, newBlend);   }
            else { TexCoordSetup(); began = true; }
        }
        public void RestoreTex() {
            if ((old_tex == tex) && (old_blend == blendState)) return;
            End(); tex = old_tex; Begin(old_tex); blendState = old_blend;
        }



        #region E N D ---------------------------------------------------------------------------------------------------------------------------------------------------------
        /// <summary> This is where FX and rendering is actually done (draws build up the data) </summary>
        /// <param name="reset_originCentering">Set this to true if you want it to stop centering sprites to their origin (centered around their position).</param>        
        public void End(bool reset_originCentering = false)
        {
            if (!began) { Debug.WriteLine("End without Begin call in SpriteBlast"); return; }

            if (reset_originCentering) originCentered = false;  // no longer need to center sprites around their positions
            if ((vert_count_reg >= 3)||(vert_count_spr >=3)) {

                // set devices states
                gpu.BlendState        = blendState;
                gpu.DepthStencilState = depthStencilState;
                gpu.SamplerStates[0]  = samplerState;
                if (gpu.RasterizerState.CullMode != CullMode.None) gpu.RasterizerState = RasterizerState.CullNone;

                int triangle_count;
                if (vert_count_reg >= 3) { // SETUP CPU Calc Shaders:

                    SET_FX("MainShader", CustomFX_CPU_Technique);

                    triangle_count = vert_count_reg/2;
                    vertexBuffer_reg.SetData<VertexPositionColorTexture>(verts_reg, 0, vert_count_reg);
                    gpu.SetVertexBuffer(vertexBuffer_reg);  gpu.Indices = indexBuffer;                    
                    gpu.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, triangle_count);
                    vert_count_reg = 0;
                    old_source1 = new Rectangle(999999, 999999, 0, 0);
                    old_source2 = new Rectangle(999999, 999999, 0, 0);
                    if (old_source3.X != 999999) old_source3 = new Rectangle(999999, 999999, 0, 0); // check only because this is rarely used                    
                }
                if (vert_count_spr >= 3) {  // SETUP GPU Calc Shaders:

                    SET_FX("SpriteShader", CustomFX_GPU_Technique);

                    triangle_count = vert_count_spr / 2;
                    vertexBuffer_spr.SetData<VertexSpriteData>(verts_spr, 0, vert_count_spr);
                    gpu.SetVertexBuffer(vertexBuffer_spr);  gpu.Indices = indexBuffer;
                    gpu.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, triangle_count);
                    vert_count_spr = 0;
                    old_source4 = new Rectangle(999999, 999999, 0, 0);
                    old_source5 = new Rectangle(999999, 999999, 0, 0);
                }                  
            }
            began = false;  
            if (!keep_fx) {
                fx = null; using_default_vertex_shader = true;
            }
        }

        // S E T   F X 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SET_FX(string Technique_Default, string Technique_CustomFX)
        {            
            if ((using_default_vertex_shader)||(fx==null)) {
                default_fx.CurrentTechnique = default_fx.Techniques[Technique_Default];   // TECHNIQUE 1 
                default_fx.CurrentTechnique.Passes[0].Apply();
                if (update_matrix) default_fx.Parameters["MatrixTransform"].SetValue(WorldViewProj);
                if (tex != null) {
                    default_fx.Parameters["UseTexture"].SetValue(1);                    
                    gpu.Textures[0] = tex; 
                } else {
                    default_fx.Parameters["UseTexture"].SetValue(0);
                }
            }
            else {
                fx.CurrentTechnique = fx.Techniques[Technique_CustomFX];                  // TECHNIQUE 1
                fx.CurrentTechnique.Passes[0].Apply();
                if (update_matrix) fx.Parameters["MatrixTransform"]?.SetValue(WorldViewProj);
                if (tex != null) {
                    fx.Parameters["UseTexture"]?.SetValue(1);
                    gpu.Textures[0] = tex; 
                }
                else {
                    fx.Parameters["UseTexture"]?.SetValue(0);                    
                }
            }            
        }
        #endregion




        //--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        #region D R A W   [ C P U  versions ]
        //--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        #region D R A W   T R A N S F O R M E D   V E R T I C E S
        /// <summary> Draw a sprite-quad from 4 verts_reg (offsets from a position) that do not need to be scaled or rotated (usedful with meomotion) </summary>        
        public void DrawTransformedVerts(Rectangle sourceRect, Vector2 position, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, Color c1, SpriteEffects flip = SpriteEffects.None)
        {            
            float u1, v1, u2, v2; 

            p1 += position; p2 += position; p3 += position; p4 += position;
            u1 = sourceRect.X * normalize_u;  // gets the texture coords in terms of (0.0f-1.0f, 0.0f-1.0f)
            v1 = sourceRect.Y * normalize_v;  // " 
            u2 = (sourceRect.X + sourceRect.Width)  * normalize_u;
            v2 = (sourceRect.Y + sourceRect.Height) * normalize_v;
            if ((flip & SpriteEffects.FlipVertically) != 0) {
                var temp = v2; v2 = v1; v1 = temp;
            }
            if ((flip & SpriteEffects.FlipHorizontally) != 0) {
                var temp = u2; u2 = u1; u1 = temp;
            }
            verts_reg[vert_count_reg].Position = new Vector3(p1, z);
            verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u1, v1); // upper-left texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(p2, z);
            verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u2, v1); // upper-right texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(p3, z);
            verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u2, v2); // lower-right texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(p4, z);
            verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u1, v2); // lower-left texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            if (vert_count_reg >= MAX_VERTS) {
                End(); began = true;
            }
        }
        #endregion



        #region D R A W   C P U   (fast)  (origin based) ------------------------------------------
        /// <summary> 1 color & uniform scale [ for drawing from same texture-coords and want to keep calcs on CPU side ] </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Draw_CPU(Rectangle sourceRect, Vector2 pos, Vector2 origin, float scale, float rot, Color c1, SpriteEffects flip = SpriteEffects.None)
        {
            Draw_CPU(sourceRect, pos, origin, new Vector2(scale, scale), rot, c1, flip);
        }

        static float w, h, o_x, o_y, x1, y1, x2, y2, x3, y3, x4, y4, u1, v1, u2, v2; // these can be shared with negligable performance difference    
        Rectangle old_source1 = new Rectangle(999999, 999999, 0, 0); Vector2 old_scale1 = new Vector2(99999), old_origin1 = new Vector2(99999), old_pos1; float old_rot1; SpriteEffects old_spr_effect1 = SpriteEffects.None;
        // D R A W   C P U  ( fast ) -------------------------------------------
        /// <summary> Non-Uniform scale( 1 color ) [ used when repeating same sprite source and want to keep calcs on CPU side ] </summary>                         
        public void Draw_CPU(Rectangle sourceRect, Vector2 pos, Vector2 origin, Vector2 scale, float rot, Color c1, SpriteEffects flip = SpriteEffects.None)
        {
            #region REDUCE REDUNDANCY
            bool duplicate_uv = false;
            if (old_source1 == sourceRect) duplicate_uv = true; else old_source1 = sourceRect;
            if (origin == old_origin1) {
                if (duplicate_uv) {                                    // as long as UV is same and origin is same, we are safe to try other optimizations: 
                    if ((old_scale1 == scale) && (old_rot1 == rot)) {  // knowing that scale and rot also haven't changed, we can go to where it checks the flip for changes
                        if (old_pos1 != pos) {
                            if (originCentered) { pos.X -= o_x; pos.Y -= o_y; }
                            Vector2 dif = pos - old_pos1;
                            x1 += dif.X; x2 += dif.X; x3 += dif.X; x4 += dif.X; y1 += dif.Y; y2 += dif.Y; y3 += dif.Y; y4 += dif.Y;
                        }
                        goto LABEL_flip1;
                    }
                    else {
                        if (old_scale1 != scale) old_scale1 = scale;   // if scale has changed, we'll need to recalculate (only uv calculation can be skipped later)                     
                        else if (old_rot1 != rot) {                    // if scale is same and rot changed, we can skip scale
                            old_rot1 = rot;
                            goto LABEL_coords1;
                        }
                    }
                }
            }
            else { old_origin1 = origin; }
            #endregion

            if ((scale.X != 1) || (scale.Y != 1)) {
                w = sourceRect.Width * scale.X; h = sourceRect.Height * scale.Y;
                o_x = origin.X * scale.X; o_y = origin.Y * scale.Y;
            }
            else {   // same but no scaling
                w = sourceRect.Width; h = sourceRect.Height;
                o_x = origin.X; o_y = origin.Y;
            }
            LABEL_coords1:
            if (originCentered) { pos.X -= o_x; pos.Y -= o_y; }            
            x1 = pos.X;     y1 = pos.Y;         //upper-left
            x2 = pos.X + w; y2 = pos.Y;         //upper-right
            x3 = x2;        y3 = pos.Y + h;     //lower-right
            x4 = pos.X;     y4 = y3;            //lower-left 

            // optimization notes: keep in mind we can't skip rotation if scale had changed because the old x1,y1,etc has been changed inside here (only skip if no rotation):
            if (rot != 0f) {
                float ox = pos.X + o_x, oy = pos.Y + o_y;
                float cos = (float)Math.Cos(rot), sin = (float)Math.Sin(rot); // this is faster than it used to be
                float hd1 = x1 - ox, vd1 = y1 - oy; //top-left dif
                float hd2 = x3 - ox, vd2 = y3 - oy; //bottom-right dif
                float xhh1 = ox + hd1 * cos, xvv1 = vd1 * sin, yhh1 = oy + hd1 * sin, yvv1 = vd1 * cos;
                float xhh2 = ox + hd2 * cos, xvv2 = vd2 * sin, yhh2 = oy + hd2 * sin, yvv2 = vd2 * cos;
                x1 = xhh1 - xvv1; y1 = yhh1 + yvv1; x2 = xhh2 - xvv1; y2 = yhh2 + yvv1;
                x3 = xhh2 - xvv2; y3 = yhh2 + yvv2; x4 = xhh1 - xvv2; y4 = yhh1 + yvv2;
            }

            if (duplicate_uv) { goto LABEL_flip1; }

            u1 = sourceRect.X * normalize_u;  //gets the texture coords in terms of (0.0f-1.0f, 0.0f-1.0f)
            v1 = sourceRect.Y * normalize_v;  // " 
            u2 = (sourceRect.X + sourceRect.Width) * normalize_u;
            v2 = (sourceRect.Y + sourceRect.Height) * normalize_v;
            LABEL_flip1:

            if (duplicate_uv) if (old_spr_effect1 == flip) { goto LABEL_verts1; } else old_spr_effect1 = flip;

            if ((flip & SpriteEffects.FlipVertically) != 0) {
                var temp = v2; v2 = v1; v1 = temp;
            }
            if ((flip & SpriteEffects.FlipHorizontally) != 0) {
                var temp = u2; u2 = u1; u1 = temp;
            }
            LABEL_verts1:
            verts_reg[vert_count_reg].Position = new Vector3(x1, y1, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u1, v1);   //upper-left texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x2, y2, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u2, v1);   //upper-right texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x3, y3, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u2, v2);   //lower-right texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x4, y4, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u1, v2);   //lower-left texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            if (vert_count_reg >= MAX_VERTS) {
                End(); began = true;
            }
            old_pos1 = pos;
        }//Draw (Non-Uniform)
        #endregion




        #region D R A W  F U L L   T E X ---------------------------------------------
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawFullTex_CPU(Vector2 pos, float scale_x, float scale_y, SpriteEffects flip = SpriteEffects.None)
        {
            Draw_CPU(new Rectangle(0, 0, tex.Width, tex.Height), pos, new Vector2(scale_x, scale_y), 0f, Color.White, flip);
        }
        #endregion

        #region D R A W   C P U   (fast)  (auto-center) --------------------------------------------------------
        /// <summary> (rotations are centered) position, uniform scale, rotation, color [ CPU version for minimized redundancy if repeating sprite source ]</summary> 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Draw_CPU(Rectangle sourceRect, Vector2 pos, float scale, float rot, Color c1, SpriteEffects flip = SpriteEffects.None)
        {
            Draw_CPU(sourceRect, pos, new Vector2(scale, scale), rot, c1, flip);
        }

        Rectangle old_source2 = new Rectangle(999999, 999999, 0, 0); Vector2 old_scale2 = new Vector2(99999), old_pos2; float old_rot2; SpriteEffects old_spr_effect2 = SpriteEffects.None;
        // D R A W   C P U   (fast)
        /// <summary> (no origin - rotations happen around center) position, non-uniform scale, rotation, color [ CPU version for minimized redundancy if repeating sprite source ]</summary>        
        public void Draw_CPU(Rectangle sourceRect, Vector2 pos, Vector2 scale, float rot, Color c1, SpriteEffects flip = SpriteEffects.None)
        {
            #region REDUCE REDUNDANCY
            bool duplicate_uv = false;
            if (old_source2 == sourceRect) duplicate_uv = true; else old_source2 = sourceRect;
            if (duplicate_uv) {                                    // as long as UV is same and origin is same, we are safe to try other optimizations: 
                if ((old_scale2 == scale) && (old_rot2 == rot)) {  // knowing that scale and rot also haven't changed, we can go to where it checks the flip for changes
                    if (old_pos2 != pos) {
                        if (originCentered) { pos.X -= o_x; pos.Y -= o_y; }
                        Vector2 dif = pos - old_pos2;
                        x1 += dif.X; x2 += dif.X; x3 += dif.X; x4 += dif.X; y1 += dif.Y; y2 += dif.Y; y3 += dif.Y; y4 += dif.Y;
                    }
                    goto LABEL_flip2;
                }
                else {
                    if (old_scale2 != scale) old_scale2 = scale;   // if scale has changed, we'll need to recalculate (only uv calculation can be skipped later)                     
                    else if (old_rot2 != rot) {                    // if scale is same and rot changed, we can skip scale
                        old_rot2 = rot;
                        goto LABEL_coords2;
                    }
                }
            }             
            #endregion

            if ((scale.X != 1) || (scale.Y != 1)) {
                w = sourceRect.Width * scale.X; h = sourceRect.Height * scale.Y;
                o_x = w * 0.5f; o_y = h * 0.5f;                                  // * 0.5 __ half way is center of sprite to rotate around
            }
            else {   // same but no scaling
                w = sourceRect.Width; h = sourceRect.Height;
                o_x = w * 0.5f; o_y = h * 0.5f;
            }            
            LABEL_coords2:
            if (originCentered) { pos.X -= o_x; pos.Y -= o_y; }
            x1 = pos.X;     y1 = pos.Y;         //upper-left
            x2 = pos.X + w; y2 = pos.Y;         //upper-right
            x3 = x2;        y3 = pos.Y + h;     //lower-right
            x4 = pos.X;     y4 = y3;            //lower-left   

            // optimization notes: keep in mind we can't skip rotation if scale had changed because the old x1,y1,etc has been changed inside here (only skip if no rotation):                   
            if (rot != 0f) {
                float ox  = pos.X + o_x, oy = pos.Y + o_y;
                float cos = (float)Math.Cos(rot), sin = (float)Math.Sin(rot);
                float hd1 = x1 - ox, vd1 = y1 - oy; // top-left dif
                float hd2 = x3 - ox, vd2 = y3 - oy; // bottom-right dif
                float xhh1 = ox + hd1 * cos,   xvv1 = vd1 * sin,   yhh1 = oy + hd1 * sin,   yvv1 = vd1 * cos;
                float xhh2 = ox + hd2 * cos,   xvv2 = vd2 * sin,   yhh2 = oy + hd2 * sin,   yvv2 = vd2 * cos;
                x1 = xhh1 - xvv1;    y1 = yhh1 + yvv1;     x2 = xhh2 - xvv1;    y2 = yhh2 + yvv1;
                x3 = xhh2 - xvv2;    y3 = yhh2 + yvv2;     x4 = xhh1 - xvv2;    y4 = yhh1 + yvv2;
            }

            if (duplicate_uv) { goto LABEL_flip2; }

            u1 = sourceRect.X * normalize_u;
            v1 = sourceRect.Y * normalize_v;
            u2 = (sourceRect.X + sourceRect.Width) * normalize_u;
            v2 = (sourceRect.Y + sourceRect.Height) * normalize_v;
            LABEL_flip2:

            if (duplicate_uv) if (old_spr_effect2 == flip) { goto LABEL_verts2; } else old_spr_effect2 = flip;

            if ((flip & SpriteEffects.FlipVertically) != 0) {
                var temp = v2; v2 = v1; v1 = temp;
            }
            if ((flip & SpriteEffects.FlipHorizontally) != 0) {
                var temp = u2; u2 = u1; u1 = temp;
            }
            
            LABEL_verts2:
            verts_reg[vert_count_reg].Position = new Vector3(x1, y1, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u1, v1);  //upper-left texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x2, y2, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u2, v1);   //upper-right texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x3, y3, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u2, v2);   //lower-right texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x4, y4, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u1, v2);   //lower-left texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            if (vert_count_reg >= MAX_VERTS) {
                End(); began = true;
            }
            old_pos2 = pos;
        }
        #endregion




        #region D R A W  ( s i m p l e ) [with color] -------------------------
        // D R A W  1 Color        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Draw(Rectangle sourceRect, Vector2 pos, Color c1, SpriteEffects flip = SpriteEffects.None)
        {
            Draw(sourceRect, pos, c1, c1, c1, c1, flip);
        }
        // D R A W   4   C O L O R (basic) ------------------
        /// <summary> Fast simple 4-color sprite draw </summary>        
        public void Draw(Rectangle sourceRect, Vector2 pos, Color c1, Color c2, Color c3, Color c4, SpriteEffects flip = SpriteEffects.None)
        {
            float w, h, x1, y1, x2, y2, x3, y3, x4, y4, u1, v1, u2, v2;
            w = sourceRect.Width; h = sourceRect.Height;
                        
            x1 = pos.X;     y1 = pos.Y;     //upper-left
            x2 = pos.X + w; y2 = pos.Y;     //upper-right
            x3 = x2;        y3 = pos.Y + h; //lower-right
            x4 = pos.X;     y4 = y3;        //lower-left

            u1 =  sourceRect.X * normalize_u; 
            v1 =  sourceRect.Y * normalize_v; 
            u2 = (sourceRect.X + sourceRect.Width)  * normalize_u; 
            v2 = (sourceRect.Y + sourceRect.Height) * normalize_v; 
            if ((flip & SpriteEffects.FlipVertically) != 0) {
                var temp = v2; v2 = v1; v1 = temp;
            }
            if ((flip & SpriteEffects.FlipHorizontally) != 0) {
                var temp = u2; u2 = u1; u1 = temp;
            }
            verts_reg[vert_count_reg].Position = new Vector3(x1, y1, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u1, v1); //upper-left texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x2, y2, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u2, v1); //upper-right texCoord
            verts_reg[vert_count_reg].Color = c2; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x3, y3, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u2, v2); //lower-right texCoord
            verts_reg[vert_count_reg].Color = c3; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x4, y4, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u1, v2); //lower-left texCoord
            verts_reg[vert_count_reg].Color = c4; vert_count_reg++;
            if (vert_count_reg >= MAX_VERTS) {
                End(); began = true;
            }
        }
        #endregion



        //--------------------------------------------------
        #region D R A W  - D I S T O R T  _  C P U   F A S T
        //--------------------------------------------------
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawDistort_CPU(Rectangle sourceRect, Vector2 pos, Vector2 scale, float rot, Vector2 vv1, Vector2 vv2, Vector2 vv3, Vector2 vv4, Color c1, SpriteEffects flip = SpriteEffects.None)
        {
            DrawColorDistort_CPU(sourceRect, pos, scale, rot, vv1, vv2, vv3, vv4, c1, c1, c1, c1, flip);
        }
        // FREEDOM TO MODIFY A SPRITE BY SHAPE AND COLORS:
        //--------------------------------------------------------
        // D R A W  - C O L O R - D I S T O R T  _  C P U  F A S T
        //--------------------------------------------------------
        Rectangle old_source3 = new Rectangle(999999, 999999, 0, 0); SpriteEffects old_flip3 = SpriteEffects.None;
        float d_u1, d_v1, d_u2, d_v2;
        /// <summary> (centered rotation) allows shape distortion(good for psuedo3D, swaying trees, etc...) and multiple color blends [prunes redundant UV calculations - use regular version if UV's change a lot]</summary>           
        public void DrawColorDistort_CPU(Rectangle sourceRect, Vector2 pos, Vector2 scale, float rot, 
            Vector2 vv1, Vector2 vv2, Vector2 vv3, Vector2 vv4, Color c1, Color c2, Color c3, Color c4, SpriteEffects flip = SpriteEffects.None)
        {
            float w, h, o_x, o_y, x1, y1, x2, y2, x3, y3, x4, y4, cm;

            if ((scale.X != 1f) || (scale.Y != 1f)) {
                w = sourceRect.Width * scale.X; h = sourceRect.Height * scale.Y;
                o_x = w * 0.5f; o_y = h * 0.5f;
                //get scaled offsets:
                float xo1 = vv1.X * scale.X, yo1 = vv1.Y * scale.Y; float xo2 = vv2.X * scale.X, yo2 = vv2.Y * scale.Y;
                float xo3 = vv3.X * scale.X, yo3 = vv3.Y * scale.Y; float xo4 = vv4.X * scale.X, yo4 = vv4.Y * scale.Y;
                //what is the point after offset
                x1 = pos.X + xo1;                 y1 = pos.Y + yo1;  //upper-left
                cm = pos.X + w;   x2 = cm + xo2;  y2 = pos.Y + yo2;  //upper-right
                x3 = cm + xo3;    cm = pos.Y + h; y3 = cm + yo3;     //lower-right
                x4 = pos.X + xo4;                 y4 = cm + yo4;     //lower-left                
            }
            else { //same with no scaling:            
                w = sourceRect.Width; h = sourceRect.Height;   //if (origin.HasValue) { o_x = origin.Value.X; o_y = origin.Value.Y; } else { o_x = w * 0.5f; o_y = h * 0.5f; } //Note: w and h are already scaled sizes                        
                o_x = w * 0.5f; o_y = h * 0.5f;
                //need positions of destination                
                x1 = pos.X + vv1.X;                  y1 = pos.Y + vv1.Y;  //upper-left
                cm = pos.X + w;     x2 = cm + vv2.X; y2 = pos.Y + vv2.Y;  //upper-right
                x3 = cm + vv3.X;    cm = pos.Y + h;  y3 = cm + vv3.Y;     //lower-right
                x4 = pos.X + vv4.X;                  y4 = cm + vv4.Y;     //lower-left
            }
            if (rot != 0f) {
                float ox   = pos.X + o_x, oy = pos.Y + o_y;
                float cos  = (float)Math.Cos(rot),  sin = (float)Math.Sin(rot); //this is actually quite fast on a modern computer                
                float hd1  = x1 - ox, vd1 = y1 - oy;      // top-left dif
                float hd2  = x3 - ox, vd2 = y3 - oy;      // bottom-right dif
                float xhh1 = ox + hd1 * cos,   xvv1 = vd1 * sin,   yhh1 = oy + hd1 * sin,   yvv1 = vd1 * cos; 
                float xhh2 = ox + hd2 * cos,   xvv2 = vd2 * sin,   yhh2 = oy + hd2 * sin,   yvv2 = vd2 * cos;
                x1 = xhh1 - xvv1;    y1 = yhh1 + yvv1;     x2 = xhh2 - xvv1;    y2 = yhh2 + yvv1;
                x3 = xhh2 - xvv2;    y3 = yhh2 + yvv2;     x4 = xhh1 - xvv2;    y4 = yhh1 + yvv2;
            }
            if (sourceRect == old_source3) goto LABEL_flip3; else old_source3 = sourceRect;
            d_u1 = sourceRect.X * normalize_u;  //gets the texture coords in terms of (0.0f-1.0f, 0.0f-1.0f)
            d_v1 = sourceRect.Y * normalize_v; 
            d_u2 = (sourceRect.X + sourceRect.Width)  * normalize_u;  
            d_v2 = (sourceRect.Y + sourceRect.Height) * normalize_v;
            LABEL_flip3:
            if (old_flip3 == flip) goto LABEL_verts3; else old_flip3 = flip;
            if ((flip & SpriteEffects.FlipVertically) != 0) {
                var temp = d_v2; d_v2 = d_v1; d_v1 = temp;
            }
            if ((flip & SpriteEffects.FlipHorizontally) != 0) {
                var temp = d_u2; d_u2 = d_u1; d_u1 = temp;
            }
            LABEL_verts3:
            verts_reg[vert_count_reg].Position = new Vector3(x1, y1, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(d_u1, d_v1);   //upper-left texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x2, y2, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(d_u2, d_v1);   //upper-right texCoord
            verts_reg[vert_count_reg].Color = c2; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x3, y3, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(d_u2, d_v2);   //lower-right texCoord
            verts_reg[vert_count_reg].Color = c3; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x4, y4, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(d_u1, d_v2);   //lower-left texCoord
            verts_reg[vert_count_reg].Color = c4; vert_count_reg++;
            if (vert_count_reg >= MAX_VERTS) {
                End(); began = true;
            }
        }//Draw (Color-Distort)
        #endregion



        //-----------------------
        #region D R A W _ D E S T 
        //-----------------------
        // DRAW DEST 1 color:
        /// <summary> Stretch a source image to fit into a destination rectangle </summary>        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawDest(Rectangle sourceRect, Rectangle destRect, Color c1, SpriteEffects flip = SpriteEffects.None)
        {
            DrawDest(sourceRect, destRect, c1, c1, c1, c1, flip);
        }
        // DRAW DEST 4 colors:
        /// <summary> Stretch a source image to fit into a destination rectangle (allows 4 color blending) </summary>        
        public void DrawDest(Rectangle sourceRect, Rectangle destRect, Color c1, Color c2, Color c3, Color c4, SpriteEffects flip = SpriteEffects.None)
        {
            float x1, y1, x2, y2, u1, v1, u2, v2; // generate a TARGET WIDTH, HEIGHT based on the source                        

            //need positions of destination            
            x1 = destRect.X;                   y1 = destRect.Y;            
            x2 = x1 + destRect.Width;          y2 = y1 + destRect.Height;  
            u1 = sourceRect.X * normalize_u;  //gets the texture coords in terms of (0.0f-1.0f, 0.0f-1.0f)
            v1 = sourceRect.Y * normalize_v; 
            u2 = (sourceRect.X + sourceRect.Width)  * normalize_u;  
            v2 = (sourceRect.Y + sourceRect.Height) * normalize_v;
            if ((flip & SpriteEffects.FlipVertically) != 0) {
                var temp = v2; v2 = v1; v1 = temp;
            }
            if ((flip & SpriteEffects.FlipHorizontally) != 0) {
                var temp = u2; u2 = u1; u1 = temp;
            }
            verts_reg[vert_count_reg].Position = new Vector3(x1, y1, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u1, v1);   //upper-left texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x2, y1, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u2, v1);   //upper-right texCoord
            verts_reg[vert_count_reg].Color = c2; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x2, y2, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u2, v2);   //lower-right texCoord
            verts_reg[vert_count_reg].Color = c3; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x1, y2, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u1, v2);   //lower-left texCoord
            verts_reg[vert_count_reg].Color = c4; vert_count_reg++;
            if (vert_count_reg >= MAX_VERTS) {
                End(); began = true;
            }
        }//Draw (Draw Dest 4 color)



        // D R A W   D E S T  (full texture backgrounds) 
        ///<summary> FULL TEXTURE DRAWN - ( Good for backgrounds ) </summary> 
        public void DrawDest(Rectangle destRect, Color c1)
        {
            float x1, y1, x2, y2;
            // need positions of destination: 
            x1 = destRect.X;          y1 = destRect.Y;
            x2 = x1 + destRect.Width; y2 = y1+destRect.Height;             
            verts_reg[vert_count_reg].Position = new Vector3(x1, y1, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(0, 0); //upper-left texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x2, y1, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(1, 0); //upper-right texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x2, y2, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(1, 1); //lower-right texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(x1, y2, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(0, 1); //lower-left texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            if (vert_count_reg >= MAX_VERTS) {
                End(); began = true;
            }
        }//DrawDest simplest
        #endregion



        #region 3 D   R o t a t i o n  F o r   2 D 
        // XYZ ROTATION  D R A W  ------------------------------------------------------------------------------------------------
        /// <summary> (xyz rotates around center) position, non-uniform scale, x rotation, y rotation, z rotation, color </summary>        
        public void DrawRotXYZ(Rectangle sourceRect, Vector2 pos, Vector2 scale, float rotx, float roty, float rotz, Color c1, float? depth, SpriteEffects flip = SpriteEffects.None)
        {
            float w, h, o_x, o_y, u1, v1, u2, v2;
            Vector3 ul, ur, br, bl;

            if ((scale.X != 1) || (scale.Y != 1)) {
                w = sourceRect.Width * scale.X; h = sourceRect.Height * scale.Y;                
            }
            else {   // same but no scaling
                w = sourceRect.Width; h = sourceRect.Height;                
            }
            o_x = w * 0.5f; o_y = h * 0.5f;                  // 0.5 allows to rotate around its center

            if (depth.HasValue) z = depth.Value;
            ul.X = pos.X;     ul.Y = pos.Y;      ul.Z = z;   //upper-left
            ur.X = pos.X + w; ur.Y = pos.Y;      ur.Z = z;   //upper-right
            br.X = pos.X + w; br.Y = pos.Y + h;  br.Z = z;   //lower-right
            bl.X = pos.X;     bl.Y = pos.Y + h;  bl.Z = z;   //lower-left   
            
            // get local coordinates (center around origin for rotation):
            Vector3 mid = new Vector3(pos.X + o_x, pos.Y + o_y, z);
            ul -= mid; ur -= mid;
            bl -= mid; br -= mid;

            // create x,y,z rotation matrix, rotate, and put back to original position(mid):
            Matrix rot = Matrix.CreateFromYawPitchRoll(roty, rotx, rotz);
            ul = mid+Vector3.Transform(ul, rot);
            ur = mid+Vector3.Transform(ur, rot);
            br = mid+Vector3.Transform(br, rot);
            bl = mid+Vector3.Transform(bl, rot);
            
            u1 = sourceRect.X * normalize_u;
            v1 = sourceRect.Y * normalize_v;
            u2 = (sourceRect.X + sourceRect.Width)  * normalize_u;
            v2 = (sourceRect.Y + sourceRect.Height) * normalize_v;
            if ((flip & SpriteEffects.FlipVertically) != 0) {
                var temp = v2; v2 = v1; v1 = temp;
            }
            if ((flip & SpriteEffects.FlipHorizontally) != 0) {
                var temp = u2; u2 = u1; u1 = temp;
            }
            verts_reg[vert_count_reg].Position = ul; verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u1, v1);  //upper-left texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = ur; verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u2, v1);   //upper-right texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = br; verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u2, v2);   //lower-right texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = bl; verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u1, v2);   //lower-left texCoord
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            if (vert_count_reg >= MAX_VERTS) {
                End(); began = true;
            }
        }
        #endregion



        #region D R A W    L I N E S    A N D    R E C T A N G L E S ------------------------------------------------------------        
        private Rectangle pixel = new Rectangle(0, 0, 1, 1);
        public  Rectangle PIXEL { get { return pixel; } set { pixel = value; pixel.Width = pixel.Height = 1; } }

        // D R A W   L I N E  
        /// <summary> draw a line from 2 vecs - PIXEL should be set as a rectangle source of a white dot somewhere in sprite-map </summary>
        public void DrawLine(Vector2 start, Vector2 end, Color color, float thickness = 2f)
        {
            Vector2 delta = end - start;
            float rot = (float)Math.Atan2(delta.Y, delta.X);
            pixel.Width = pixel.Height = 1;
            Draw_CPU(pixel, start, new Vector2(0, 0.5f), new Vector2(delta.Length(), thickness), rot, color);
        }

        /// <summary> Draw a line - make sure PIXEL has already been set to a white dot somewhere in your sprite-map source (unless not using a texture in this batch) </summary>
        public void Line(float x1, float y1, float x2, float y2, Color color, float thickness = 1f)
        {
            Vector2 end   = new Vector2(x2, y2);
            Vector2 start = new Vector2(x1, y1);
            Vector2 delta = end - start;
            pixel.Width = pixel.Height = 1;
            float rot = (float)Math.Atan2(delta.Y, delta.X);
            Draw_CPU(pixel, start, Vector2.Zero, new Vector2(delta.Length(), thickness), rot, color);
        }

        
        /// <summary> Quickly draws a rectangle using lines </summary>        
        public void RectLines(float x1, float y1, float x2, float y2, Color color, float thickness = 1f)
        {
            pixel.Width = pixel.Height = 1;
            Draw(pixel, new Vector2(x1, y1), Vector2.Zero, new Vector2((x2 - x1), thickness), 0f, color); // top    horizontal line 
            Draw(pixel, new Vector2(x2, y1), Vector2.Zero, new Vector2(thickness, (y2 - y1)), 0f, color); // right  vertical line 2
            Draw(pixel, new Vector2(x1, y2), Vector2.Zero, new Vector2((x2 - x1), thickness), 0f, color); // bottom horizontal line
            Draw(pixel, new Vector2(x1, y1), Vector2.Zero, new Vector2(thickness, (y2 - y1)), 0f, color); // left   vertical line
        }
        public void RectLines(Rectangle rec, Color colr, float thick = 1f) {
            RectLines(rec.X, rec.Y, rec.X + rec.Width, rec.Y + rec.Height, colr, thick);
        }
        public void RectLines(Rectangle rec, Vector2 pos_off, Color colr, float thick = 1f) {
            RectLines(rec.X + pos_off.X, rec.Y + pos_off.Y, rec.X + pos_off.X + rec.Width, rec.Y + pos_off.Y + rec.Height, colr, thick);
        }


        // DRAW COLOR FILLED QUAD (with color blending)
        /// <summary> Draw a color filled quad of any shape and allow 4 color blends (great for psuedo-3d racing games) [make sure to set PIXEL first] </summary>        
        public void DrawColorQuad(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, Color c1, Color c2, Color c3, Color c4)
        {         
            float u1, v1, u2, v2;
            u1 = pixel.X * normalize_u;
            v1 = pixel.Y * normalize_v;
            u2 = (pixel.X + pixel.Width)  * normalize_u;
            v2 = (pixel.Y + pixel.Height) * normalize_v;
            verts_reg[vert_count_reg].Position = new Vector3(p1, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u1, v1); // upper-left
            verts_reg[vert_count_reg].Color = c1; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(p2, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u2, v1); // upper-right
            verts_reg[vert_count_reg].Color = c2; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(p3, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u2, v2); // lower-right
            verts_reg[vert_count_reg].Color = c3; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(p4, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u1, v2); // lower-left
            verts_reg[vert_count_reg].Color = c4; vert_count_reg++;
            if (vert_count_reg >= MAX_VERTS) {
                End(); began = true;
            }
        }


        // DRAW A COLOR FILLED RECTANGLE
        /// <summary> Draw a filled rect (make sure PIXEL has been set) </summary> 
        public void FillRect(Rectangle Pixel, Rectangle r, Color col)
        {            
            float u1, v1, u2, v2;
            u1 = Pixel.X * normalize_u;  
            v1 = Pixel.Y * normalize_v; 
            u2 = (Pixel.X + Pixel.Width)  * normalize_u; 
            v2 = (Pixel.Y + Pixel.Height) * normalize_v; 
            Vector2 p1, p2, p3, p4;
            p1.X = r.X; p1.Y = r.Y; p2 = p1; p2.X += r.Width; p3 = p2; p3.Y += r.Height; p4 = p1; p4.Y += r.Height;
            verts_reg[vert_count_reg].Position = new Vector3(p1, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u1, v1); // upper-left
            verts_reg[vert_count_reg].Color = col; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(p2, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u2, v1); // upper-right
            verts_reg[vert_count_reg].Color = col; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(p3, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u2, v2); // lower-right
            verts_reg[vert_count_reg].Color = col; vert_count_reg++;
            verts_reg[vert_count_reg].Position = new Vector3(p4, z); verts_reg[vert_count_reg].TextureCoordinate = new Vector2(u1, v2); // lower-left
            verts_reg[vert_count_reg].Color = col; vert_count_reg++;
            if (vert_count_reg >= MAX_VERTS) {
                End(); began = true;
            }
        }
        #endregion

        #endregion // end of CPU Prepared Draws




        //--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        #region D R A W   [ G P U  versions ]
        //--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        #region D R A W   T R A N S F O R M E D   V E R T I C E S  (GPU & character rot/scale)
        //**************** ! ! ! NOT TESTED ! ! ! ***************  ( It should work XD )
        /// <summary> Draw a sprite-quad from 4 verts_reg (offsets from a position) [ useful for MeoMotion / MeoPlayer ] </summary>                
        public void DrawTransformedVerts(Rectangle sourceRect, Vector2 position, Vector2 character_root_origin, Vector2 character_scale, float character_rot, 
            Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, Color c1, SpriteEffects flip = SpriteEffects.None)
        {
            float u1, v1, u2, v2;

            p1 += position; p2 += position; p3 += position; p4 += position;
            u1 = sourceRect.X * normalize_u;  // gets the texture coords in terms of (0.0f-1.0f, 0.0f-1.0f)
            v1 = sourceRect.Y * normalize_v;  // " 
            u2 = (sourceRect.X + sourceRect.Width)  * normalize_u;
            v2 = (sourceRect.Y + sourceRect.Height) * normalize_v;
            if ((flip & SpriteEffects.FlipVertically) != 0) {
                var temp = v2; v2 = v1; v1 = temp;
            }
            if ((flip & SpriteEffects.FlipHorizontally) != 0) {
                var temp = u2; u2 = u1; u1 = temp;
            }
            verts_spr[vert_count_spr].position     = new Vector3(p1.X, p1.Y, z);
            verts_spr[vert_count_spr].origin_rot   = new Vector3(character_root_origin.X, character_root_origin.Y, character_rot);
            verts_spr[vert_count_spr].texCoord_scale = new Vector4(u1, v1, character_scale.X, character_scale.Y); // upper-left texCoord            
            verts_spr[vert_count_spr].color = c1;    vert_count_spr++;
            verts_spr[vert_count_spr].position     = new Vector3(p2.X, p2.Y, z);
            verts_spr[vert_count_spr].origin_rot   = new Vector3(character_root_origin.X, character_root_origin.Y, character_rot);
            verts_spr[vert_count_spr].texCoord_scale = new Vector4(u2, v1, character_scale.X, character_scale.Y); // upper-left texCoord            
            verts_spr[vert_count_spr].color = c1;    vert_count_spr++;
            verts_spr[vert_count_spr].position     = new Vector3(p3.X, p3.Y, z);
            verts_spr[vert_count_spr].origin_rot   = new Vector3(character_root_origin.X, character_root_origin.Y, character_rot);
            verts_spr[vert_count_spr].texCoord_scale = new Vector4(u2, v2, character_scale.X, character_scale.Y); // upper-left texCoord            
            verts_spr[vert_count_spr].color = c1;    vert_count_spr++;
            verts_spr[vert_count_spr].position     = new Vector3(p4.X, p4.Y, z);
            verts_spr[vert_count_spr].origin_rot   = new Vector3(character_root_origin.X, character_root_origin.Y, character_rot);
            verts_spr[vert_count_spr].texCoord_scale = new Vector4(u1, v2, character_scale.X, character_scale.Y); // upper-left texCoord            
            verts_spr[vert_count_spr].color = c1;    vert_count_spr++;
            if (vert_count_spr >= MAX_VERTS) {
                End(); began = true;
            }
        }
        #endregion

        


        #region D R A W   [ G P U ]  (origin based) ------------------------------------------
        /// <summary> 1 color & uniform scale [ for drawing from same texture-coords and want to keep calcs on CPU side ] </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Draw(Rectangle sourceRect, Vector2 pos, Vector2 origin, float scale, float rot, Color c1, SpriteEffects flip = SpriteEffects.None)
        {
            Draw(sourceRect, pos, origin, new Vector2(scale, scale), rot, c1, flip);
        }

        //static float w, h, o_x, o_y, x1, y1, x2, y2, x3, y3, x4, y4, u1, v1, u2, v2; // these can be shared with negligable performance difference    
        Rectangle old_source4 = new Rectangle(999999, 999999, 0, 0); SpriteEffects old_flip4 = SpriteEffects.None;
        // D R A W -------------------------------------------
        /// <summary> Non-Uniform scale( 1 color ) [ used when repeating same sprite source and want to keep calcs on CPU side ] </summary>                         
        public void Draw(Rectangle sourceRect, Vector2 pos, Vector2 origin, Vector2 scale, float rot, Color c1, SpriteEffects flip = SpriteEffects.None)
        {            
            bool duplicate_uv = false;
            if (old_source4 == sourceRect) duplicate_uv = true; else old_source4 = sourceRect;
                        
            if (originCentered) { pos.X -= origin.X; pos.Y -= origin.Y; }
            float x1 = pos.X, x2 = x1 + sourceRect.Width, y1 = pos.Y, y2 = y1 + sourceRect.Height;
            
            if (duplicate_uv) { goto LABEL_flip4; }
            u1 = sourceRect.X * normalize_u;  //gets the texture coords in terms of (0.0f-1.0f, 0.0f-1.0f)
            v1 = sourceRect.Y * normalize_v;  // " 
            u2 = (sourceRect.X + sourceRect.Width)  * normalize_u;
            v2 = (sourceRect.Y + sourceRect.Height) * normalize_v;
            LABEL_flip4:

            if (duplicate_uv) if (old_flip4 == flip) { goto LABEL_verts4; } else old_flip4 = flip;
            if ((flip & SpriteEffects.FlipVertically) != 0) {
                var temp = v2; v2 = v1; v1 = temp;
            }
            if ((flip & SpriteEffects.FlipHorizontally) != 0) {
                var temp = u2; u2 = u1; u1 = temp;
            }
            LABEL_verts4:
            float ox = x1 + origin.X, oy = y1 + origin.Y;
            verts_spr[vert_count_spr].position     = new Vector3(x1, y1, z);
            verts_spr[vert_count_spr].origin_rot   = new Vector3(ox, oy, rot);
            verts_spr[vert_count_spr].texCoord_scale = new Vector4(u1, v1, scale.X, scale.Y);
            verts_spr[vert_count_spr].color        = c1;         vert_count_spr++;
            verts_spr[vert_count_spr].position     = new Vector3(x2, y1, z);
            verts_spr[vert_count_spr].origin_rot   = new Vector3(ox, oy, rot);
            verts_spr[vert_count_spr].texCoord_scale = new Vector4(u2, v1, scale.X, scale.Y);
            verts_spr[vert_count_spr].color        = c1;         vert_count_spr++;
            verts_spr[vert_count_spr].position     = new Vector3(x2, y2, z);
            verts_spr[vert_count_spr].origin_rot   = new Vector3(ox, oy, rot);
            verts_spr[vert_count_spr].texCoord_scale = new Vector4(u2, v2, scale.X, scale.Y);
            verts_spr[vert_count_spr].color        = c1;         vert_count_spr++;
            verts_spr[vert_count_spr].position     = new Vector3(x1, y2, z);
            verts_spr[vert_count_spr].origin_rot   = new Vector3(ox, oy, rot);
            verts_spr[vert_count_spr].texCoord_scale = new Vector4(u1, v2, scale.X, scale.Y);
            verts_spr[vert_count_spr].color        = c1;         vert_count_spr++;
            if (vert_count_spr >= MAX_VERTS) {
                End(); began = true;
            }
        }//Draw (Non-Uniform)
        #endregion



        #region D R A W  F U L L   T E X ---------------------------------------------
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawFullTex(Vector2 pos, float scale_x, float scale_y, SpriteEffects flip = SpriteEffects.None)
        {
            Draw(new Rectangle(0, 0, tex.Width, tex.Height), pos, new Vector2(scale_x, scale_y), 0f, Color.White, flip);
        }
        #endregion

      
        #region D R A W  [ G P U ]  (auto-center) --------------------------------------------------------
        /// <summary> (rotations are centered) position, uniform scale, rotation, color [ CPU version for minimized redundancy if repeating sprite source ]</summary> 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Draw(Rectangle sourceRect, Vector2 pos, float scale, float rot, Color c1, SpriteEffects flip = SpriteEffects.None)
        {
            Draw_CPU(sourceRect, pos, new Vector2(scale, scale), rot, c1, flip);
        }

        Rectangle old_source5 = new Rectangle(999999, 999999, 0, 0); SpriteEffects old_flip5 = SpriteEffects.None;
        // D R A W 
        /// <summary> (no origin - rotations happen around center) position, non-uniform scale, rotation, color [ CPU version for minimized redundancy if repeating sprite source ]</summary>        
        public void Draw(Rectangle sourceRect, Vector2 pos, Vector2 scale, float rot, Color c1, SpriteEffects flip = SpriteEffects.None)
        {            
            bool duplicate_uv = false;
            if (old_source5 == sourceRect) duplicate_uv = true; else old_source5 = sourceRect;

            float orx = sourceRect.Width / 2, ory = sourceRect.Height / 2;
            if (originCentered) { pos.X -= orx; pos.Y -= ory; }
            float x1 = pos.X, x2 = x1 + sourceRect.Width, y1 = pos.Y, y2 = y1 + sourceRect.Height;

            if (duplicate_uv) { goto LABEL_flip5; }
            u1 = sourceRect.X * normalize_u;  //gets the texture coords in terms of (0.0f-1.0f, 0.0f-1.0f)
            v1 = sourceRect.Y * normalize_v;  // " 
            u2 = (sourceRect.X + sourceRect.Width) * normalize_u;
            v2 = (sourceRect.Y + sourceRect.Height) * normalize_v;
            LABEL_flip5:

            if (duplicate_uv) if (old_flip5 == flip) { goto LABEL_verts5; } else old_flip5 = flip;
            if ((flip & SpriteEffects.FlipVertically) != 0) {
                var temp = v2; v2 = v1; v1 = temp;
            }
            if ((flip & SpriteEffects.FlipHorizontally) != 0) {
                var temp = u2; u2 = u1; u1 = temp;
            }
            LABEL_verts5:
            float ox = x1 + orx, oy = y1 + ory;
            verts_spr[vert_count_spr].position     = new Vector3(x1, y1, z);
            verts_spr[vert_count_spr].origin_rot   = new Vector3(orx, ory, rot);
            verts_spr[vert_count_spr].texCoord_scale = new Vector4(u1, v1, scale.X, scale.Y);
            verts_spr[vert_count_spr].color        = c1; vert_count_spr++;
            verts_spr[vert_count_spr].position     = new Vector3(x2, y1, z);
            verts_spr[vert_count_spr].origin_rot   = new Vector3(orx, ory, rot);
            verts_spr[vert_count_spr].texCoord_scale = new Vector4(u2, v1, scale.X, scale.Y);
            verts_spr[vert_count_spr].color        = c1;                  vert_count_spr++;
            verts_spr[vert_count_spr].position     = new Vector3(x2, y2, z);
            verts_spr[vert_count_spr].origin_rot   = new Vector3(orx, ory, rot);
            verts_spr[vert_count_spr].texCoord_scale = new Vector4(u2, v2, scale.X, scale.Y);
            verts_spr[vert_count_spr].color        = c1;                  vert_count_spr++;
            verts_spr[vert_count_spr].position     = new Vector3(x1, y2, z);
            verts_spr[vert_count_spr].origin_rot   = new Vector3(orx, ory, rot);
            verts_spr[vert_count_spr].texCoord_scale = new Vector4(u1, v2, scale.X, scale.Y);
            verts_spr[vert_count_spr].color        = c1;                  vert_count_spr++;
            if (vert_count_spr >= MAX_VERTS) {
                End(); began = true;
            }
        }
        #endregion

        #endregion



        void Say(string s) { Console.WriteLine(s); }
    }
}

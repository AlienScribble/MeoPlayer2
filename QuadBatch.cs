using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Runtime.CompilerServices;

namespace MeoMo {
    public class QuadBatch {
        GraphicsDevice gpu;
        bool began;

        // QUAD DATA
        VertexPositionColorTexture[] vertices;
        short[]      indices;
        VertexBuffer vertexBuffer;
        IndexBuffer  indexBuffer;
        int          vert_count;

        // EFFECTS & TEXTURES
        Effect    default_fx, fx;
        Texture2D tex, old_tex;

        // COORDINATE CONTROLS
        Matrix world, view, proj, ViewProj, WorldViewProj;
        float normalize_u,  normalize_v;       // scale tex coords:

        // DEVICE STATE CHANGES
        BlendState        blendState, old_blend;
        SamplerState      samplerState;
        DepthStencilState depthStencilState;  // depth - default = off
        int  screenWidth, screenHeight;

        // PUBLIC ACCESS:
        public float z;                       // current drawing depth (change as needed while drawing)
        public bool  keep_fx = false;         // if you pass an Effect to begin, setting this to true allows you to keep effect applied to next Begin-End



        // C O N S T R U C T

        public QuadBatch(ContentManager Content, GraphicsDevice GPU, bool use3DCamera = false)
        {
            default_fx = Content.Load<Effect>("QuadEffect");
            gpu = GPU;
            screenWidth  = gpu.Viewport.Width;
            screenHeight = gpu.Viewport.Height;
            vertices = new VertexPositionColorTexture[8192]; // up to 2048 quads per batch
            indices  = new short[12288];                     // "                        "
            int c = 0;
            for (int i = 0; i < 8192; i += 4) {
                indices[c] = (short)(i + 0); c++; indices[c] = (short)(i + 1); c++; indices[c] = (short)(i + 2); c++;
                indices[c] = (short)(i + 2); c++; indices[c] = (short)(i + 3); c++; indices[c] = (short)(i + 0); c++;
            }
            vertexBuffer = new DynamicVertexBuffer(gpu, typeof(VertexPositionColorTexture), 8192, BufferUsage.WriteOnly);
            indexBuffer  = new IndexBuffer(gpu, typeof(short), 12288, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);

            world = Matrix.Identity;
            if (use3DCamera) Set3DCamera(new Vector3(screenWidth / 2, screenHeight / 2, -900f), 1f);
            else Set2DCamera();

            blendState          = BlendState.NonPremultiplied;
            samplerState        = SamplerState.LinearClamp;
            depthStencilState   = DepthStencilState.None;
            gpu.RasterizerState = RasterizerState.CullNone;            
        }




        // C A M E R A S -------------------------------------------------------------------------------------------------------------------------------

        // S E T   3 D   C A M E R A 
        public void Set3DCamera(Vector3 eye_pos, float target_z)
        {
            Set3DCamera(eye_pos, new Vector3(eye_pos.X, eye_pos.Y, target_z));
        }
        public void Set3DCamera(Vector3 eye_pos, Vector3 targ_pos)
        {
            view = Matrix.CreateLookAt(eye_pos, targ_pos, Vector3.Down);
            proj = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, gpu.Viewport.AspectRatio, 0.1f, 2000f);
            Matrix halfPixel = Matrix.CreateTranslation(-0.5f, -0.5f, 0); proj = halfPixel * proj; // maybe not necessary 
            ViewProj      = view * proj;
            WorldViewProj = world * ViewProj;
            z = 20f;
        }


        // S E T   2 D   C A M E R A 
        public void Set2DCamera()
        {
            view = Matrix.Identity;
            proj = Matrix.CreateOrthographicOffCenter(0.0f, screenWidth, screenHeight, 0.0f, -2000f, 2000f);
            Matrix halfPixel = Matrix.CreateTranslation(-0.5f, -0.5f, 0); proj = halfPixel * proj;  // <-- fixes half-pixel offset problem            
            ViewProj = view * proj; WorldViewProj = world * ViewProj;
            z = 0.5f;
        }

        // SET WORLD
        public void SetWorld(Matrix World) { world = World; WorldViewProj = world * ViewProj; }




        //B E G I N --------------------------------------------------------------------------------------------------------------------------------------       

        bool ResetBegin()
        {
            if (began) { Console.WriteLine("Quad Begin already called."); return false; }
            vert_count = 0;
            began      = true;
            if (!keep_fx) fx = null;
            return true;
        }
        void TexCoordSetup()
        {
            normalize_u = 1.0f / (float)tex.Width;
            normalize_v = 1.0f / (float)tex.Height;
        }
        // BEGIN ------------------------------------------------------------------------
        public void Begin(Texture2D texture)
        {
            if (!ResetBegin()) return;
            tex = texture;
            TexCoordSetup();
        }
        public void Begin(BlendState blend_State)
        {
            if (!ResetBegin()) return;
            blendState = blend_State;
        }
        public void Begin(Texture2D texture, BlendState blend_State)
        {
            if (!ResetBegin()) return;
            tex = texture; blendState = blend_State;
            TexCoordSetup();
        }
        public void Begin(Texture2D texture, BlendState blend_State, SamplerState sampler_State)
        {
            if (!ResetBegin()) return;
            tex = texture; blendState = blend_State;
            samplerState = sampler_State;
            TexCoordSetup();
        }
        public void Begin(Texture2D texture, BlendState blend_State, SamplerState sampler_State, DepthStencilState depth_StencilState)
        {
            if (!ResetBegin()) return;
            tex = texture; blendState = blend_State;
            samplerState = sampler_State; depthStencilState = depth_StencilState;
            TexCoordSetup();
        }
        public void Begin(Texture2D texture, BlendState blend_state, SamplerState sampler_state, DepthStencilState depth_StencilState, Matrix World, Effect myEffect = null)
        {
            if (!ResetBegin()) return;
            tex = texture; blendState = blend_state;
            samplerState = sampler_state; depthStencilState = depth_StencilState;
            world = World; WorldViewProj = world * ViewProj;
            if (myEffect != null) fx = myEffect;
            TexCoordSetup();
        }


        public void SwitchTex(Texture2D newTex) { old_tex = tex; old_blend = blendState; End(); Begin(newTex); }
        public void SwitchTex(Texture2D newTex, BlendState newBlend) { old_tex = tex; old_blend = blendState; End(); Begin(newTex, newBlend); }
        public void RestoreTex() { End(); tex = old_tex; Begin(old_tex); blendState = old_blend; }



         // E N D ---------------------------------------------------------------------------------------------------------------------------------------------------------

        public void End()
        {
            if (!began) { Console.WriteLine("End without Begin call in Quad"); return; }

            if (vert_count >= 3) {

                // set devices states
                gpu.BlendState        = blendState;
                gpu.DepthStencilState = depthStencilState;
                gpu.SamplerStates[0]  = samplerState;
                if (gpu.RasterizerState.CullMode != CullMode.None) gpu.RasterizerState = RasterizerState.CullNone;

                // set fx                                
                default_fx.CurrentTechnique.Passes[0].Apply();                
                if (fx  != null) fx.CurrentTechnique.Passes[0].Apply();
                if (tex != null) {
                       gpu.Textures[0] = tex;
                       default_fx.Parameters["UseTexture"].SetValue(1);
                } else default_fx.Parameters["UseTexture"].SetValue(0);
                default_fx.Parameters["MatrixTransform"].SetValue(WorldViewProj);

                int triangle_count = vert_count / 2;
                gpu.DrawUserIndexedPrimitives<VertexPositionColorTexture>(PrimitiveType.TriangleList, vertices, 0, vert_count, indices, 0, triangle_count);
            }
            began = false;
            vert_count = 0;
        }




        // D R A W ------------------------------------------------------------------------------------------------------------------------------------------------------

        // D R A W   T R A N S F O R M E D   V E R T S   (vertices)
        /// <summary> Draw a sprite-quad from 4 vertices (offsets from a position) that do not need to be scaled or rotated (usedful with meomotion) </summary>        
        public void DrawTransformedVerts(Rectangle sourceRect, Vector2 position, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, Color c1, SpriteEffects flip = SpriteEffects.None)
        {
            if (!began) { Console.WriteLine("BEGIN not called before Quad Draw"); return; }
            float u1, v1, u2, v2;

            p1 = (p1 + position); p2 = (p2 + position); p3 = (p3 + position); p4 = (p4 + position);
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
            vertices[vert_count].Position = new Vector3(p1, z);
            vertices[vert_count].TextureCoordinate = new Vector2(u1, v1); // upper-left texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = new Vector3(p2, z);
            vertices[vert_count].TextureCoordinate = new Vector2(u2, v1); // upper-right texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = new Vector3(p3, z);
            vertices[vert_count].TextureCoordinate = new Vector2(u2, v2); // lower-right texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = new Vector3(p4, z);
            vertices[vert_count].TextureCoordinate = new Vector2(u1, v2); // lower-left texCoord
            vertices[vert_count].Color = c1; vert_count++;
            if (vert_count >= 8187) {
                End(); began = true;
            }
        }




        // D R A W -------------------------------------------
        /// <summary> 1 color & uniform scale </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Draw(Rectangle sourceRect, Vector2 pos, Vector2 origin, float scale, float rot, Color c1, SpriteEffects flip = SpriteEffects.None)
        {
            Draw(sourceRect, pos, origin, new Vector2(scale, scale), rot, c1, flip);
        }
        // D R A W -------------------------------------------
        /// <summary> Non-Uniform scale ( 1 color ) </summary>        
        public void Draw(Rectangle sourceRect, Vector2 pos, Vector2 origin, Vector2 scale, float rot, Color c1, SpriteEffects flip = SpriteEffects.None)
        {
            if (!began) { Console.WriteLine("BEGIN not called before Quad Draw"); return; }
            float w, h, o_x, o_y, x1, y1, x2, y2, x3, y3, x4, y4, u1, v1, u2, v2;

            if ((scale.X != 1) || (scale.Y != 1)) {
                w = sourceRect.Width * scale.X; h = sourceRect.Height * scale.Y;
                o_x = origin.X * scale.X; o_y = origin.Y * scale.Y;
            }
            else {   // same but no scaling
                w = sourceRect.Width; h = sourceRect.Height;
                o_x = origin.X; o_y = origin.Y;
            }

            x1 = pos.X;     y1 = pos.Y;         //upper-left
            x2 = pos.X + w; y2 = pos.Y;         //upper-right
            x3 = pos.X + w; y3 = pos.Y + h;     //lower-right
            x4 = pos.X;     y4 = pos.Y + h;     //lower-left                
            if (rot != 0f) {
                float ox = pos.X + o_x, oy = pos.Y + o_y;
                float cos = (float)Math.Cos(rot), sin = (float)Math.Sin(rot); // this is faster than it used to be
                float hd1 = x1 - ox, vd1 = y1 - oy; //top-left dif
                float hd2 = x3 - ox, vd2 = y3 - oy; //bottom-right dif
                x1 = ox + hd1 * cos - vd1 * sin; y1 = oy + hd1 * sin + vd1 * cos; x2 = ox + hd2 * cos - vd1 * sin; y2 = oy + hd2 * sin + vd1 * cos;
                x3 = ox + hd2 * cos - vd2 * sin; y3 = oy + hd2 * sin + vd2 * cos; x4 = ox + hd1 * cos - vd2 * sin; y4 = oy + hd1 * sin + vd2 * cos;
            }
            u1 =  sourceRect.X * normalize_u;  //gets the texture coords in terms of (0.0f-1.0f, 0.0f-1.0f)
            v1 =  sourceRect.Y * normalize_v;  // " 
            u2 = (sourceRect.X + sourceRect.Width)  * normalize_u;
            v2 = (sourceRect.Y + sourceRect.Height) * normalize_v; 
            if ((flip & SpriteEffects.FlipVertically) != 0) {
                var temp = v2; v2 = v1; v1 = temp;
            }
            if ((flip & SpriteEffects.FlipHorizontally) != 0) {
                var temp = u2; u2 = u1; u1 = temp;
            }
            vertices[vert_count].Position = new Vector3(x1, y1, z); vertices[vert_count].TextureCoordinate = new Vector2(u1, v1);   //upper-left texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = new Vector3(x2, y2, z); vertices[vert_count].TextureCoordinate = new Vector2(u2, v1);   //upper-right texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = new Vector3(x3, y3, z); vertices[vert_count].TextureCoordinate = new Vector2(u2, v2);   //lower-right texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = new Vector3(x4, y4, z); vertices[vert_count].TextureCoordinate = new Vector2(u1, v2);   //lower-left texCoord
            vertices[vert_count].Color = c1; vert_count++;
            if (vert_count >= 8187) {
                End(); began = true;
            }
        }//Draw (Non-Uniform)




        //  D R A W ---------------------------------------------
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawFullTex(Vector2 pos, float scale_x, float scale_y, SpriteEffects flip = SpriteEffects.None)
        {
            Draw(new Rectangle(0, 0, tex.Width, tex.Height), pos, new Vector2(scale_x, scale_y), 0f, Color.White, flip);
        }
        /// <summary> (rotations are centered) position, uniform scale, rotation, color </summary> 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Draw(Rectangle sourceRect, Vector2 pos, float scale, float rot, Color c1, SpriteEffects flip = SpriteEffects.None)
        {
            Draw(sourceRect, pos, new Vector2(scale, scale), rot, c1, flip);
        }
        /// <summary> (no origin - rotations happen around center) position, non-uniform scale, rotation, color </summary>        
        public void Draw(Rectangle sourceRect, Vector2 pos, Vector2 scale, float rot, Color c1, SpriteEffects flip = SpriteEffects.None)
        {
            if (!began) { Console.WriteLine("BEGIN not called before Quad Draw"); return; }
            float w, h, o_x, o_y, x1, y1, x2, y2, x3, y3, x4, y4, u1, v1, u2, v2;

            if ((scale.X != 1) || (scale.Y != 1)) {
                w = sourceRect.Width * scale.X; h = sourceRect.Height * scale.Y;
                o_x = w * 0.5f; o_y = h * 0.5f;                                  // * 0.5 __ half way is center of sprite to rotate around
            }
            else {   // same but no scaling
                w = sourceRect.Width; h = sourceRect.Height;
                o_x = w * 0.5f; o_y = h * 0.5f;
            }

            x1 = pos.X; y1 = pos.Y;         //upper-left
            x2 = pos.X + w; y2 = pos.Y;         //upper-right
            x3 = pos.X + w; y3 = pos.Y + h;     //lower-right
            x4 = pos.X; y4 = pos.Y + h;     //lower-left   
            if (rot != 0f) {
                float ox = pos.X + o_x, oy = pos.Y + o_y;
                float cos = (float)Math.Cos(rot), sin = (float)Math.Sin(rot);
                float hd1 = x1 - ox, vd1 = y1 - oy; // top-left dif
                float hd2 = x3 - ox, vd2 = y3 - oy; // bottom-right dif
                x1 = ox + hd1 * cos - vd1 * sin; y1 = oy + hd1 * sin + vd1 * cos;
                x2 = ox + hd2 * cos - vd1 * sin; y2 = oy + hd2 * sin + vd1 * cos;
                x3 = ox + hd2 * cos - vd2 * sin; y3 = oy + hd2 * sin + vd2 * cos;
                x4 = ox + hd1 * cos - vd2 * sin; y4 = oy + hd1 * sin + vd2 * cos;
            }
            u1 = sourceRect.X * normalize_u;
            v1 = sourceRect.Y * normalize_v;
            u2 = (sourceRect.X + sourceRect.Width) * normalize_u;
            v2 = (sourceRect.Y + sourceRect.Height) * normalize_v;
            if ((flip & SpriteEffects.FlipVertically) != 0) {
                var temp = v2; v2 = v1; v1 = temp;
            }
            if ((flip & SpriteEffects.FlipHorizontally) != 0) {
                var temp = u2; u2 = u1; u1 = temp;
            }
            vertices[vert_count].Position = new Vector3(x1, y1, z); vertices[vert_count].TextureCoordinate = new Vector2(u1, v1);  //upper-left texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = new Vector3(x2, y2, z); vertices[vert_count].TextureCoordinate = new Vector2(u2, v1);   //upper-right texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = new Vector3(x3, y3, z); vertices[vert_count].TextureCoordinate = new Vector2(u2, v2);   //lower-right texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = new Vector3(x4, y4, z); vertices[vert_count].TextureCoordinate = new Vector2(u1, v2);   //lower-left texCoord
            vertices[vert_count].Color = c1; vert_count++;
            if (vert_count >= 8187) {
                End(); began = true;
            }
        }



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
            if (!began) { Console.WriteLine("BEGIN not called before Quad Draw"); return; }
            float w, h, x1, y1, x2, y2, x3, y3, x4, y4, u1, v1, u2, v2;
            w = sourceRect.Width; h = sourceRect.Height;
                        
            x1 = pos.X;     y1 = pos.Y;     //upper-left
            x2 = pos.X + w; y2 = pos.Y;     //upper-right
            x3 = pos.X + w; y3 = pos.Y + h; //lower-right
            x4 = pos.X;     y4 = pos.Y + h; //lower-left

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
            vertices[vert_count].Position = new Vector3(x1, y1, z); vertices[vert_count].TextureCoordinate = new Vector2(u1, v1); //upper-left texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = new Vector3(x2, y2, z); vertices[vert_count].TextureCoordinate = new Vector2(u2, v1); //upper-right texCoord
            vertices[vert_count].Color = c2; vert_count++;
            vertices[vert_count].Position = new Vector3(x3, y3, z); vertices[vert_count].TextureCoordinate = new Vector2(u2, v2); //lower-right texCoord
            vertices[vert_count].Color = c3; vert_count++;
            vertices[vert_count].Position = new Vector3(x4, y4, z); vertices[vert_count].TextureCoordinate = new Vector2(u1, v2); //lower-left texCoord
            vertices[vert_count].Color = c4; vert_count++;
            if (vert_count >= 8187) {
                End(); began = true;
            }
        }



        // D R A W  - D I S T O R T  ( VECTOR TYPE )
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawDistort(Rectangle sourceRect, Vector2 pos, Vector2 scale, float rot, Vector2 vv1, Vector2 vv2, Vector2 vv3, Vector2 vv4, Color c1, SpriteEffects flip = SpriteEffects.None)
        {
            DrawColorDistort(sourceRect, pos, scale, rot, vv1, vv2, vv3, vv4, c1, c1, c1, c1, flip);
        }
        // FREEDOM TO MODIFY A SPRITE BY SHAPE AND COLORS:
        //-------------------------------------
        // D R A W  - C O L O R - D I S T O R T  ( VECTOR TYPE )
        //-------------------------------------
        /// <summary> (centered rotation) allows shape distortion(good for psuedo3D, swaying trees, etc...) and multiple color blends </summary>        
        public void DrawColorDistort(Rectangle sourceRect, Vector2 pos, Vector2 scale, float rot, 
            Vector2 vv1, Vector2 vv2, Vector2 vv3, Vector2 vv4, Color c1, Color c2, Color c3, Color c4, SpriteEffects flip = SpriteEffects.None)
        {
            if (!began) { Console.WriteLine("BEGIN not called before Quad Draw"); return; }
            float w, h, o_x, o_y, x1, y1, x2, y2, x3, y3, x4, y4, u1, v1, u2, v2;

            if ((scale.X != 1f) || (scale.Y != 1f)) {
                w = sourceRect.Width * scale.X; h = sourceRect.Height * scale.Y;
                o_x = w * 0.5f; o_y = h * 0.5f;
                //get scaled offsets:
                float xo1 = vv1.X * scale.X, yo1 = vv1.Y * scale.Y; float xo2 = vv2.X * scale.X, yo2 = vv2.Y * scale.Y;
                float xo3 = vv3.X * scale.X, yo3 = vv3.Y * scale.Y; float xo4 = vv4.X * scale.X, yo4 = vv4.Y * scale.Y;
                //what is the point after offset
                x1 = pos.X + xo1;     y1 = pos.Y + yo1;         //upper-left
                x2 = pos.X + w + xo2; y2 = pos.Y + yo2;         //upper-right
                x3 = pos.X + w + xo3; y3 = pos.Y + h + yo3;     //lower-right
                x4 = pos.X + xo4;     y4 = pos.Y + h + yo4;     //lower-left                
            }
            else { //same with no scaling:            
                w = sourceRect.Width; h = sourceRect.Height;   //if (origin.HasValue) { o_x = origin.Value.X; o_y = origin.Value.Y; } else { o_x = w * 0.5f; o_y = h * 0.5f; } //Note: w and h are already scaled sizes                        
                o_x = w * 0.5f; o_y = h * 0.5f;
                //need positions of destination                
                x1 = pos.X + vv1.X;     y1 = pos.Y + vv1.Y;         //upper-left
                x2 = pos.X + w + vv2.X; y2 = pos.Y + vv2.Y;         //upper-right
                x3 = pos.X + w + vv3.X; y3 = pos.Y + h + vv3.Y;     //lower-right
                x4 = pos.X + vv4.X;     y4 = pos.Y + h + vv4.Y;     //lower-left
            }
            if (rot != 0f) {
                float ox = pos.X + o_x, oy = pos.Y + o_y;
                float cos = (float)Math.Cos(rot), sin = (float)Math.Sin(rot); //this is actually quite fast on a modern computer
                float hd = x1 - ox, vd = y1 - oy;
                x1 = ox + hd * cos - vd * sin; y1 = oy + hd * sin + vd * cos; hd = x2 - ox; vd = y2 - oy;
                x2 = ox + hd * cos - vd * sin; y2 = oy + hd * sin + vd * cos; hd = x3 - ox; vd = y3 - oy;
                x3 = ox + hd * cos - vd * sin; y3 = oy + hd * sin + vd * cos; hd = x4 - ox; vd = y4 - oy;
                x4 = ox + hd * cos - vd * sin; y4 = oy + hd * sin + vd * cos;
            }
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
            vertices[vert_count].Position = new Vector3(x1, y1, z); vertices[vert_count].TextureCoordinate = new Vector2(u1, v1);   //upper-left texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = new Vector3(x2, y2, z); vertices[vert_count].TextureCoordinate = new Vector2(u2, v1);   //upper-right texCoord
            vertices[vert_count].Color = c2; vert_count++;
            vertices[vert_count].Position = new Vector3(x3, y3, z); vertices[vert_count].TextureCoordinate = new Vector2(u2, v2);   //lower-right texCoord
            vertices[vert_count].Color = c3; vert_count++;
            vertices[vert_count].Position = new Vector3(x4, y4, z); vertices[vert_count].TextureCoordinate = new Vector2(u1, v2);   //lower-left texCoord
            vertices[vert_count].Color = c4; vert_count++;
            if (vert_count >= 8187) {
                End(); began = true;
            }
        }//Draw (Color-Distort)




        //-----------
        // DRAW_DEST
        //-----------
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
            if (!began) { Console.WriteLine("BEGIN not called before Quad Draw"); return; }
            float w, h, x1, y1, x2, y2, x3, y3, x4, y4, u1, v1, u2, v2; // generate a TARGET WIDTH, HEIGHT based on the source            
            w = sourceRect.Width; h = sourceRect.Height;

            //need positions of destination            
            x1 = destRect.X;                   y1 = destRect.Y;                   //upper-left
            x2 = destRect.X + destRect.Width;  y2 = destRect.Y;                   //upper-right
            x3 = destRect.X + destRect.Width;  y3 = destRect.Y + destRect.Height; //lower-right
            x4 = destRect.X;                   y4 = destRect.Y + destRect.Height; //lower-left                          
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
            vertices[vert_count].Position = new Vector3(x1, y1, z); vertices[vert_count].TextureCoordinate = new Vector2(u1, v1);   //upper-left texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = new Vector3(x2, y2, z); vertices[vert_count].TextureCoordinate = new Vector2(u2, v1);   //upper-right texCoord
            vertices[vert_count].Color = c2; vert_count++;
            vertices[vert_count].Position = new Vector3(x3, y3, z); vertices[vert_count].TextureCoordinate = new Vector2(u2, v2);   //lower-right texCoord
            vertices[vert_count].Color = c3; vert_count++;
            vertices[vert_count].Position = new Vector3(x4, y4, z); vertices[vert_count].TextureCoordinate = new Vector2(u1, v2);   //lower-left texCoord
            vertices[vert_count].Color = c4; vert_count++;
            if (vert_count >= 8187) {
                End(); began = true;
            }
        }//Draw (Draw Dest 4 color)



        // DRAW DEST simplest:
        public void DrawDest(Rectangle destRect, Color c1)
        {
            if (!began) { Console.WriteLine("BEGIN not called before Quad Draw"); return; }
            float x1, y1, x2, y2, x3, y3, x4, y4;
            // need positions of destination: 
            x1 = destRect.X; y1 = destRect.Y;                                    //upper-left
            x2 = destRect.X + destRect.Width; y2 = destRect.Y;                   //upper-right
            x3 = destRect.X + destRect.Width; y3 = destRect.Y + destRect.Height; //lower-right
            x4 = destRect.X; y4 = destRect.Y + destRect.Height;                  //lower-left            
            vertices[vert_count].Position = new Vector3(x1, y1, z); vertices[vert_count].TextureCoordinate = new Vector2(0, 0); //upper-left texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = new Vector3(x2, y2, z); vertices[vert_count].TextureCoordinate = new Vector2(1, 0); //upper-right texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = new Vector3(x3, y3, z); vertices[vert_count].TextureCoordinate = new Vector2(1, 1); //lower-right texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = new Vector3(x4, y4, z); vertices[vert_count].TextureCoordinate = new Vector2(0, 1); //lower-left texCoord
            vertices[vert_count].Color = c1; vert_count++;
            if (vert_count >= 8187) {
                End(); began = true;
            }
        }//DrawDest simplest




        // XYZ ROTATION  D R A W  ------------------------------------------------------------------------------------------------
        /// <summary> (xyz rotates around center) position, non-uniform scale, x rotation, y rotation, z rotation, color </summary>        
        public void DrawRotXYZ(Rectangle sourceRect, Vector2 pos, Vector2 scale, float rotx, float roty, float rotz, Color c1, float? depth, SpriteEffects flip = SpriteEffects.None)
        {
            if (!began) { Console.WriteLine("BEGIN not called before Quad Draw"); return; }
            float w, h, o_x, o_y, u1, v1, u2, v2;
            Vector3 ul, ur, br, bl;

            if ((scale.X != 1) || (scale.Y != 1)) {
                w = sourceRect.Width * scale.X; h = sourceRect.Height * scale.Y;
                o_x = w * 0.5f; o_y = h * 0.5f;                                  // * 0.5 __ half way is center of sprite to rotate around
            }
            else {   // same but no scaling
                w = sourceRect.Width; h = sourceRect.Height;
                o_x = w * 0.5f; o_y = h * 0.5f;
            }

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
            u2 = (sourceRect.X + sourceRect.Width) * normalize_u;
            v2 = (sourceRect.Y + sourceRect.Height) * normalize_v;
            if ((flip & SpriteEffects.FlipVertically) != 0) {
                var temp = v2; v2 = v1; v1 = temp;
            }
            if ((flip & SpriteEffects.FlipHorizontally) != 0) {
                var temp = u2; u2 = u1; u1 = temp;
            }
            vertices[vert_count].Position = ul; vertices[vert_count].TextureCoordinate = new Vector2(u1, v1);  //upper-left texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = ur; vertices[vert_count].TextureCoordinate = new Vector2(u2, v1);   //upper-right texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = br; vertices[vert_count].TextureCoordinate = new Vector2(u2, v2);   //lower-right texCoord
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = bl; vertices[vert_count].TextureCoordinate = new Vector2(u1, v2);   //lower-left texCoord
            vertices[vert_count].Color = c1; vert_count++;
            if (vert_count >= 8187) {
                End(); began = true;
            }
        }



        // D R A W    L I N E S    A N D    R E C T A N G L E S ------------------------------------------------------------        
        private Rectangle pixel = new Rectangle(0, 0, 1, 1);
        public Rectangle PIXEL { get { return pixel; } set { pixel = value; pixel.Width = pixel.Height = 1; } }

        // D R A W   L I N E  
        /// <summary> draw a line from 2 vecs - PIXEL should be set as a rectangle source of a white dot somewhere in sprite-map </summary>
        public void DrawLine(Vector2 start, Vector2 end, Color color, float thickness = 2f)
        {
            Vector2 delta = end - start;
            float rot = (float)Math.Atan2(delta.Y, delta.X);
            pixel.Width = pixel.Height = 1;
            Draw(pixel, start, new Vector2(0, 0.5f), new Vector2(delta.Length(), thickness), rot, color);
        }

        /// <summary> Draw a line - make sure PIXEL has already been set to a white dot somewhere in your sprite-map source (unless not using a texture in this batch) </summary>
        public void Line(float x1, float y1, float x2, float y2, Color color, float thickness = 1f)
        {
            Vector2 end   = new Vector2(x2, y2);
            Vector2 start = new Vector2(x1, y1);
            Vector2 delta = end - start;
            pixel.Width = pixel.Height = 1;
            float rot = (float)Math.Atan2(delta.Y, delta.X);
            Draw(pixel, start, Vector2.Zero, new Vector2(delta.Length(), thickness), rot, color);
        }


        /// <summary> Quickly draws a rectangle using lines </summary>        
        public void RectLines(float x1, float y1, float x2, float y2, Color color, float thickness = 1f)
        {
            pixel.Width = pixel.Height = 1;
            Draw(pixel, new Vector2(x1, y1), Vector2.Zero, new Vector2((x2 - x1), thickness), 0f, color);
            Draw(pixel, new Vector2(x2, y1), Vector2.Zero, new Vector2(thickness, (y2 - y1)), 0f, color);
            Draw(pixel, new Vector2(x1, y2), Vector2.Zero, new Vector2((x2 - x1), thickness), 0f, color);
            Draw(pixel, new Vector2(x1, y1), Vector2.Zero, new Vector2(thickness, (y2 - y1)), 0f, color);
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
            if (!began) { Console.WriteLine("BEGIN not called before Quad Draw"); return; }
            float u1, v1, u2, v2;
            u1 = pixel.X * normalize_u;
            v1 = pixel.Y * normalize_v;
            u2 = (pixel.X + pixel.Width)  * normalize_u;
            v2 = (pixel.Y + pixel.Height) * normalize_v;
            vertices[vert_count].Position = new Vector3(p1, z); vertices[vert_count].TextureCoordinate = new Vector2(u1, v1); // upper-left
            vertices[vert_count].Color = c1; vert_count++;
            vertices[vert_count].Position = new Vector3(p2, z); vertices[vert_count].TextureCoordinate = new Vector2(u2, v1); // upper-right
            vertices[vert_count].Color = c2; vert_count++;
            vertices[vert_count].Position = new Vector3(p3, z); vertices[vert_count].TextureCoordinate = new Vector2(u2, v2); // lower-right
            vertices[vert_count].Color = c3; vert_count++;
            vertices[vert_count].Position = new Vector3(p4, z); vertices[vert_count].TextureCoordinate = new Vector2(u1, v2); // lower-left
            vertices[vert_count].Color = c4; vert_count++;
            if (vert_count >= 8187) {
                End(); began = true;
            }
        }


        // DRAW A COLOR FILLED RECTANGLE
        /// <summary> Draw a filled rect (make sure PIXEL has been set) </summary> 
        public void FillRect(Rectangle Pixel, Rectangle r, Color col)
        {
            if (!began) { Console.WriteLine("BEGIN not called before QuadBatch Draw. Draw aborted."); return; }
            float u1, v1, u2, v2;
            u1 = Pixel.X * normalize_u;  
            v1 = Pixel.Y * normalize_v; 
            u2 = (Pixel.X + Pixel.Width)  * normalize_u; 
            v2 = (Pixel.Y + Pixel.Height) * normalize_v; 
            Vector2 p1, p2, p3, p4;
            p1.X = r.X; p1.Y = r.Y; p2 = p1; p2.X += r.Width; p3 = p2; p3.Y += r.Height; p4 = p1; p4.Y += r.Height;
            vertices[vert_count].Position = new Vector3(p1, z); vertices[vert_count].TextureCoordinate = new Vector2(u1, v1); // upper-left
            vertices[vert_count].Color = col; vert_count++;
            vertices[vert_count].Position = new Vector3(p2, z); vertices[vert_count].TextureCoordinate = new Vector2(u2, v1); // upper-right
            vertices[vert_count].Color = col; vert_count++;
            vertices[vert_count].Position = new Vector3(p3, z); vertices[vert_count].TextureCoordinate = new Vector2(u2, v2); // lower-right
            vertices[vert_count].Color = col; vert_count++;
            vertices[vert_count].Position = new Vector3(p4, z); vertices[vert_count].TextureCoordinate = new Vector2(u1, v2); // lower-left
            vertices[vert_count].Color = col; vert_count++;
            if (vert_count >= 8187) {
                End(); began = true;
            }
        }

    }
}

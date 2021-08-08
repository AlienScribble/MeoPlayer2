using MeoMo;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace MeoPlayer_V2_Test {
    public class Game1 : Game
    {        
        GraphicsDeviceManager graphics;
        GraphicsDevice        gpu;
        SpriteBatch           spriteBatch;
        //SpriteBlast           spriteBlast;       // (experimental - discluding - using QuadBatch instead since it's more tested)      
        QuadBatch             quadBatch;           // (using namespace MeoMo)
        MeoMotion             meoMotion1;          // (using namespace MeoMo)  
        Input                 inp;

        //GAME ASSETS
        Texture2D             tex_background;
        MeoSprite[]           mSprite; 
        int                   id;                 // character selection        

        public static Rectangle mouse_rect;       // test mouse rect collisions
        public static Color     mouse_col = Color.Green;
        public static Texture2D pixel_tex;        // pixel texture used to draw rectangles


        //------------------
        // C O N S T R U C T
        //------------------                
        public Game1() {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;            
            inp = new Input();
        }



        //--------
        // I N I T
        //--------        
        // S e t   D i s p l a y 
        void SetDisplay(int Width, int Height, bool Fullscreen = false, bool hiDef = false, SurfaceFormat ColorFormat = SurfaceFormat.Color, DepthFormat depthFormat = DepthFormat.None) {
            graphics.PreferredBackBufferWidth   = Width;          graphics.PreferredBackBufferHeight   = Height;         graphics.IsFullScreen = Fullscreen;
            graphics.PreferredBackBufferFormat  = ColorFormat;    graphics.PreferredDepthStencilFormat = depthFormat;    if (hiDef) graphics.GraphicsProfile = GraphicsProfile.HiDef;            
            graphics.ApplyChanges();            
        }
        // I N I T 
        protected override void Initialize() {            
            SetDisplay(Width: 1024, Height: 768);
            gpu         = GraphicsDevice;
            spriteBatch = new SpriteBatch(gpu);
            //spriteBlast = new SpriteBlast(Content, gpu);
            quadBatch = new QuadBatch(Content, gpu);

            // SETUP MEOMOTION FOR A CHARACTER GROUP: 
            meoMotion1 = new MeoMotion(Content, gpu);

            base.Initialize();
        }



        //--------
        // L O A D 
        //--------
        const int HERO = 0, GOBL = 1;
        protected override void LoadContent() {
            
            // LOAD CHARACTER GROUP
            meoMotion1.Load_TXT_From_Content("Characters/", "Characters/characters.txt");

            // SETUP AN ANIMATION PLAYER FOR EACH CHARACTER: 
            Vector2 StartPos = new Vector2(512, 300);
            mSprite       = new MeoSprite[2];
            mSprite[HERO] = new MeoSprite("Wizzy",        StartPos, meoMotion1, quadBatch); //spriteBlast);
            mSprite[GOBL] = new MeoSprite("staff_goblin", StartPos, meoMotion1, quadBatch); //spriteBlast);

            tex_background = Content.Load<Texture2D>("background");

            // Create a texture for pixels/lines/fills (suggesting to have this on a sprite-sheet instead [reduce texture switching] but it's here for debug use)
            pixel_tex = new Texture2D(gpu, 1, 1, false, SurfaceFormat.Color); 
            pixel_tex.SetData(new[] { Color.White });
        }        
        protected override void UnloadContent() { }



        
        //------------
        // U P D A T E
        //------------
        protected override void Update(GameTime gameTime) {
            inp.Update();
            if (inp.Keypress(Keys.Escape)) Exit();

            // SELECT CHARACTER
            if (inp.Keypress(Keys.Enter)) { id++; if (id > 1) id = 0; }           

            // UPDATE CHARACTERS:
            mSprite[id].Update(gameTime, inp);

            mouse_rect = new Rectangle(inp.ms.Position, new Point(11, 20));   // pos, size
            
            base.Update(gameTime);
        }


        //--------
        // D R A W
        //--------
        protected override void Draw(GameTime gameTime) {

            // DRAW BACKGROUND    ( or use:    GraphicsDevice.Clear(Color.Transparent); )
            quadBatch.Begin(tex_background);
            quadBatch.DrawDest(new Rectangle(0, 0, gpu.Viewport.Width, gpu.Viewport.Height), Color.White);
            quadBatch.End();

            // DRAW CHARACTERS   ( grouping any characters which share the same spritesheet inside Begin-End )  
            quadBatch.Begin(meoMotion1.tex, BlendState.AlphaBlend);
            mSprite[id].Draw();

            quadBatch.SwitchTex(pixel_tex); // show rectangle around mouse [normally we'd just use a pixel rect from the sprite sheet so we don't need to switch textures]
            quadBatch.RectLines(mouse_rect, mouse_col, 2);
            quadBatch.RestoreTex();         // just in case we need to draw more stuff from meoMotion1.tex after this... which we don't in this example

            quadBatch.End();

            base.Draw(gameTime);
        }



        void Say(string s) {
            Console.WriteLine(s);
        }
    }
}

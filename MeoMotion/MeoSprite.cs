using MeoPlayer_V2_Test;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Diagnostics;

namespace MeoMo {

    //*************************
    // THIS IS JUST AN EXAMPLE  - You'll probably want to add lots of other properties or other MeoSprite types
    //*************************

    public class MeoSprite {
        public Vector2     pos, vel, max_vel = new Vector2(5,15);
        public float       rot, size = 1f;
        public Color       col;
        public MeoMotion   meo;
        public MeoPlayer   meoPlayer;          // ( using MeoMo )
        public QuadBatch   qBatch;             //SpriteBlast sBlast;             // reference for easy access
        public string      name;
        public int         idle, walk, attack;
        public int         anim;
        public float       walk_adjust = 1f;
                
        /// <param name="SheetName">Used to identify which character is selected (by original PNG name before combining [ie: if combine was used])</param>
        /// <param name="Position"> Staring position for character (not that important - you can set this at any time) </param>
        /// <param name="meo">      Reference to MeoMotion for holding characters & animations. You can also set a different MeoMotion object for different characters or character sets.</param>
        /// <param name="sBlast">   SpriteBlast is a slightly faster version of QuadBatch I started. It's very similar to SpriteBatch but depends on sprite-sheets(UV-Atlas) and no sorting (for speed)</param>
        public MeoSprite(string SheetName, Vector2 Position, MeoMotion Meo, QuadBatch quadBatch) //SpriteBlast spriteBlast)
        {
            pos       = Position;
            qBatch    = quadBatch;   //sBlast    = spriteBlast;
            meo       = Meo;

            // CHARACTER SELECT:             
            meoPlayer = new MeoPlayer(SheetName, pos, meo, qBatch); //sBlast);            
            name = SheetName;
            switch(name) {
                case "Wizzy":
                    pos.Y += 100;
                    idle   = meo.GetIndex(name, "idle1");  
                    walk   = meo.GetIndex(name, "walk");   meo.anim[walk].offset.Y -= 7f;  walk_adjust = 0.5f; // move character up a bit for walking and set a unique speed adjuster
                    attack = meo.GetIndex(name, "spell");  meo.anim[attack].speed = 2.2f;  // speeds up the original animation a bit
                    break;
                case "staff_goblin":
                    pos.Y -= 10;
                    idle   = meo.GetIndex(name, "idle");
                    walk   = meo.GetIndex(name, "walk");    walk_adjust = 0.3f;
                    attack = meo.GetIndex(name, "attack1"); meo.anim[attack].Adjust_Last_Key_Time(-300);
                    break;
            }
            anim = idle; meoPlayer.SetAnimation(idle);
        }



        public void Attack()
        {            
            if (anim != attack) {
                anim = attack; meoPlayer.SetAnimation(anim);
                // trigger sounds and maybe other initializations associated with this sprite's activity
                return;
            }                       
        }



        //------------
        // U P D A T E 
        //------------
        bool  spin;
        float last_vx, shrink;
        public void Update(GameTime gameTime, Input inp)
        {
            // GET COMMANDS:
            if (anim != attack) {
                if (inp.Keydown(Keys.Left))   { vel.X -= 0.5f; if (vel.X < -max_vel.X) vel.X = -max_vel.X; }
                if (inp.Keydown(Keys.Right))  { vel.X += 0.5f; if (vel.X >  max_vel.X) vel.X =  max_vel.X; }
                if (inp.Keypress(Keys.LeftControl)) spin   = true;
                if (inp.Keypress(Keys.LeftShift))   shrink = -1f;
                if (inp.Keypress(Keys.Space)) { Attack(); }
            }
            if (spin)      { rot += 0.2f; if (rot > 6.28f) { rot = 0; spin = false; } }
            if (shrink != 0) {
                if (shrink < 0) size -= 0.01f;
                if (shrink > 0) { size += 0.01f; if (size > 1f) { size = 1f; shrink = 0; } }
                if (size < 0.2f) shrink = 1;
            }
            
            Game1.mouse_col = Color.Green;
            // test if mouse is over body:
            if (meoPlayer.was_hit_by(Game1.mouse_rect)) Game1.mouse_col = Color.Yellow;

            // SELECT ANIMATION (IF CHANGE)
            if (anim==idle) {                
                if (vel.X!=0) { anim = walk; meoPlayer.SetAnimation(walk); }                
            } else if (anim==walk) {                
                if (vel.X==0) { anim = idle; meoPlayer.SetAnimation(idle); }                
            } else if (anim==attack) {
                if (meoPlayer.IsDoneAnimation()) {
                    if (vel.X != 0) { anim = walk; meoPlayer.SetAnimation(walk); }
                        else { anim = idle; meoPlayer.SetAnimation(idle); }
                }
                // attack is in progress... test if attack hits: 
                if (meoPlayer.hits(Game1.mouse_rect)) Game1.mouse_col = Color.Red;
            }

            // VELOCITY & FLIP STUFF 
            vel.X *= 0.93f;
            if (vel.X != 0) last_vx = vel.X;
            if ((vel.X < 0.4f) && (vel.X > -0.4f)) vel.X = 0;
            if (last_vx < 0) meoPlayer.flip = true; else if (last_vx > 0) meoPlayer.flip = false;                                
            
            // UPDATE POSITION  
            pos += vel;
            meoPlayer.position = pos;

            // ADJUST ANIMATION SPEED (if applicable)
            if (anim == walk) meo.anim[walk].speed = Math.Abs(vel.X) * walk_adjust;

            // UPDATE ANIMATION: 
            meoPlayer.Update(gameTime);
        }


        //--------
        // D R A W 
        //--------
        public void Draw()
        {            
            meoPlayer.Draw(Color.White, rot, size);
            meoPlayer.DrawBoxes();
        }

    }
}

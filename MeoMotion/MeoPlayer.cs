using MeoPlayer_V2_Test;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;

//************************************************************************************************************************************
// 2021 UPDATE
// What's new:
// Can optionally load TXT from Content folder (add txt and set property to copy & no processing required)
// It's possible to mark parts (like legs) to animate independently (like walk loop) while running other animations (like for upper body)
// MeoMotion editor, loader, and player now support animated bounding boxes which can be given an ID (ie: attack box, collide box, etc)
// and Player now contains detection methods for determining collisions

// ************************************************************************************************************************************
// PLEASE FEEL FREE TO MODIFY, AND DEVELOP THIS PLAYER FOR YOUR OWN USES OR CODE PUBLICATIONS
// N'HÉSITEZ PAS À MODIFIER ET DÉVELOPPER CE LECTEUR POUR VOS PROPRES UTILISATIONS OU CODER DES PUBLICATIONS 
//*************************************************************************************************************************************

namespace MeoMo {
    public class MeoPlayer
    {
        const bool  DIAGNOSTICS = false;  // debug option to show information in output window

        public bool PREMULTIPLY_ALPHA = true;
        const float HIDE_ALPHA_UNDER = 0.02f; // minimum alpha (anything lower is clipped from calculations)        
        //--------------------------------------------------------------------------------------------------
        public  Vector2 position;        // character's position         
        public  string  sheet_name;      // name of the original sheet used in the animator (without the .png) [this is just for identification purposes]
        public  int     animation_index; // index into list of animations (ie: anim[animation_index])         
        public  bool    flip;            // horizontal flip - use this to change direction character is facing
        public  bool    stopped, active; // animation stopped on completion, "active" could disable animation for pause (I've never used it) 
        public  bool    reverse, last_reverse; // play direction, play direction during previous iteration
        public  int     key1;            // current key
        public  int     key2;            // next key                
        public  float   timer;           // for interpolating between keys        
        public  float   play_speed;      // animation speed (make sure to set a2_play_speed correctly too when things change if mixing animations) 
        private bool    done_anim;       // used in IsDoneAnimation() to tell if at least one animation cycle has finished
        public  int     part_count;      // how many sprite parts
        public  Final[] final;           // holds final animation data        
        
        //public SpriteBlast batch;     // for distortion drawing 
        public  QuadBatch batch;        // using QuadBatch instead of SpriteBlast because SpriteBlast needs more testing

        public MeoMotion meo;           // meo = original instance of MeoMotion manager which loads and contains all the animations



        #region C O N S T R U C T O R - - - 
        /// <summary> Creates a new character -- note: character can be changed by changing sheet_name and then setting an animation.
        /// (The sheet_name is the original PNG file name used when making each character (see inside TXT file) and is used to identify different characters on a combined final sheet)  </summary>
        /// <param name="SheetName">used to determine which character on the spritesheet to use (uses the old original PNG sheet name to refer to it - check in your TXT file to see what to use)</param>
        /// <param name="Position">default position of character</param>
        /// <param name="Meo">instance of MeoMotion to use [loads and holds animations]</param>        
        public MeoPlayer(string SheetName, Vector2 Position, MeoMotion Meo, QuadBatch qbatch) // (you can use QuadBatch or SpriteBlast[experimental] )
        {
            // NOTE: If you are converting this for CPP, make sure to init all variables to 0, false, etc... (c# assumes 0 or false)
            sheet_name = SheetName;
            position   = Position;
            play_speed = 1.0f; 
            meo        = Meo;
            final      = new Final[(meo.max_parts + 1)];    // create a memory pool big enough to hold any character's render data
            int a = 0;
            do { final[a] = new Final(); a++; } while (a < (meo.max_parts + 1)); // allocate
            batch = qbatch;
        }
        #endregion // constructor - - - - -



        #region  S E T  A N I M A T I O N - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        /// <summary> Set a character animation by name with option to set flip horizontally </summary> 
        public void SetAnimation(string AnimationName, bool Flip = false, bool play_backward = false) // note: normally, if changing character direction - just change public variable flip = !flip
        {
            if (AnimationName == "none") { active = false; return; }    // usually better to just change public var active to false for this [this might only be used for pause]
            animation_index = meo.GetIndex(sheet_name, AnimationName);  // Debug.WriteLine("name: " + sheet_name + "    ---- ANIMATION NAME: " + AnimationName);
            CommonSetAnim(play_backward, Flip);
        }
        
        /// <summary> Set a character animation by index with option to set flip horizontally </summary> 
        public void SetAnimation(int AnimationIndex, bool Flip = false, bool Active=true, bool play_backward = false) // (if we already know the index)
        {
            if (!Active) { active = false; return; }
            animation_index = AnimationIndex;
            CommonSetAnim(play_backward, Flip);
        }
        
        // COMMON SET ANIM (used in SetAnimation above)
        void CommonSetAnim(bool play_backward, bool Flip)
        {
            mixed_play_mode = false; flip = Flip; active = true; key1 = 0; key2 = 1; timer = 0;     // reset vars
            done_anim = false; stopped = false; reverse = false; last_reverse = false;              // "        "
            part_count = meo.anim[animation_index].end_part - meo.anim[animation_index].start_part; // make sure we are using the correct part_count                        
            if (play_backward) StartReverse();
        }
        #endregion // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 




        #region I S  D O N E  A N I M A T I O N
        // see if at the end of an animation cycle (could check done_anim(if public) but this accounts for possibility of manual play direction recently changed)
        public bool IsDoneAnimation() 
        {
            if (last_reverse != reverse) return false;  // just changed playback direction so not done
            if (done_anim) {
                //done_anim = false;                    // (comment remains here as a reminder not to do this here) 
                return true; 
            }
            return false; 
        }
        #endregion



        #region S T A R T   R E V E R S E 
        // Used to play animation in reverse (change animation direction during animation - or used by SetAnimation if play_backward is true) [ie: duck-down, duck-up]
        public void StartReverse()
        {
            key2 = meo.anim[animation_index].num_keys - 1; key1 = key2 - 1; done_anim = false; stopped = false; reverse = true;
            timer = meo.anim[animation_index].times[key2]; 
        }
        #endregion




        //-----------------
        #region U P D A T E 
        //-----------------
        // Customizable update for character animation. Could have other updates with other behaviors too, or pass in vars to control behavior decisions.. (like point weapon or something)
        public void Update(GameTime gameTime)
        {
            if (!active) return;
//            last_timer = timer;
            int a = animation_index;
            int k1 = key1, k2 = key2;
            int t1 = meo.anim[a].times[k1], t2 = meo.anim[a].times[k2];
            float time_dif, percent;
            if (meo.anim[a].looping) done_anim = false;
            if (stopped) goto LABEL_update_done;
            if (reverse)   // R E V E R S E  
            {
                timer -= 16.666667f * (play_speed * meo.anim[a].speed); //timer -= (gameTime.ElapsedGameTime.Milliseconds * (play_speed * meo.anim[a].speed)); // <-- (could use this also) track milliseconds that passed since start of first key                       
                if (timer < t1)  // ready to switch keys to interpolate between
                {
                    key2 = key1;
                    key1--;
                    if (key1 < 0)
                    {
                        if (meo.anim[a].looping == false)
                        {   // STOP ANIMATION
                            key1 = 0; key2 = 0; stopped = true; 
                            timer = meo.anim[a].times[key1]; 
                            done_anim = true; last_reverse = reverse; goto LABEL_update_done;
                        }
                        else
                        {   // LOOP ANIMATION
                            key1 = meo.anim[a].num_keys-2; key2 = key1+1; timer = meo.anim[a].times[key2]; done_anim = true;
                        }
                    }
                    k1 = key1; k2 = key2; t1 = meo.anim[a].times[k1]; t2 = meo.anim[a].times[k2];
                }
            }
            else       // F O R W A R D 
            {
                timer += 16.666667f * (play_speed * meo.anim[a].speed); //timer += (gameTime.ElapsedGameTime.Milliseconds * (play_speed * meo.anim[a].speed)); // <-- (could use this also) track milliseconds that passed since start of first key                       
                if (timer > t2) // ready to switch keys to interpolate between
                {
                    key1 = key2;
                    key2++;
                    if (key2 >= meo.anim[a].num_keys)
                    {
                        if (meo.anim[a].looping == false)
                        {   // STOP ANIMATION
                            key2 = meo.anim[a].num_keys - 1; stopped = true; timer = 0; done_anim = true; last_reverse = reverse; goto LABEL_update_done;
                        }
                        else
                        {   // LOOP ANIMATION
                            key1 = 0; key2 = 1; timer = 0; done_anim = true; 
                        }
                    }
                    k1 = key1; k2 = key2; t1 = meo.anim[a].times[k1]; t2 = meo.anim[a].times[k2];
                }
            }
            time_dif = t2 - t1;                     // total time between both keys
            if (time_dif <= 0) time_dif = 0.0001f;  // prevent unlikely possibility of division by zero
            percent = (timer - t1) / time_dif;      // what is the percentage (0-1) [for interpolation]

            Vector2 pos, scale, o1, o2, o3, o4;
            float rot;
            float x1, x2, x3, x4;
            float y1, y2, y3, y4;
            int i = 0, p;
            do
            {
                final[i].order = meo.anim[a].keys[i, k1].order;
                if (meo.anim[a].keys[i, k1].active == false) { final[i].hide = true; i++; continue; } // (part not shown - skip - go to do)
                final[i].hide = false;
                final[i].part = meo.anim[a].keys[i, k1].part; p = final[i].part;
                
                // ADDED (for mixed animations)
                if ((mixed_play_mode)&&(meo.parts[p].mix_flag == true)) { i++; continue; } // ADDED - Second Update will do these ones (may be running on different playspeed and different keys)
                
                // interpolate the data between the 2 keyframes
                final[i].alpha = MathHelper.Lerp(meo.anim[a].keys[i, k1].alpha, meo.anim[a].keys[i, k2].alpha, percent); // blend alpha transparency
                final[i].red   = MathHelper.Lerp(meo.anim[a].keys[i, k1].red,   meo.anim[a].keys[i, k2].red,   percent);
                final[i].green = MathHelper.Lerp(meo.anim[a].keys[i, k1].green, meo.anim[a].keys[i, k2].green, percent);
                final[i].blue  = MathHelper.Lerp(meo.anim[a].keys[i, k1].blue,  meo.anim[a].keys[i, k2].blue,  percent);
                if (final[i].alpha < HIDE_ALPHA_UNDER) { final[i].hide = true; i++; continue; }
                if (final[i].alpha > 1.0f) final[i].alpha = 1.0f;                                                        // precaution
                pos = Vector2.Lerp(meo.anim[a].keys[i, k1].pos, meo.anim[a].keys[i, k2].pos, percent);                   // blend position                
                rot = MathHelper.Lerp(meo.anim[a].keys[i, k1].rot, meo.anim[a].keys[i, k2].rot, percent);                // blend rotation
                scale = Vector2.Lerp(meo.anim[a].keys[i, k1].scale, meo.anim[a].keys[i, k2].scale, percent);             // blend scale                
                o1 = Vector2.Lerp(meo.anim[a].keys[i, k1].o1, meo.anim[a].keys[i, k2].o1, percent);                      // blend distortion offsets
                o2 = Vector2.Lerp(meo.anim[a].keys[i, k1].o2, meo.anim[a].keys[i, k2].o2, percent);
                o3 = Vector2.Lerp(meo.anim[a].keys[i, k1].o3, meo.anim[a].keys[i, k2].o3, percent);
                o4 = Vector2.Lerp(meo.anim[a].keys[i, k1].o4, meo.anim[a].keys[i, k2].o4, percent);
                // calculate the transformed vertices from the above data                
                x1 = meo.parts[p].m1.X * scale.X; y1 = meo.parts[p].m1.Y * scale.Y; // scale part points at origin(0,0)
                x2 = meo.parts[p].m2.X * scale.X; y2 = meo.parts[p].m2.Y * scale.Y;
                x3 = meo.parts[p].m3.X * scale.X; y3 = meo.parts[p].m3.Y * scale.Y;
                x4 = meo.parts[p].m4.X * scale.X; y4 = meo.parts[p].m4.Y * scale.Y;
                pos += meo.anim[a].offset; // adjust postions by default offset property
                // HERE YOU COULD ADD EXTRA PROGRAMMED ROTATION RESPONSES (like trailing hair, tail, etc - or vector based weapon pointing)
                // Note: Use: meo.parts[p].name to identify and if has children add child to end-point of parent-limb (and add parent rotation to child rotation also)  
                if (rot != 0f)
                {
                    float cos = (float)Math.Cos(rot), sin = (float)Math.Sin(rot);   // rotate points around origin and then add the position where they belong
                    final[i].v1.X = pos.X + x1 * cos - y1 * sin; final[i].v1.Y = pos.Y + x1 * sin + y1 * cos;
                    final[i].v2.X = pos.X + x2 * cos - y2 * sin; final[i].v2.Y = pos.Y + x2 * sin + y2 * cos;
                    final[i].v3.X = pos.X + x3 * cos - y3 * sin; final[i].v3.Y = pos.Y + x3 * sin + y3 * cos;
                    final[i].v4.X = pos.X + x4 * cos - y4 * sin; final[i].v4.Y = pos.Y + x4 * sin + y4 * cos;
                }
                else
                {
                    final[i].v1.X = pos.X + x1; final[i].v1.Y = pos.Y + y1;         // no rotation, so just put the points in the correct position
                    final[i].v2.X = pos.X + x2; final[i].v2.Y = pos.Y + y2;
                    final[i].v3.X = pos.X + x3; final[i].v3.Y = pos.Y + y3;
                    final[i].v4.X = pos.X + x4; final[i].v4.Y = pos.Y + y4;    
                }
                final[i].v1 += o1; // add the distortion offsets of the points
                final[i].v2 += o2;
                final[i].v3 += o3;
                final[i].v4 += o4;
                if (flip) // flip horizontally: 
                {
                    final[i].v1.X = -final[i].v1.X;
                    final[i].v2.X = -final[i].v2.X;
                    final[i].v3.X = -final[i].v3.X;
                    final[i].v4.X = -final[i].v4.X;
                }
                i++;
            } while (i < part_count);
            last_reverse = reverse;

            // ADDED  
            LABEL_update_done:
            if (mixed_play_mode)             Update2(gameTime);          // override animation for certain elements (mixing 2 animations) 
            if (meo.anim[a].boxes.Count > 0) UpdateBoxes(gameTime);      // update any bounding box detections
        }
        #endregion //Update
//        float last_timer;


        //--------------
        #region D R A W
        //--------------
        ///<summary> Customizable draw for character </summary> // Could make other draw overloads or pass in vars to control drawing behavior for specific characters.        
        ///<param name="character_rot">Option to rotate character around its position (you could add additional params to send an origin offset in DrawTransformedVerts)</param>
        ///<param name="scale">This allows in-game scaling (like if you get bigger or smaller for some reason) [ non-uniform scaling support can be added too ] </param>
        public void Draw(Color color, float character_rot = 0f, float scale = 1f)
        {
            rem_scale = scale;
            if (!active) return;          

            bool last_was_additive = false;
            int i = 0, n = 0, p;
            Color col = color;
            n = 0;
            do
            {
                i = final[n].order;
                if (final[i].hide) { n++; continue; }
                p = final[i].part;                                  // may switch parts during animation

                // ADDITIVE SWITCHER: 
                if (meo.parts[p].additive && !last_was_additive) {
                    batch.End();  batch.Begin(meo.tex, BlendState.Additive);  last_was_additive = true; 
                } 
                else if (!meo.parts[p].additive && last_was_additive)  { 
                    batch.End();  if (PREMULTIPLY_ALPHA) batch.Begin(meo.tex, BlendState.AlphaBlend);  else batch.Begin(meo.tex, BlendState.NonPremultiplied);  last_was_additive = false;
                }

                Vector4 v_col = color.ToVector4();
                col.R = (byte)((final[i].red   * v_col.X) * 255.0f);
                col.G = (byte)((final[i].green * v_col.Y) * 255.0f);
                col.B = (byte)((final[i].blue  * v_col.Z) * 255.0f);                
                col.A = (byte)((final[i].alpha * v_col.W) * 255.0f);
                if (PREMULTIPLY_ALPHA) col = Color.FromNonPremultiplied(col.ToVector4()); // make sure the properties of the image in content is set to premultiply alpha true                  

                //if ((character_rot == 0f) && (scale == 1f))
                batch.DrawTransformedVerts(meo.parts[p].rect, position, final[i].v1, final[i].v2, final[i].v3, final[i].v4, col); // draw transformed sprite parts at character's position                
                // The following: works if using SpriteBlast.cs [all calculations done in hardware]  (this could be made for QuadBatch too) 
                //else
                  //  batch.DrawTransformedVerts(meo.parts[p].rect, position, position, new Vector2(scale), character_rot, final[i].v1, final[i].v2, final[i].v3, final[i].v4, col);

                n++;
            } while (n < part_count);            
            if (last_was_additive) { batch.End(); if (PREMULTIPLY_ALPHA) batch.Begin(meo.tex, BlendState.AlphaBlend); else batch.Begin(meo.tex, BlendState.NonPremultiplied); last_was_additive = false; }
        }
        float rem_scale = 1f;
        #endregion
        



        #region G E T  I N D E X  ---------------------------------------------------
        public int GetIndex(string AnimationName, bool show_errors = true)
        {
            if (AnimationName == "none") return 0;
            return meo.GetIndex(sheet_name, AnimationName, show_errors); 
        }
        #endregion //----------------------------------------------------------------





        //---------------------------------------------------------------------------------------------------------------------------------------------------------------
        // THE FOLLOWING IS NEW - USED FOR MIXING WALKING WITH AIMING (or looped lower-body animation and allow upper body parts to follow a different animation)
        /// This stuff isn't essential to running plain animations - I'm using it for some characters in my own games - I make it available in case you want to try it too. 


        // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // N  E  W   (for MIXED PLAY:) ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        #region MIXED PLAY's  V A R S:
        public bool mixed_play_mode;   // whether or not to mix 2 animations (ie: attack1 + walk (in which case a Mixed Parts List is used to separate leg animation))        
        public int  animation_index_2; // secondary animation (like walk)
        public List<int> mixList;      // parts used in secondary animation
        public float a2_play_speed;    // secondary animation speed    
        public bool  a2_stopped, a2_reverse, a2_last_reverse;
        public int   a2_key1;          // current key
        public int   a2_key2;          // next key                
        public float a2_timer;         // for interpolating between keys                
        private bool a2_done_anim;        
        private int  mix_list_len;
        #endregion


        
        #region D U M P   P A R T S   L I S T 
        /// <summary> Utility to show names of parts in the animation (useful for determining what to specify for GetMixedPartsList) </summary>        
        public void DumpPartsList(string AnimationName) {            
            int a_2    = meo.GetIndex(sheet_name, AnimationName);
            int pcount = meo.anim[a_2].end_part - meo.anim[a_2].start_part;
            int i = 0;
            do { Report("PART:   " + meo.parts[i].name); i++; } while (i < pcount);
        }
        #endregion



        #region M I X E D   P A R T S   G E T   L I S T - - - - - - - - - -
        /// <summary> STEP1) Create a list of parts which can be assigned to a different animation (ie: legs using "walk") [while other body parts do something else - like attack] </summary>
        /// <param name="AnimationName">Name of secondary animation to use (like "walk")</param>
        /// <param name="part_list">"Check .TXT file to see exact part names to add to list [like legs might be L_thigh001, L_calf001, etc...] (also see: DumpPartsList)</param>
        public List<int> MixedParts_GetList(string AnimationName, params string[] parts_list)
        {
            List<int> mix_part_list;   // ie: legs if secondary animation is walking (primary animation might be some attack [except normally legs don't move])
            mix_part_list = new List<int>();
            int a_2 = animation_index_2 = meo.GetIndex(sheet_name, AnimationName);
            int pcount = meo.anim[a_2].end_part - meo.anim[a_2].start_part;
            int i = 0; 
            mix_list_len = parts_list.Length;
            do
            {
                string entry = parts_list[i];
                int n = 0;
                do
                {
                    meo.parts[n].mix_flag = false;
                    if (entry == meo.parts[n].name) {
                        meo.parts[n].mix_flag = true;   // by default I assume only 2 animations are mixed but an overload of MixAnimation can be used to set flagged parts if there are other mixed animations
                        mix_part_list.Add(n);           if (DIAGNOSTICS) Report("FOUND: " + meo.parts[n].name + "   mix_flag at index: " + n); // diagnostics
                        break; 
                    }
                    n++;
                } while (n < pcount);
                i++;
            } while (i < mix_list_len);
            return mix_part_list;
        }
        #endregion



        #region M I X E D   P A R T S   F L A G - - - - - - - - - - (by name)
        /// <summary> (string) Similar to MixedParts_GetList but simply sets mix_flags assuming only 1 mixed animation will be used (ie: walk)
        /// however this could still work if same parts would be flagged in other mixes (like run & walk). Using this instead of GetList is a bit more efficient. </summary>
        public void MixedParts_Flag(string AnimationName, params string[] parts_list)
        {            
            int a_2 = animation_index_2 = meo.GetIndex(sheet_name, AnimationName);
            int pcount = meo.anim[a_2].end_part - meo.anim[a_2].start_part;
            // CLEAR FLAG LIST: 
            int n = 0; do { meo.parts[n].mix_flag = false; n++; } while (n < pcount);
            // SET FLAGS
            int i = 0; mix_list_len = parts_list.Length;
            do {
                string entry = parts_list[i];
                n = 0;
                do {                    
                    if (entry == meo.parts[n].name) {
                        meo.parts[n].mix_flag = true;   // by default I assume only 2 animations are mixed but an overload of MixAnimation can be used to set flagged parts if there are other mixed animations
                        if (DIAGNOSTICS) Report("FOUND: " + meo.parts[n].name + "   flag at index: " + n); // diagnostics
                        break;
                    }
                    n++;
                } while (n < pcount);
                i++;
            } while (i < mix_list_len);            
        }
        #endregion


        #region M I X E D   P A R T S   F L A G - - - - - - - - - - (by index)
        /// <summary> (index) Similar to MixedParts_GetList but simply sets mix_flags assuming only 1 mixed animation will be used (ie: walk)
        /// however this could still work if same parts would be flagged in other mixes (like run & walk). Using this instead of GetList is a bit more efficient. </summary>
        public void MixedParts_Flag(int AnimationIndex, params string[] parts_list)
        {
            int a_2 = animation_index_2 = AnimationIndex;
            int pcount = meo.anim[a_2].end_part - meo.anim[a_2].start_part;            
            // CLEAR FLAG LIST: 
            int n = 0; do { meo.parts[n].mix_flag = false; n++; } while (n < pcount);            
            // SET FLAGS:
            int i = 0; mix_list_len = parts_list.Length;
            do {
                string entry = parts_list[i];
                n = 0;
                do {                    
                    if (entry == meo.parts[n].name) {
                        meo.parts[n].mix_flag = true;   // by default I assume only 2 animations are mixed but an overload of MixAnimation can be used to set flagged parts if there are other mixed animations
                        if (DIAGNOSTICS) Report("FOUND: " + meo.parts[n].name + "   flag at index: " + n); // diagnostics
                        break;
                    }
                    n++;
                } while (n < pcount);
                i++;
            } while (i < mix_list_len);            
        }
        #endregion



        #region M I X  A N I M A T I O N - - - - - - - - - - - - - (by animation NAME -- version which accepts a parts list)
        /// <summary> Set a secondary character animation (like walking) [while doing something else (like attacking) ] </summary> 
        /// <param name="MainAnimation">Main animation of interest (like an attack)</param>
        /// <param name="Looped2ndAnimation">A looping secondary animation like walking or running.</param>
        /// <param name="MixPartsList"> Predetermined list of parts to use for mixing in a looped background animation (like walk) [ie: thigh1, calf1, foot1, thigh2, etc]</param>
        /// <param name="set_mix_flags">By default it will go through and set mixing flags, but if you only need to blend one thing for entire character (like a walk), you can set to false for more efficiency</param>
        public void MixAnimation(string MainAnimation, string Looped2ndAnimation, List<int> MixPartsList, bool set_mix_flags = true, bool Flip = false, bool play_backward = false, bool play_backward2 = false) 
        {
            a2_play_speed     = play_speed;
            mixList           = MixPartsList;
            animation_index   = meo.GetIndex(sheet_name, MainAnimation);
            animation_index_2 = meo.GetIndex(sheet_name, Looped2ndAnimation); if (animation_index == animation_index_2) Report("ERROR: Mixed animation indices are the same. (string)");
            CommonSetAnim(play_backward, Flip);
            CommonSetAnim2(play_backward2, set_mix_flags);
        }
        #endregion


        #region M I X  A N I M A T I O N - - - - - - - - - - - - - (by animation INDEX -- version which accepts a parts list)
        /// <summary> Set a secondary character animation (like walking) [while doing something else (like attacking) ] </summary>         
        /// <param name="MainAnimIndex">Index of main animation of interest (like an attack)</param>
        /// <param name="Looped2ndAnimIndex">Index of a looping secondary animation like walking or running.</param>
        /// <param name="MixPartsList" >Predetermined list of parts to use for mixing in a looped background animation (like walk) [ie: thigh1, calf1, foot1, thigh2, etc]</param>
        /// <param name="set_mix_flags">By default it will go through and set mixing flags, but if you only need to blend one thing for entire character (like a walk), you can set to false for more efficiency</param>
        public void MixAnimation(int MainAnimIndex, int Looped2ndAnimIndex, List<int> MixPartsList, bool set_mix_flags = true, bool Flip = false, bool play_backward = false, bool play_backward2 = false) // (if we already know the index)
        {   
            mixList           = MixPartsList;
            animation_index   = MainAnimIndex;
            animation_index_2 = Looped2ndAnimIndex; if (animation_index == animation_index_2) Report("ERROR: Mixed animation indices are the same. (index)");
            CommonSetAnim(play_backward, Flip);
            CommonSetAnim2(play_backward2, set_mix_flags);
        }
        #endregion


        #region COMMON SET ANIM 2 (used by Mix Animation to setup common animation start settings) 
        void CommonSetAnim2(bool play_backward2, bool set_mix_flags)
        {
            mixed_play_mode = true;
            if (DIAGNOSTICS) { if ((meo.anim[animation_index_2].end_part - meo.anim[animation_index_2].start_part) != part_count) Report("ERROR: Can't mix animations: Part counts need to be the same."); }                       
            a2_key1 = 0;  a2_key2 = 1;
            a2_timer = 0;
            a2_stopped = false; a2_reverse = false; a2_last_reverse = false; a2_done_anim = false;
            if (play_backward2) StartReverse2();
            if (set_mix_flags)                                              // note: you'll need this to be true if you might mix more than 1 secondary animation at some point
            {
                int i = 0;
                do {
                    meo.parts[i].mix_flag = false;
                    if (mixList.Contains(i)) meo.parts[i].mix_flag = true;  // tell the parts list which ones to use for secondary animation (ie: walk)
                    i++;
                } while (i < part_count);
            }            
        }
        #endregion


        #region M I X   A N I M A T I O N - - - - - - - - - - - - - (by animation NAME -- no parts list needed  [parts are already flagged] )
        ///<summary> (string) This version doesn't need a MixPartsList and assumes the MixedParts_Flag was used and flagged the necessary parts [this is efficient for mixing with
        /// a single type of animation which needs to use the same parts [like walk, run] -- if there's varied mixing, a mix-list may be needed </summary>
        public void MixAnimation(string MainAnimation, string Looped2ndAnimation, bool Flip = false, bool play_backward = false, bool play_backward2 = false)
        {
            a2_play_speed     = play_speed;
            animation_index   = meo.GetIndex(sheet_name, MainAnimation);
            animation_index_2 = meo.GetIndex(sheet_name, Looped2ndAnimation); if (animation_index == animation_index_2) Report("ERROR: Mixed animation indices are the same. (string)");            
            CommonSetAnim(play_backward, Flip);
            CommonSetAnim2(play_backward2, false);
        }
        #endregion


        #region M I X   A N I M A T I O N - - - - - - - - - - - - - (by animation INDEX -- no parts list needed  [parts are already flagged] )
        ///<summary> (index) This version doesn't need a MixPartsList and assumes the MixedParts_Flag was used and flagged the necessary parts [this is efficient for mixing with
        /// a single type of animation which needs to use the same parts [like walk, run] -- if there's varied mixing, a mix-list may be needed </summary>
        public void MixAnimation(int MainAnimIndex, int Looped2ndAnimIndex, bool Flip = false, bool play_backward = false, bool play_backward2 = false) // (if we already know the index)
        {
            a2_play_speed     = play_speed;            
            animation_index   = MainAnimIndex;
            animation_index_2 = Looped2ndAnimIndex; if (animation_index == animation_index_2) Report("ERROR: Mixed animation indices are the same. (index)");            
            CommonSetAnim(play_backward, Flip);
            CommonSetAnim2(play_backward2, false);
        }
        #endregion


        #region S T A R T   R E V E R S E  #2
        // reverse play pertaining to secondary animation
        public void StartReverse2()
        {
            a2_key2  = meo.anim[animation_index_2].num_keys - 1; a2_key1 = a2_key2 - 1; a2_stopped = false; a2_reverse = true; a2_done_anim = false;
            a2_timer = meo.anim[animation_index_2].times[a2_key2];
        }
        #endregion


        #region D O N E   A N I M A T I O N  #2
        public bool DoneAnimation2()
        {
            if (a2_last_reverse != a2_reverse) return false;
            if (a2_done_anim) return true;
            return false;
        }
        #endregion



        #region U P D A T E  2  ( mixed animation update - NOTE: this is assumed to be a looping animation [like running or something] )
        // (same as other Update but using different animation for certain parts of character (like legs for "walk"))
        void Update2(GameTime gameTime)
        {
            int a  = animation_index_2;            
            int k1 = a2_key1, k2 = a2_key2;
            int t1 = meo.anim[a].times[k1], t2 = meo.anim[a].times[k2];
            float time_dif, percent;            
            if (a2_reverse)
            {
                a2_timer -= 16.666667f * (a2_play_speed * meo.anim[a].speed); //a2_timer -= (gameTime.ElapsedGameTime.Milliseconds * (a2_play_speed * meo.anim[a].speed)); // <-- (could use this also) track milliseconds that passed since start of first key                                       
                if (a2_timer < t1) // ready to switch keys to interpolate between
                {
                    a2_key2 = a2_key1;
                    a2_key1--;
                    if (a2_key1 < 0)
                    {
                        #region COMMENTS1
                        /// NOTE: COMMENTED OUT (kept as reminder that Update2 is mainly for repeated animations like walking) [Update2 exists so you can aim and walk at same time]
                        //if (meo.anim[a].looping == false)
                        //{   // STOP ANIMATION
                        //    a2_key1 = 0; a2_key2 = 0; a2_stopped = true;
                        //    a2_timer = meo.anim[a].times[a2_key1];
                        //    a2_done_anim = true; a2_last_reverse = a2_reverse; return;
                        //}
                        //else
                        //{   
                        // LOOP ANIMATION
                        #endregion
                        a2_key1 = meo.anim[a].num_keys - 2; a2_key2 = key1 + 1; a2_timer = meo.anim[a].times[a2_key2]; //a2_done_anim = true;
                       // }
                    }
                    k1 = a2_key1; k2 = a2_key2; t1 = meo.anim[a].times[k1]; t2 = meo.anim[a].times[k2];
                }
            }
            else
            {
                a2_timer += 16.666667f * (a2_play_speed * meo.anim[a].speed); //a2_timer += (gameTime.ElapsedGameTime.Milliseconds * (a2_play_speed * meo.anim[a].speed)); // <-- (could use this also) track milliseconds that passed since start of first key                                                       
                if (a2_timer > t2) // ready to switch keys to interpolate between
                {                    
                    a2_key1 = a2_key2;
                    a2_key2++;
                    if (a2_key2 >= meo.anim[a].num_keys)
                    {
                        #region COMMENTS2
                        //if (meo.anim[a].looping == false)
                        //{   // STOP ANIMATION
                        //    a2_key2 = meo.anim[a].num_keys - 1; a2_stopped = true; a2_timer = 0; a2_done_anim = true; a2_last_reverse = a2_reverse; return;
                        //}
                        //else
                        //{   
                        // LOOP ANIMATION                        
                        #endregion
                        a2_key1 = 0; a2_key2 = 1; a2_timer = 0; //a2_done_anim = true;
                        //}
                    }
                    k1 = a2_key1; k2 = a2_key2; t1 = meo.anim[a].times[k1]; t2 = meo.anim[a].times[k2];
                }
            }

            time_dif = t2 - t1;                     // total time between both keys
            if (time_dif <= 0) time_dif = 0.0001f;  // prevent unlikely possibility of division by zero
            percent = (a2_timer - t1) / time_dif;      // what is the percentage (0-1) [for interpolation]
            
            Vector2 pos, scale, o1, o2, o3, o4;
            float rot;
            float x1, x2, x3, x4;
            float y1, y2, y3, y4;
            int i = 0, p;
            do
            {
                final[i].order = meo.anim[a].keys[i, k1].order;
                if (meo.anim[a].keys[i, k1].active == false) { final[i].hide = true; i++; continue; } // (part not shown - skip - go to do)
                final[i].hide  = false;
                final[i].part  = meo.anim[a].keys[i, k1].part; p = final[i].part;

                // ADDED (for mixed animations)
                if (meo.parts[p].mix_flag == false) { i++; continue; } // Already did this one in primary animation so skip it. 
                // interpolate the data between the 2 keyframes
                final[i].alpha = MathHelper.Lerp(meo.anim[a].keys[i, k1].alpha, meo.anim[a].keys[i, k2].alpha, percent); // blend alpha transparency
                final[i].red   = MathHelper.Lerp(meo.anim[a].keys[i, k1].red,   meo.anim[a].keys[i, k2].red,   percent);
                final[i].green = MathHelper.Lerp(meo.anim[a].keys[i, k1].green, meo.anim[a].keys[i, k2].green, percent);
                final[i].blue  = MathHelper.Lerp(meo.anim[a].keys[i, k1].blue,  meo.anim[a].keys[i, k2].blue,  percent);
                if (final[i].alpha < HIDE_ALPHA_UNDER) { final[i].hide = true; i++; continue; }
                if (final[i].alpha > 1.0f) final[i].alpha = 1.0f;                                                        // precaution
                pos = Vector2.Lerp(meo.anim[a].keys[i, k1].pos, meo.anim[a].keys[i, k2].pos, percent);                   // blend position                
                rot = MathHelper.Lerp(meo.anim[a].keys[i, k1].rot, meo.anim[a].keys[i, k2].rot, percent);                // blend rotation
                scale = Vector2.Lerp(meo.anim[a].keys[i, k1].scale, meo.anim[a].keys[i, k2].scale, percent);             // blend scale                
                o1 = Vector2.Lerp(meo.anim[a].keys[i, k1].o1, meo.anim[a].keys[i, k2].o1, percent);                      // blend distortion offsets
                o2 = Vector2.Lerp(meo.anim[a].keys[i, k1].o2, meo.anim[a].keys[i, k2].o2, percent);
                o3 = Vector2.Lerp(meo.anim[a].keys[i, k1].o3, meo.anim[a].keys[i, k2].o3, percent);
                o4 = Vector2.Lerp(meo.anim[a].keys[i, k1].o4, meo.anim[a].keys[i, k2].o4, percent);
                // calculate the transformed vertices from the above data                
                x1 = meo.parts[p].m1.X * scale.X; y1 = meo.parts[p].m1.Y * scale.Y; // scale part points at origin(0,0)
                x2 = meo.parts[p].m2.X * scale.X; y2 = meo.parts[p].m2.Y * scale.Y;
                x3 = meo.parts[p].m3.X * scale.X; y3 = meo.parts[p].m3.Y * scale.Y;
                x4 = meo.parts[p].m4.X * scale.X; y4 = meo.parts[p].m4.Y * scale.Y;
                pos += meo.anim[a].offset; // adjust postions by default offset property
                // HERE YOU COULD ADD EXTRA PROGRAMMED ROTATION RESPONSES (like trailing hair, tail, etc - or vector based weapon pointing)
                // Note: Use: meo.parts[p].name to identify and if has children add child to end-point of parent-limb (and add parent rotation to child rotation also)  
                if (rot != 0f)
                {
                    float cos = (float)Math.Cos(rot), sin = (float)Math.Sin(rot);   // rotate points around origin and then add the position where they belong
                    final[i].v1.X = pos.X + x1 * cos - y1 * sin; final[i].v1.Y = pos.Y + x1 * sin + y1 * cos;
                    final[i].v2.X = pos.X + x2 * cos - y2 * sin; final[i].v2.Y = pos.Y + x2 * sin + y2 * cos;
                    final[i].v3.X = pos.X + x3 * cos - y3 * sin; final[i].v3.Y = pos.Y + x3 * sin + y3 * cos;
                    final[i].v4.X = pos.X + x4 * cos - y4 * sin; final[i].v4.Y = pos.Y + x4 * sin + y4 * cos;
                }
                else
                {
                    final[i].v1.X = pos.X + x1; final[i].v1.Y = pos.Y + y1;         // no rotation, so just put the points in the correct position
                    final[i].v2.X = pos.X + x2; final[i].v2.Y = pos.Y + y2;
                    final[i].v3.X = pos.X + x3; final[i].v3.Y = pos.Y + y3;
                    final[i].v4.X = pos.X + x4; final[i].v4.Y = pos.Y + y4;
                }
                final[i].v1 += o1; // add the distortion offsets of the points
                final[i].v2 += o2;
                final[i].v3 += o3;
                final[i].v4 += o4;
                if (flip) // flip horizontally: 
                {
                    final[i].v1.X = -final[i].v1.X;
                    final[i].v2.X = -final[i].v2.X;
                    final[i].v3.X = -final[i].v3.X;
                    final[i].v4.X = -final[i].v4.X;
                }
                i++;
            } while (i < part_count);
            a2_last_reverse = a2_reverse;            
        }//Update2
        #endregion


        #region U P D A T E   B O X E S 
        // NOTE: boxes use a different key system that doesn't play well with the rest of meo-motion - this is partly 
        // because for some things I wanted to do, I needed certain box keys to alter dynamically
        // NOTE *** rotation is not added to boxes ***
        void UpdateBoxes(GameTime gameTime) // this is currently setup for primary animation [it's assumed the body will be relatively close to the same range as walking or running]
        {            
            if (stopped) return;
            float scale = rem_scale;            
            int a = animation_index;
            MeoAnimation anim = meo.anim[a];            
            int timer_frame = (int)((timer / anim.old_final_time) * 1000f); // figure out the meo-motion frame for this                                    
            for (int n = 0; n < anim.boxes.Count; n++) {                
                MeoBox mbox = anim.boxes[n];                                       // mbox will be the current animation box to work on
                mbox.current_box = Rectangle.Empty;
                if (timer_frame < mbox.first_key) continue;
                if (timer_frame > mbox.last_key)  continue;
                // find an index who's frame is less than the current time
                int p_ind = 0;
                for (int b = 0; b < mbox.box_keys.Count; b++) { // ( there's usually only a few rect keys per animation )
                    MeoBoxKey bkey = mbox.box_keys[b];          // let bkey be the current key           
                    if (bkey.frame > timer_frame) break;        // if its frame is beyond the timer_frame, it's the key just after the current-timeline-position
                    p_ind = b;                                  // it was not the key after so memorize this as the previous key for later   
                }
                // now we have the index of the one before the current time... (which it memorized and then it did a break when it went too far)
                int n_ind = p_ind + 1; // (note: technically next-key should be what b was when it did a break from loop)                   
                // figure out what percentage the timer_frame is between the 2 key frames
                if (n_ind >= mbox.box_keys.Count) n_ind = p_ind;
                float total_time_interval = (float)mbox.box_keys[n_ind].frame - (float)mbox.box_keys[p_ind].frame; // space between keys                
                if (total_time_interval < 0) Debug.WriteLine("warning - total_time_interval<0 in UpdateBoxes");   // maybe a bug 
                float tween_dist = (float)timer_frame - (float)mbox.box_keys[p_ind].frame;                         // space between previous key and timer_frame
                
                if (total_time_interval == 0) { total_time_interval = 0.0001f; }  // prevent possibility of div_by_zero error
                float tween_perc = tween_dist / total_time_interval;              // get percentage (as 0-1 where 1 is 100%)

                // calculate the current box shape:
                Rectangle rec1 = mbox.box_keys[p_ind].rec;
                Rectangle rec2 = mbox.box_keys[n_ind].rec;

                // mbox refers to anim.boxes[n] and so we store the interpolated current box or rectangle animation
                if (p_ind == n_ind) mbox.current_box = Rectangle.Empty;
                else { 
                    mbox.current_box.X      = (int)MathHelper.Lerp((float)rec1.X, (float)rec2.X, tween_perc);
                    mbox.current_box.Y      = (int)MathHelper.Lerp((float)rec1.Y, (float)rec2.Y, tween_perc);
                    mbox.current_box.Width  = (int)MathHelper.Lerp((float)rec1.Width, (float)rec2.Width, tween_perc);
                    mbox.current_box.Height = (int)MathHelper.Lerp((float)rec1.Height, (float)rec2.Height, tween_perc);
                }

                if (flip) { // flip horizontally:                 
                    mbox.current_box.X = -mbox.current_box.X - mbox.current_box.Width;                    
                }
            }            
        }
        #endregion



        #region D R A W   B O X E S       
        /// Optional: to show collision-detection boxes for debugging purposes (any scaling is already done in UpdateBoxes)        
        public void DrawBoxes(bool show_all_as_white = false, bool use_pixel_tex = true) // show_all_as_white can be used if the original color is hard to see or transparent
        {
            if (stopped) return;
            if (use_pixel_tex) batch.SwitchTex(Game1.pixel_tex, BlendState.NonPremultiplied);
            int a = animation_index;
            MeoAnimation anim = meo.anim[a];


            for (int n = 0; n < anim.boxes.Count; n++) {
                MeoBox mbox = anim.boxes[n];                                       // mbox will be the current animation box to work on
                if ((mbox.current_box.Width < 1) || (mbox.current_box.Height < 1)) continue; // skip it - this won't be detectable
                Color box_color = mbox.color;
                if (show_all_as_white) box_color = Color.White;                    // override color (if using) 
                if (PREMULTIPLY_ALPHA) box_color = Color.FromNonPremultiplied(box_color.ToVector4());
                batch.RectLines(mbox.current_box, position, box_color, 2);
            }                      
            if (use_pixel_tex) batch.RestoreTex();
        }
        #endregion


        
        #region H I T S  and   W A S   H I T   B Y  ------------- ( C O L L I S I O N   B O X   S T U F F  ) --------------------------------
        /// <summary> Test if attack-box hits something. ie: if (monster.hits(player.body_rect)) player_ouch(); </summary>       
        public bool hits(Rectangle rec_to_hit, int attack_box_type = 1) {
            if (in_box(rec_to_hit, attack_box_type)) return true;
            return false;
        }
        /// <summary> Test if any box collides with character's body. ie: if (monster.was_hit_by(player_attack_rec)) Ouch(); </summary>        
        public bool was_hit_by(Rectangle rec, int box_type = 0) {
            if (in_box(rec, box_type)) return true;
            return false;
        }


        bool in_box(Rectangle rc, int box_type)
        {
            // loop through boxes for this animation and see if any are attack boxes... then see if we have a collision            
            for(int bx=0; bx<meo.anim[animation_index].boxes.Count; bx++) {
                if (meo.anim[animation_index].boxes[bx].bbox_type_id == box_type) {
                    ref Rectangle r = ref meo.anim[animation_index].boxes[bx].current_box;
                    int L = r.X + (int)position.X, R = L + r.Width, T = r.Y + (int)position.Y, B = T + r.Height;
                    int uL = rc.X, uR = rc.X + rc.Width, uT = rc.Y, uB = rc.Y + rc.Height;
                    //Report("L:" + L + "  R:" + R + "  T:" + T + "  B:" + B);
                    if ((uL < R) && (uR > L) && (uT < B) && (uB > T)) return true;                    
                }
            }
            return false;
        }
        #endregion


        void Report(string str) { Debug.WriteLine(str);  }
    }
}

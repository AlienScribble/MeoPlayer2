using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

/// UPDATED
/// [ Still reverse and forward compatible (old files work in new code and new files work in old code) ]
/// mix animation support in new MeoPlayer (main body animation but allow legs to move independently), so added a mix_flag
/// also added: old_anim_time which is the originally loaded final time - this is useful for bbox timing conversion


namespace MeoMo {
    // Holds info about sprite parts
    public class SpritePart
    {
        public string    name;
        public Rectangle rect;           // source rectangle on sprite sheet     
        public Vector2   pivot;          // pivot/origin to rotate around
        public int       parent;         // what sprite was the parent (useful when programming vector tracking [like head looking or pointing weapon] )
        public Vector2   m1, m2, m3, m4; // model points around origin(0,0) before transforms
        public bool      additive;       // tagged for additive blending         
        /// N E W :
        public bool      mix_flag;       /// if true, secondary animation (ie: walk) is used instead for this part (ie: leg)
    }


    // Holds an individual key for a part in an animation
    public class Key
    {
        public int     part;             // which actual sprite part is to be rendered
        public int     order;            // index for draw order
        public Vector2 scale;            // sprite size
        public float   rot;              // sprite rotation 
        public Vector2 pos;              // sprite position
        public float   alpha;            // transparency
        public Vector2 o1, o2, o3, o4;   // vertex offsets/distortions
        public bool    active;           // active (for hiding parts not animated)
        public float   red, green, blue; // color blending
    }


    // Holds an animation (and all key and part manipulation info) 
    public class MeoAnimation
    {
        public string animation_name;    // used when need to identify which animation index to use (using dictionary)
        public int    num_keys;          // total keys in this animation sequence
        public bool   looping;           // is a loop type animation? 
        public int[]  times;             // time of key_index
        public Key[,] keys;              // keyframes (part index, key index)
        public int    key1;              // current key
        public int    key2;              // next key 
        public int    root;              // index of root bone (could be useful) 
        public int    start_part;        // section of parts this animation works on
        public int    end_part;          // "                                      "
        public float  timer;             // used in the example Play() method for interpolating between keys

        // add customization properties here:
        public float   speed    = 1.0f;  // 100%
        public Vector2 offset   = Vector2.Zero;
        
        /// N E W : 
        public List<MeoBox>boxes;        // animatable bounding boxes which can be used to detect collisions, attack-regions, etc... 
        public float old_final_time;     // added - ( b-box animations need original time of last key to convert to 0-1000 properly )

        // You could use this to trim any unused animation (ie: n =- 300 would make unlooped animation end 300 sooner (timeline is set for 0-1000)) 
        public void Adjust_Last_Key_Time(int n) {
            times[num_keys - 1] += n;
        }
    }


    // Holds final rendering data for the Draw method
    public class Final
    {
        public int     order;            // which index to use ( drawing order )
        public int     part;             // index of actual part/rect to render
        public float   alpha;            // transparency
        public Vector2 v1, v2, v3, v4;   // transformed vertices
        public bool    hide;             // don't draw? 
        public float   red, green, blue; 
    } 


    /// a d d e d - - - - - - - - - - - - - - - - - - 
    /// BOUNDING BOX DATA (if using) 
    public class MeoBoxKey {
        public int       prev_key, next_key, frame; 
        public Rectangle rec;        
    }    
    public class MeoBox {
        public string name;                // name could be used to communicate more about the effect of the box (what's if for?)
        public Color  color;               // just used to identify box type
        public int    first_key, last_key; // first key the box animation starts and and last key it ends at on the timeline
        public int    bbox_type_id;        // type id (ie: an attack type or body type or some other custom type - used to communicate what the box intended use is)
        public List<MeoBoxKey> box_keys;   // rectangle, current frame(index for key), previous key, next key     
        public Rectangle current_box;      // current animated box shape
    }
    /// - - - - - - - - - - - - - - - - - - - - - - -



    //----------------------------------------------
    // * * *  C L A S S    M E O M O T I O N  * * *
    //----------------------------------------------
    public class MeoMotion
    {
        GraphicsDevice gpu;
        ContentManager content;            
        public  Texture2D tex;
        public  int num_parts, max_parts, total_animations; // Note: max_parts can be used elsewhere to allocate a memory pool for final renders (see NPC.cs)
        Final[] final;                  // used in final rendering of animated parts     
        public  SpritePart[]   parts;   // sprite part list
        public  MeoAnimation[] anim;    // all the animations
        public  Dictionary<string, int> lookup = new Dictionary<string, int>(); // used to find animation index of a named animation
       

        #region CONSTRUCTOR
        // C O N S T R U C T O R ------------------
        public MeoMotion(ContentManager _content, GraphicsDevice GPU)
        {
            content   = _content;
            gpu       = GPU;
            max_parts = 0;
        }
        #endregion



        #region L O A D   T X T   F R O M   C O N T E N T         
        /// <summary> Load's MeoMotion txt file from a folder in Content.  ie: Dog/dog.txt   (set file to copy and no processing required) </summary>
        /// <param name="imageFolder">folder (only) of png file.  ie: "Cat/" </param>
        /// <param name="txtFilePath">folder and/or filename of txt file.  ie: "Cat/cat.txt" </param>
        /// <param name="rescale">can be used to adjust scale for resolution</param>
        /// <param name="adjustOrder">DON'T USE. This option only exists for a different customized player if you wanted to make one.</param>        
        public void Load_TXT_From_Content(string imageFolder, string txtFilePath, bool scale_for_resolution = true, bool adjustOrder = false)
        {
            string filename = txtFilePath;
            if (!filename.EndsWith(".txt")) filename += ".txt";
            string s = Path.Combine(Path.Combine(Environment.CurrentDirectory, "Content"), filename);
            if (!File.Exists(s)) {
                s = filename;
                Debug.Assert(File.Exists(s), "Could not find the file: " + s);                
            }            
            Vector2 adjust_scale = Vector2.One;
            //if (scale_for_resolution) Adjust_Scale_For_ScreenResolution(ref adjust_scale, gpu.Viewport.Width, gpu.Viewport.Height);
            Load_TXT(imageFolder, s, adjust_scale, adjustOrder);
        }
        #endregion



        // L O A D  _  T X T -------------------------------------------------------------------------------------------------------------
        /// NEED TO: place the export .TXT file in your project (in BIN and then in x86 and then in debug - or whatever your build place is)
        /// -- apon final release of your project you'll need to make sure this exported file is in the same folder as the executable
        /// [ You can specify export folders in MeoMotion using export options ]
        // rescale - to resize the how big the sprites will appear
        public void Load_TXT(string ImagePath, string Filename, Vector2 rescale, bool adjustOrder = false)
        {
            int a = -1;            
            if (!Filename.EndsWith(".txt")) Filename += ".txt";
            if (!File.Exists(Filename)) { Debug.WriteLine("File not found: " + Filename); return; }
            using (StreamReader reader = new StreamReader(Filename))
            {                
                string anim_name = "";                
                string line = reader.ReadLine(); string[] strs;
                strs = line.Split(','); 
                if ((strs[0] != "SPRITESHEET_FILENAME")&&(strs[0]!="COMBO_FILENAME")) { Debug.WriteLine("Data="+strs[0]); Debug.WriteLine("\n Unexpected first line in " + Filename + " while trying to load as .TXT MeoMotion file."); return; }
                string image_filename = Path.GetFileName(strs[1]);
                image_filename = ImagePath + Path.GetFileNameWithoutExtension(image_filename);

                tex = content.Load<Texture2D>(image_filename);  // load actual spritemap
                // continue loadint txt:
                int current_root = 0, part_count = 0;
                if (strs[0] == "COMBO_FILENAME")
                {
                    num_parts        = Convert.ToInt32(strs[4]);
                    total_animations = Convert.ToInt32(strs[5]);
                } else 
                {
                    line = reader.ReadLine(); strs = line.Split(','); // reading TOTAL_NUM_PARTS
                    num_parts = Convert.ToInt32(strs[1]); part_count = num_parts; if (part_count > max_parts) max_parts = part_count;
                    line = reader.ReadLine(); strs = line.Split(','); // reading ROOT_INDEX
                    current_root = Convert.ToInt32(strs[1]);
                    line = reader.ReadLine(); strs = line.Split(','); // reading TOTAL_ANIMATIONS
                    total_animations = Convert.ToInt32(strs[1]);
                }
                parts = new SpritePart[num_parts];                   //allocate parts
                anim  = new MeoAnimation[total_animations];          //allocate animations                 

                int p = 0, pi = -1, first_index = 0;  // part index, timeline part index, first index within a sheet section
                int k = 0, ba = -1;                   // key index, b-box animation index
                MeoBox    mbox    = new MeoBox();     
                MeoBoxKey mbx_key = new MeoBoxKey();
                string current_sheetname = "";        // used when sheet data has been combined into one sheet (old sheet names - used for looking up animations which use different sections of a spritemap) 
                bool first = false;
                ba = -2; // just make sure it's not 0 to start with
                do
                {
                    line = reader.ReadLine(); strs = line.Split(',');
                    switch (strs[0])
                    {
                        case "SPRITESHEET_FILENAME": current_sheetname = Path.GetFileNameWithoutExtension(strs[1]); first = true; break; // SET FIRST so we know to set the first index
                        case "TOTAL_NUM_PARTS": part_count   = Convert.ToInt32(strs[1]); if (part_count > max_parts) max_parts = part_count; break;
                        case "ROOT_INDEX":      current_root = Convert.ToInt32(strs[1]);  break;
                        case "PART_INDEX":  p = Convert.ToInt32(strs[1]); if (p >= num_parts) { Debug.WriteLine("Meo file integrity problem: part index p>=part_count"); return; }
                            if (first) { first = false; first_index = p; } // record the first index if this is the first entry of this sheet section 
                            break;
                        case "PART_NAME":      parts[p] = new SpritePart(); parts[p].name = strs[1]; parts[p].additive = false;  break;
                        case "PART_RECTANGLE": parts[p].rect.X = Convert.ToInt32(strs[1]); parts[p].rect.Y = Convert.ToInt32(strs[2]); parts[p].rect.Width = Convert.ToInt32(strs[3]); parts[p].rect.Height = Convert.ToInt32(strs[4]); break;
                        case "LOCAL_POINTS_M1M2M3M4":
                            parts[p].m1.X = Convert.ToSingle(strs[1]) * rescale.X; parts[p].m1.Y = Convert.ToSingle(strs[2]) * rescale.Y;
                            parts[p].m2.X = Convert.ToSingle(strs[3]) * rescale.X; parts[p].m2.Y = Convert.ToSingle(strs[4]) * rescale.Y;
                            parts[p].m3.X = Convert.ToSingle(strs[5]) * rescale.X; parts[p].m3.Y = Convert.ToSingle(strs[6]) * rescale.Y;
                            parts[p].m4.X = Convert.ToSingle(strs[7]) * rescale.X; parts[p].m4.Y = Convert.ToSingle(strs[8]) * rescale.Y;
                            parts[p].pivot = -parts[p].m1;  break;
                        case "PART_PIVOT":  break; // version of pivot used in original (not used in game) 
                        case "PART_PARENT": parts[p].parent   = Convert.ToInt32(strs[1]) + first_index;  break; // offset by first index
                        case "ADDITIVE":    parts[p].additive = true;  break; 
                        //------------------------------------------------------------------------
                        case "ANIMATION_NAME": anim_name = strs[1]; break; 
                        case "ANIMATION_NUMBER":
                            a++;
                            if (a >= total_animations) { Debug.WriteLine("Error: animation index a>=total_animations"); return; }
                            anim[a] = new MeoAnimation();
                            anim[a].animation_name = anim_name;
                            anim[a].looping = false;
                            anim[a].root = current_root + first_index;   // offset by first index
                            anim[a].start_part = first_index;
                            anim[a].end_part = first_index + part_count;
                            lookup.Add(current_sheetname + anim_name, a);
                            anim[a].key1 = 0; anim[a].key2 = 0; anim[a].timer = 0; pi=-1; k=0;
                            anim[a].boxes = new List<MeoBox>();          // added for bounding box detections 
                            break;
                        case "ANIMATION_KEY_COUNT":
                            anim[a].num_keys = Convert.ToInt32(strs[1]);
                            anim[a].keys  = new Key[num_parts, anim[a].num_keys]; // allocate keys for this animation
                            anim[a].times = new int[anim[a].num_keys];            // allocate times
                            break;
                        case "KEY": k = Convert.ToInt32(strs[1]); pi = -1; break;
                        case "LOOPING": anim[a].looping = true; break;
                        case "TIME": anim[a].times[k] = Convert.ToInt32(strs[1]); anim[a].old_final_time = anim[a].times[k]; break;
                        case "PART": pi++; anim[a].keys[pi, k] = new Key();
                            anim[a].keys[pi, k].part = Convert.ToInt32(strs[1]) + first_index; // offset by first_index
                            anim[a].keys[pi, k].active = true;
                            anim[a].keys[pi, k].red = 1f; anim[a].keys[pi, k].green = 1f; anim[a].keys[pi, k].blue = 1f; anim[a].keys[pi,k].alpha=1f;
                            break;
                        case "ORDER": anim[a].keys[pi, k].order = Convert.ToInt32(strs[1]);
                            if (adjustOrder) anim[a].keys[pi, k].order += first_index;         
                            break;
                        case "NOT_ACTIVE": anim[a].keys[pi, k].active = false; break;
                        case "K_SCALE":    anim[a].keys[pi, k].scale.X = Convert.ToSingle(strs[1]); anim[a].keys[pi, k].scale.Y = Convert.ToSingle(strs[2]); break;
                        case "K_ROT":   anim[a].keys[pi, k].rot = Convert.ToSingle(strs[1]); break;
                        case "K_POS":   anim[a].keys[pi, k].pos.X = Convert.ToSingle(strs[1]) * rescale.X; anim[a].keys[pi, k].pos.Y = Convert.ToSingle(strs[2]) * rescale.Y; break;
                        case "K_ALPHA": anim[a].keys[pi, k].alpha = Convert.ToSingle(strs[1]); break;
                        case "K_VERT_OFF1": anim[a].keys[pi, k].o1.X = Convert.ToSingle(strs[1]) * rescale.X; anim[a].keys[pi, k].o1.Y = Convert.ToSingle(strs[2]) * rescale.Y; break;
                        case "K_VERT_OFF2": anim[a].keys[pi, k].o2.X = Convert.ToSingle(strs[1]) * rescale.X; anim[a].keys[pi, k].o2.Y = Convert.ToSingle(strs[2]) * rescale.Y; break;
                        case "K_VERT_OFF3": anim[a].keys[pi, k].o3.X = Convert.ToSingle(strs[1]) * rescale.X; anim[a].keys[pi, k].o3.Y = Convert.ToSingle(strs[2]) * rescale.Y; break;
                        case "K_VERT_OFF4": anim[a].keys[pi, k].o4.X = Convert.ToSingle(strs[1]) * rescale.X; anim[a].keys[pi, k].o4.Y = Convert.ToSingle(strs[2]) * rescale.Y; break;
                        case "K_RGB": anim[a].keys[pi, k].red = Convert.ToSingle(strs[1]); anim[a].keys[pi, k].green = Convert.ToSingle(strs[2]); anim[a].keys[pi, k].blue = Convert.ToSingle(strs[3]); break;
                        /// N E W :
                        case "BOX_ANIM_INDEX":
                            //ba = Convert.ToInt32(strs[1]);                  // note: not all animations will have MeoBox data
                            if (ba != a) { // don't add new set if it's part of the same animation 
                                ba = a; // note that the index should match up to current animation
                                anim[ba].boxes = new List<MeoBox>();
                                if (ba > a) { Console.WriteLine("Warning: BOX_ANIM_INDEX > a (current animation count)"); }
                            }
                            break;
                        case "BOX_INDEX": mbox = new MeoBox(); anim[ba].boxes.Add(mbox); break; // not sure if we need bi (because we're referencing everything)
                        case "BOX_NAME":  mbox.name = strs[1]; break;
                        case "BOX_TYPE":  mbox.bbox_type_id = Convert.ToInt32(strs[1]);
                            switch(mbox.bbox_type_id) {
                                case 0: mbox.color = Color.White;     break;
                                case 1: mbox.color = Color.Red;       break;
                                case 2: mbox.color = Color.LimeGreen; break;
                                default: mbox.color = Color.LightYellow; break;
                            }
                            break;
                        case "BOX_FIRST_KEY": mbox.first_key = Convert.ToInt32(strs[1]); break;
                        case "BOX_LAST_KEY":  mbox.last_key = Convert.ToInt32(strs[1]); break;
                        case "BOX_KEY_COUNT": mbox.box_keys = new List<MeoBoxKey>(); break;
                        case "B_K_INDEX":    mbx_key = new MeoBoxKey(); mbox.box_keys.Add(mbx_key); break;
                        case "B_K_FRAME":    mbx_key.frame = Convert.ToInt32(strs[1]); break;
                        case "B_K_PREV_KEY": mbx_key.prev_key = Convert.ToInt32(strs[1]); break;
                        case "B_K_NEXT_KEY": mbx_key.next_key = Convert.ToInt32(strs[1]); break;
                        case "B_K_RECT": mbx_key.rec.X      = (int)((float)Convert.ToInt32(strs[1]) * rescale.X);
                                         mbx_key.rec.Y      = (int)((float)Convert.ToInt32(strs[2]) * rescale.Y);
                                         mbx_key.rec.Width  = (int)((float)Convert.ToInt32(strs[3]) * rescale.X);
                                         mbx_key.rec.Height = (int)((float)Convert.ToInt32(strs[4]) * rescale.Y);
                            break;
                    }
                } while (reader.EndOfStream != true); 
            }//using reader
            if (total_animations > a) total_animations = a;
            //precautionary setting of second key (in case no other keys):
            a = 0;
            do { if (anim[a].num_keys > 1) anim[a].key2 = 1; else anim[a].key2 = 0; a++; } while (a < total_animations);
            final = new Final[num_parts];
            a = 0;
            do { final[a] = new Final(); a++; } while (a < num_parts);
            ////UNCOMMENT EACH IF NEED TO DIAGNOSE:
            // foreach (KeyValuePair<string, int> kvp in lookup) { Debug.WriteLine(string.Format("Key = {0}, Value = {1}", kvp.Key, kvp.Value)); }            
            // DumpBBOX_Data();
        }// Load_TXT



        // G E T  I N D E X --------------------------------------
        // finds the animation index of a named animation
        public int GetIndex(string sheetname, string animation_name, bool show_error_messages = true)
        {
            int value;
            if (!lookup.TryGetValue(sheetname + animation_name, out value)) {
                if (show_error_messages) Debug.WriteLine("Animation not found in dictionary: " + sheetname + animation_name);               
                return 0;
            }
            return value; 
        }



        // ADJUST SCALE FOR SCREEN RESOLUTION
        // a tool to scale position, size, and speed of characters to match display resolutions
        public void Adjust_Scale_For_ScreenResolution(ref Vector2 rescale, int width, int height) {
            // Assuming default is 800 x 600 (can change this)
            float DEFAULT_W = 800, DEFAULT_H = 600;
            rescale.X = (rescale.X * width)  / DEFAULT_W;
            rescale.Y = (rescale.Y * height) / DEFAULT_H;
        }




        // this is temporary for debugging purposes:
        void DumpBBOX_Data()
        {
            for (int a=0; a<total_animations; a++) {
                Debug.WriteLine("anim["+a+"].boxes.Count: " + anim[a].boxes.Count);
                for (int i=0; i<anim[a].boxes.Count; i++) {
                    MeoBox mbox = anim[a].boxes[i];
                    Debug.WriteLine("NAME: " + mbox.name + "  id:" + mbox.bbox_type_id + "  col:" + mbox.color.ToString() + "  1st:" + mbox.first_key + "  2nd:" + mbox.last_key);
                    for (int b = 0; b < mbox.box_keys.Count; b++) {
                        MeoBoxKey bkey = mbox.box_keys[b];
                        Debug.WriteLine("FRAME: " + bkey.frame + "   pkey:" + bkey.prev_key + "   nkey:" + bkey.next_key + "    rec:" + bkey.rec.ToString());
                    }
                }
            }
        }
    }
}

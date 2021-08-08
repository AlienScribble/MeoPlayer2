using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MeoPlayer_V2_Test {
    // I n p u t   H e l p e r s
    public class Input {               
        public KeyboardState ks, oks;
        public MouseState    ms, oms;        

        public bool Keypress(Keys k) { if (ks.IsKeyDown(k) && oks.IsKeyUp(k)) return true; return false; }
        public bool Keydown(Keys k)  { if (ks.IsKeyDown(k)) return true; return false; }  
        

        public void Update()
        {
            oks = ks; ks = Keyboard.GetState();
            oms = ms; ms = Mouse.GetState();           
        }
    }
}

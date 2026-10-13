using System;
namespace Hlc.ChillPrototype
{
    // Session object readiness from HeroineDrinks, WindowController and MemorieController.
    // Timing is explicit so cancellation/reactions cannot reset object cooldowns.
    public sealed class ChillActivityObjects
    {
        public int drinks=5;
        public bool cold,windowOpen;
        public double lastPour=-3600,nextWindow,nextMusicbox;
        bool firstWindow=true;
        int nextPour;
        public bool Empty=>drinks<=0;
        public bool Hot(double now)=>!cold&&drinks==5&&now-lastPour<600;
        public bool CanRefill(double now)=>Empty&&now-lastPour>=3600;
        public bool NeedCold(double now,float draw,float chance)
        {if(!CanRefill(now))return false;if(nextPour==0)nextPour=draw<chance?2:1;return nextPour==2;}
        public bool NeedHot(double now,float draw,float chance)=>CanRefill(now)&&!NeedCold(now,draw,chance);
        public void Drink(){drinks=Math.Max(0,drinks-1);}
        public void Pour(double now,bool isCold){drinks=5;lastPour=now;cold=isCold;nextPour=0;}
        public void Window(bool open,double now,float draw)
        {windowOpen=open;nextWindow=now+(firstWindow?60+30*draw:180+60*draw)*60;firstWindow=false;}
        public void Musicbox(double now,float draw)=>nextMusicbox=now+(20+40*draw)*60;
    }
}

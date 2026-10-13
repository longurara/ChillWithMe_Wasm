namespace Hlc.ChillPrototype
{
    // ScenarioReader.CommandText calls AutoNext(Arg2) only after both the
    // typewriter and voice finish. Loading or speaking cannot consume this wait.
    public sealed class ChillAutoAdvanceGate
    {
        double readyAt=-1;
        public bool Ready(bool completed,double now,float waitSeconds)
        {
            if(!completed){readyAt=-1;return false;}
            if(readyAt<0)readyAt=now;
            return now-readyAt>=System.Math.Max(0,waitSeconds);
        }
    }
}

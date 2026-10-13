using System;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    // A scoped clock and random source let source behaviour be replayed without
    // changing the user's clock or Unity's global animation/particle randomness.
    public static class ChillRuntimeContext
    {
        static DateTimeOffset? instant;
        static System.Random random;
        public static DateTime LocalNow => instant?.LocalDateTime ?? DateTime.Now;
        public static double Now => (instant ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds()/1000.0;
        public static float Value => random == null ? UnityEngine.Random.value : (float)random.NextDouble();
        public static int Range(int min,int max) => random == null ? UnityEngine.Random.Range(min,max) : random.Next(min,max);
        public static float Range(float min,float max) => random == null ? UnityEngine.Random.Range(min,max) : min+(max-min)*Value;
        public static IDisposable Replay(DateTimeOffset at,int seed)
        {
            var scope=new Scope{previousInstant=instant,previousRandom=random};
            instant=at;random=new System.Random(seed);return scope;
        }
        public static void Advance(TimeSpan elapsed)
        {if(!instant.HasValue)throw new InvalidOperationException("No replay clock is active.");instant=instant.Value.Add(elapsed);}
        sealed class Scope:IDisposable
        {
            public DateTimeOffset? previousInstant;public System.Random previousRandom;bool disposed;
            public void Dispose(){if(disposed)return;disposed=true;instant=previousInstant;random=previousRandom;}
        }
    }
}

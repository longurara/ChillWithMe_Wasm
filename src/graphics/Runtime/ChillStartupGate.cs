using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    // The browser displays Boot's sprites/curves while Wasm and room data load.
    // Gameplay starts only after the original finish/fade sequence completes.
    public sealed class ChillStartupGate : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void ChillStartupReveal(string receiver);
        [DllImport("__Internal")] static extern void ChillStartupFailure(string message);
#endif
        public bool Complete { get; private set; }
        public bool Failed { get; private set; }
        bool requested;
        public void StartupComplete(){if(requested&&!Failed)Complete=true;}
        public void Fail(string message)
        {
            if(Complete||Failed)return;Failed=true;
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillStartupFailure(message);
#endif
        }
        public IEnumerator Reveal()
        {
            if(!requested&&!Failed)
            {
                requested=true;
#if UNITY_WEBGL && !UNITY_EDITOR
                ChillStartupReveal(gameObject.name);
#else
                yield return new WaitForSecondsRealtime(1.9f);if(!Failed)Complete=true;
#endif
            }
            while(!Complete&&!Failed)yield return null;
        }
    }
}

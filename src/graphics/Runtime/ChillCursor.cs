using UnityEngine;
using UnityEngine.EventSystems;
namespace Hlc.ChillPrototype
{
    // CursorService: UI keeps the default cursor; Satone uses talk/talk-block.
    // The recovered click texture is not used by the source UpdateCursor path.
    [DefaultExecutionOrder(300)]
    public sealed class ChillCursor:MonoBehaviour
    {
        public ChillGraphicsPlayer player;
        public Texture2D normal,talk,blocked;
        Texture2D current;
        ChillCharacterPicking picking;
        float nextHit;
        bool overSatone;
        void Awake(){Set(normal);}
        // Native CursorService reapplies this each frame. WebGL's visibility and
        // viewport changes can overwrite the CSS cursor, so do not cache away
        // that call when the texture itself has not changed.
        void Set(Texture2D texture){current=texture;Cursor.SetCursor(texture,new Vector2(7,0),CursorMode.Auto);}
        void LateUpdate()
        {
            if(!player||!player.story||!player.view||Input.touchCount>0)return;
            if(!player.story.FeatureReady||EventSystem.current&&EventSystem.current.IsPointerOverGameObject()){overSatone=false;Set(normal);return;}
            if(Time.unscaledTime>=nextHit)
            {
                nextHit=Time.unscaledTime+.1f;
                if(!picking)picking=GetComponent<ChillCharacterPicking>()??gameObject.AddComponent<ChillCharacterPicking>();
                overSatone=picking.Hit(player.character,player.view.ScreenPointToRay(Input.mousePosition));
            }
            var story=player.story;
            Set(!overSatone||story.Playing&&story.TutorialCommand!=14&&story.TutorialCommand!=50?normal:story.CanReactToPointer?talk:blocked);
        }
        void OnDisable(){current=null;Cursor.SetCursor(null,Vector2.zero,CursorMode.Auto);}
    }
}

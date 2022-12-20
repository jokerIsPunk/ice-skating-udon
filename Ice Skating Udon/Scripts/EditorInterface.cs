
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace jokerispunk.IceSkatingUdon
{
    // this class relays information input in the editor to the main programs
    //  just makes it easier for users so they don't need to dig around in the heirarchy
    // written by github.com/jokerispunk
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EditorInterface : BaseIceSkatingUdon
    {
        public string surfaceName = "ice";
        public LayerMask rayMask = 2049;
    }
}

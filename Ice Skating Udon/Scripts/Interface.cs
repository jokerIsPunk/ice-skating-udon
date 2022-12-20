
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace jokerispunk.IceSkatingUdon
{
    // this class relays information from other scripts
    //  it serves as the access point for external programs and for the user in-editor
    // written by github.com/jokerispunk
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Interface : BaseIceSkatingUdon
    {
        public string surfaceName = "ice";
        public LayerMask rayMask = 2049;

        [Header("internal references don't change")]
        [Space]
        [SerializeField] BackgroundLoop bg;

        private Skating skateLoop;
        private bool initialized = false;

        private void _Initialize()
        {
            if (initialized) return;

            skateLoop = bg.skateLoop;
            initialized = true;
        }

        public float _GetSpeed()
        {
            _Initialize();

            if (skateLoop)
                return skateLoop.momentum;

            return 0f;
        }

        public Vector3 _GetDirection()
        {
            _Initialize();

            if (skateLoop)
                return skateLoop.direction;

            return Vector3.forward;
        }

        public MoveState _GetMoveState()
        {
            _Initialize();

            if (skateLoop)
                return skateLoop.moveState;

            return MoveState.forward;
        }
    }
}

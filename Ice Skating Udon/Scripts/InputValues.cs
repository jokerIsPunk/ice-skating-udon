
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace jokerispunk.IceSkatingUdon
{
    // this class caches input values so that the current value can be read on any frame
    //  it also handles and executes custom jump events
    // written by github.com/jokerispunk
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class InputValues : BaseIceSkatingUdon
    {
        public float forward;
        [SerializeField] Skating skateLoop;
        private VRCPlayerApi lp;

        private void Start()
        {
            lp = Networking.LocalPlayer;
        }

        public override void InputMoveVertical(float value, UdonInputEventArgs args)
        {
            forward = value;
        }

        public override void InputJump(bool value, UdonInputEventArgs args)
        {
            if (value && lp.IsPlayerGrounded() && skateLoop.running)
            {
                Vector3 vel = lp.GetVelocity();
                vel.y += skateLoop.jumpCache;
                lp.SetVelocity(vel);
            }
        }
    }
}

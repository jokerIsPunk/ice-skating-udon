
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace jokerispunk.IceSkatingUdon
{
    // listens for a double-click event on screen or a double-trigger-pull in VR
    //  then forwards an event call
    // written by github.com/jokerispunk
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ToggleInput : BaseIceSkatingUdon
    {
        [SerializeField] UdonBehaviour eventReceiver;
        [SerializeField] string eventName = "_OnDoubleClick";
        private bool leftUse, rightUse;
        private float doubleClickEndtime, doubleClickLength = 0.5f;
        private float minClickTime, minClickLength = 0.05f;
        private bool vr;

        void Start()
        {
            vr = Networking.LocalPlayer.IsUserInVR();
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if (vr)
            {
                if (args.handType == HandType.LEFT)
                    leftUse = value;
                else if (args.handType == HandType.RIGHT)
                    rightUse = value;

                if (leftUse && rightUse)
                    _Event();
            }
            else
            {
                if (value)
                {
                    if ((Time.time > minClickTime) && (Time.time < doubleClickEndtime))
                    {
                        doubleClickEndtime = 0f;
                        minClickTime = 0f;
                        _Event();
                    }
                    else
                    {
                        doubleClickEndtime = Time.time + doubleClickLength;

                        // workaround for VRC calling InputUse twice per frame
                        minClickTime = Time.time + minClickLength;
                    }
                }
            }
        }

        private void _Event()
        {
            _LogMsg("{gameObject.name} - Detected custom input event! Calling event...");
            eventReceiver.SendCustomEvent(eventName);
        }
    }
}


using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

namespace jokerispunk.IceSkatingUdon
{
    // this class manages the active state and movement of the UI panel
    // written by github.com/jokerispunk
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PortableUI : BaseIceSkatingUdon
    {
        [SerializeField] float lerpSpeed = 16f;
        [SerializeField] Transform pivotTf, attachTf, canvasTf;
        [SerializeField] BackgroundLoop bgLoop;
        [SerializeField] Toggle allowOffIce;
        private bool vr;
        private bool attached;
        private VRCPlayerApi lp;
        private bool initialized = false;

        private void Start()
        {
            if (!attached)
                this.enabled = false;
        }

        private void _Initialize()
        {
            lp = Networking.LocalPlayer;
            vr = lp.IsUserInVR();
            if (!pivotTf)
                pivotTf = transform;

            initialized = true;
        }

        public void _Toggle()
        {
            // toggle while on ice, toggle off otherwise
            if ((allowOffIce && allowOffIce.isOn) || bgLoop._SurfaceCheck())
            {
                attached = !attached;
                this.enabled = attached;
            }
            else
            {
                if (this.enabled)
                    this.enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!initialized)
                _Initialize();

            pivotTf.SetPositionAndRotation(bgLoop.rayHit.point, _GetTrackingRotation());
            canvasTf.SetPositionAndRotation(attachTf.position, attachTf.rotation);
        }

        private void OnDisable()
        {
            canvasTf.localPosition = Vector3.zero;
            canvasTf.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            if (!this.enabled) return;

            // attach pivot to player, but skip rotation in screen mode for usability
            //  rotation will be set once in OnEnable
            pivotTf.position = bgLoop.rayHit.point;
            if (vr)
                pivotTf.rotation = _GetTrackingRotation();

            // lerp to attach point for smoothing
            Vector3 pos = Vector3.Lerp(canvasTf.position, attachTf.position, lerpSpeed * Time.deltaTime);
            Quaternion rot = Quaternion.Lerp(canvasTf.rotation, attachTf.rotation, lerpSpeed * Time.deltaTime);
            canvasTf.SetPositionAndRotation(pos, rot);
        }

        private Quaternion _GetTrackingRotation()
        {
            Vector3 headFwd = bgLoop.head.rotation * Vector3.forward;
            headFwd.y = 0f;
            return Quaternion.LookRotation(headFwd);
        }
    }
}

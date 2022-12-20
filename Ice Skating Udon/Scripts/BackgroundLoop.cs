
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace jokerispunk.IceSkatingUdon
{
    // this class manages the basic state of whether or not the player is on a skateable surface
    // written by github.com/jokerispunk
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class BackgroundLoop : BaseIceSkatingUdon
    {
        [SerializeField] EditorInterface iface;
        private LayerMask rayMask = 2049;
        [SerializeField] Skating skateLoop;
        private string surfaceName = "ice";
        private bool onSurfaceLast = false;
        private float rayOriginOffset = 0.2f;
        private float rayMaxDist = 25f;
        VRCPlayerApi lp;

        // spatial data
        [HideInInspector] public VRCPlayerApi.TrackingData head;
        [HideInInspector] public RaycastHit rayHit;

        void Start()
        {
            // copy input data from interface script
            if (iface)
            {
                surfaceName = iface.surfaceName;
                rayMask = iface.rayMask;
                skateLoop.rayMask = iface.rayMask;
                _LogMsg($"rayMask: {rayMask.value}");
            }
            else _LogWarn($"Missing reference {nameof(iface)}!");

            // cache local player
            lp = Networking.LocalPlayer;
        }

        private void Update()
        {
            _UpdateSpatialData();
            bool onSurface = _SurfaceCheck();

            if (onSurface)
            {
                // listen for state transition events
                if (!onSurfaceLast)
                    _OnSurfaceStart();

                // drive the main loop from here
                //  this eliminates any questions about execution order
                skateLoop._SkateLoop(head, rayHit.point);
            }
            else
            {
                if (onSurfaceLast)
                    _OnSurfaceEnd();
            }

            // store state for next frame
            onSurfaceLast = onSurface;
        }

        private void _UpdateSpatialData()
        {
            head = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        }

        public bool _SurfaceCheck()
        {
            // layermask is walkable environment generally; always expecting a hit; check to see if ground name matches the surface name
            bool envRaycast = Physics.Raycast(head.position, Vector3.down, out rayHit, rayMaxDist, rayMask);
            if (envRaycast)
                return rayHit.collider.name == surfaceName;

            return false;
        }

        private void _OnSurfaceStart()
        {
            skateLoop._Enable();
        }

        private void _OnSurfaceEnd()
        {
            skateLoop._Disable();
        }

        public void _DebugAccessor(SkateDebug debug)
        {
            debug.onSurfaceLast = onSurfaceLast;
        }

        public override void OnPlayerRespawn(VRCPlayerApi player)
        {
            // immobilize failsafe
            if (player.isLocal)
            {
                _LogMsg("Set Immobilize false on respawn.");
                lp.Immobilize(false);
            }
        }
    }
}

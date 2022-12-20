
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

namespace jokerispunk.IceSkatingUdon
{
    // this script manages various ways of visualizing data from the Skate script
    //  it copies the data from the Skate script via an accessor method
    // written by github.com/jokerispunk
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SkateDebug : BaseIceSkatingUdon
    {
        [SerializeField] Skating skateProgram;
        [SerializeField] BackgroundLoop bgLoop;

        // misc references to visualize
        public float stopAngleRange = 90f;

        // state logic data
        public float inflectionEndTime = 0f;
        public bool inflectingLast = false;
        public MoveState moveState = MoveState.forward;
        public bool onSurfaceLast = false;
        public bool hips;

        // motion data
        public float momentum;
        public Vector3 direction;
        public float drag;

        // spatial data
        public Vector3 groundPos;
        public VRCPlayerApi.TrackingData room;
        public Vector3 anterior;
        public Quaternion lpRotFromRoom, lpRotFromRoomLast;
        public float feetDistLast;
        public Vector3 lFootPos, rFootPos;
        public float heightCalibrationContinuous;
        public VRCPlayerApi.TrackingData head;

        [Header("output references")]
        public GameObject[] debugOutputs;
        public Transform debugLinesTf;
        private Vector3 hapticBump = new Vector3(0.7f, 1f, 90f);
        public Transform footHeightPlane, lfBall, rfBall, posBall;
        public Text dataScreen1, dataScreen2, logMessage;
        public LineRenderer dbgLMoment, dbgLAnt, dbgLnFtDist, /*dbgLRoom,*/ dbgLAngVel;
        public Transform angVelThreshLn;
        public LineRenderer stopRangeLine1, stopRangeLine2;
        public LineRenderer dbgLStopRef, dbgLPump, dbgLBladeDrift;
        private Vector3 dbgHipsLine;
        private Vector3 dbgLfPos3, dbgRfPos3;
        private float dbgHeightProp;
        private float dbgFeetDist;

        // local player
        private VRCPlayerApi lp;

        private void Start()
        {
            lp = Networking.LocalPlayer;
        }

        private void Update()
        {
            // copy all the most recent data
            skateProgram._DebugAccessor(this);
            bgLoop._DebugAccessor(this);

            // text
            dataScreen1.text =
            $"onIceLast: {onSurfaceLast}\n" +
            $"inflecting: {inflectingLast}\n" +
            $"movestate: {moveState}\n" +
            $"drag: {drag}\n" +
            $"height: {heightCalibrationContinuous}\n" +
            $"momentum: {momentum}\n" +
            $"hips: {hips}\n";

            posBall.position = groundPos;

            if (onSurfaceLast)
            {
                // attach the debug lines pivot to the player
                //  and then overwrite the debug lines' inherited rotation to match blade direction
                debugLinesTf.parent.SetPositionAndRotation(groundPos, lp.GetRotation());
                debugLinesTf.rotation = Quaternion.LookRotation(anterior);

                // draw stop angle ranges
                float stopRange = stopAngleRange / 2;
                Vector3 rangeRot = new Vector3(0f, stopRange, 0f);
                //float stopRangeSin = Mathf.Sin(stopRange);
                //float stopRangeCos = Mathf.Cos(stopRange);
                //Vector3 stopRangeACW = new Vector3(stopRangeCos, 0f, stopRangeSin);
                //Vector3 stopRangeCW = new Vector3(stopRangeCos, 0f, -stopRangeSin);
                //stopRangeLine1.SetPosition(0, -stopRangeACW);
                //stopRangeLine1.SetPosition(1, stopRangeACW);
                //stopRangeLine2.SetPosition(0, -stopRangeCW);
                //stopRangeLine2.SetPosition(1, stopRangeCW);
                stopRangeLine1.transform.localEulerAngles = rangeRot;
                stopRangeLine2.transform.localEulerAngles = -rangeRot;

                // momentum and momentum direction
                dbgLMoment.SetPosition(0, debugLinesTf.position);
                dbgLMoment.SetPosition(1, debugLinesTf.position + (direction * momentum));

                // position balls
                lfBall.position = lFootPos;
                rfBall.position = rFootPos;
            }
            else
            {
                // set outputs to zero
            }
        }

        public void _PlatformInfo()
        {
            _LogMsg($"hips: {skateProgram.hips}, vr: {skateProgram.vr}");
        }
    }
}

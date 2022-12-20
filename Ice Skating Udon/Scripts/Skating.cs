
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace jokerispunk.IceSkatingUdon
{
    // this class holds all the logic and data for modeling skating
    //  it has no update loop and instead its main method is called each frame by the background loop
    // written by github.com/jokerispunk
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Skating : BaseIceSkatingUdon
    {
        // external references
        [SerializeField] InputValues input;
        [SerializeField] Effects fx;

        // internal state
        private bool initialized = false;

        // skating state logic references
        [HideInInspector] public bool running = false;
        public float inflectionThreshold = 270f;
        public float inflectionPeriod = 0.4f;
        [HideInInspector] public LayerMask rayMask = 2049;

        // motion references
        public float stopAngleRange = 90f;
        public float stopMinMomentum = 0.05f;
        [Range(0.1f, 8f)] public float accelScale = 2f;
        [Range(0.5f, 3f)] public float dragStopping = 1.0f;
        [Range(0.01f, 0.5f)] public float dragMoving = 0.05f;
        [Range(0f, 1f)] public float forwardBias = 0.5f;
        [Range(0.05f, 0.4f)] public float footHeightThresholdPortion = 0.2f;
        [Range(0.01f, 1f)] public float surfaceHardness = 0.1f;
        [Range(0f, 1f)] public float inputReversePortion = 0.3f;
        private float heightChangeDeltaThreshold = 0.1f;

        // skating state logic data
        [HideInInspector] public bool vr;
        private float inflectionEndTime = 0f;
        private bool inflectingLast = false;
        private MoveState moveState = MoveState.forward;
#if UNITY_ANDROID
        [HideInInspector] public bool hips = false;
#else
        [HideInInspector] public bool hips = true;
#endif

        // motion data
        [HideInInspector] public float momentum;
        [HideInInspector] public Vector3 direction;
        [HideInInspector] public float drag;

        // spatial data
        private Vector3 groundPos;
        private VRCPlayerApi.TrackingData room;
        private Vector3 anterior;
        private Quaternion lpRotFromRoom, lpRotFromRoomLast;
        private float feetDistLast;
        private Vector3 lFootPos, rFootPos;
        private float heightCalibrationContinuous;
        private VRCPlayerApi.TrackingData head;

        // vrc player data cache
        [HideInInspector]
        public float walkCache, strafeCache, runCache, jumpCache;
        private VRCPlayerApi lp;

        // refs and data for IK workaround
        [SerializeField] float immobileTurnThreshold = 10f;
        [SerializeField] float postureAngleThreshold = 5f;
        private Vector3 headRotRef = Vector3.forward;
        private bool pauseImmobile = false;
        private Vector3 roomPosRef = Vector3.zero;

        private void Start()
        {
            // script has no update loops to run and does no networking, so it can always be disabled
            this.enabled = false;
        }

        private void _Initialize()
        {
            // initialize local player
            lp = Networking.LocalPlayer;
            vr = lp.IsUserInVR();

            initialized = true;
        }

        public void _Enable()
        {
            // review all the data fields and decide if they need to be initialized here
            if (!initialized)
                _Initialize();

            // initialize momentum from velocity in the horizontal plane
            Vector3 vel = lp.GetVelocity();
            Vector3 vel2D = new Vector3(vel.x, 0f, vel.z);
            momentum = vel2D.magnitude;
            direction = vel2D.normalized;
            lp.SetVelocity(new Vector3(0f, vel.y, 0f));

            // Immobilize to avoid IK drift!
            lp.Immobilize(true);
            _LogMsg("<color=yellow>Immobilizing player...</color>");

            // cache playercontroller values for later reversion and set to 0
            //  needed to prevent non-fbt users from moving during IK immobilization pause
            walkCache = lp.GetJumpImpulse();
            strafeCache = lp.GetStrafeSpeed();
            runCache = lp.GetRunSpeed();
            jumpCache = lp.GetJumpImpulse();
            lp.SetWalkSpeed(0f);
            lp.SetStrafeSpeed(0f);
            lp.SetRunSpeed(0f);
            lp.SetJumpImpulse(0f);

            // initialize spatial data
            _UpdateSpatialData();
            _UpdateFeetData();
            feetDistLast = _GetFeetDistance();

            // use initial momentum and anterior values to initialize move state
            _UpdateMoveState();
            _ApplyMoveState();

            // enable sfx and vfx
            if (fx)
                fx.gameObject.SetActive(true);

            // start listening for inflection start event
            inflectingLast = false;
            inflectionEndTime = 0f;

            // state logic flag
            running = true;
        }

        public void _Disable()
        {
            // revert immobile
            lp.Immobilize(false);

            // translate momentum
            Vector3 vel = (direction * momentum);
            vel.y += lp.GetVelocity().y;
            lp.SetVelocity(vel);

            // revert playercontroller values
            lp.SetWalkSpeed(walkCache);
            lp.SetStrafeSpeed(strafeCache);
            lp.SetRunSpeed(runCache);
            lp.SetJumpImpulse(jumpCache);

            // stop effects
            if (fx)
            {
                fx._SetMoveFX(MoveState.forward);
                fx.gameObject.SetActive(false);
            }

            // stop update loop
            running = false;
            //this.enabled = false;
        }

        public void _SkateLoop(VRCPlayerApi.TrackingData headArg, Vector3 pos)
        {
            // cache data
            head = headArg;
            groundPos = pos;
            _UpdateSpatialData();

            // move fx parent and manage audio
            if (fx)
            {
                fx.transform.position = groundPos;
                fx._PitchAndVolume(Mathf.Abs(momentum), moveState);
            }

            // manage momentum and direction; drag always applies
            _MomentumAndDirection();

            // check for vertical obstacles
            _Obstacle();

            // do lerped teleport for custom locomotion
            _Motion();

            // store data for next frame; determines change over time
            _StoreSpatialData();

            // check for whether to pause immobilization for 1 frame to fix IK
            _CheckIKRotation();
        }

        private void _Obstacle()
        {
            float obstacleDist = 0.25f;
            Vector3 rayDir = direction * Mathf.Sign(momentum);
            bool obstacle = Physics.Raycast(head.position, rayDir, out RaycastHit obstacleHit, obstacleDist, rayMask);

            if (obstacle)
                momentum = 0.1f;
        }

        private void _UpdateSpatialData()
        {
            // order matters with these values; later values derive from earlier ones
            room = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
            head = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            _UpdateAnterior();
            _UpdateRotationFromRoom();
            _CalibrateHeight();
        }

        private void _UpdateAnterior()
        {
            // for 3-point tracking
            if (!hips)
            {
                anterior = _GetPlayerForward();
                return;
            }

            // check for missing hip bones i.e. nonhumanoid skeleton
            Vector3 lHipPos = lp.GetBonePosition(HumanBodyBones.LeftUpperLeg);
            if (lHipPos == Vector3.zero)
            {
                anterior = _GetPlayerForward();
                return;
            }
            //
            Vector3 rHipPos = lp.GetBonePosition(HumanBodyBones.RightUpperLeg);
            if (rHipPos == Vector3.zero)
            {
                anterior = _GetPlayerForward();
                return;
            }

            // the line between hips, from left to right
            Vector3 hipsLineRight = rHipPos - lHipPos;

            // the line anticlockwise perpendicular from hips line, on the XZ plane, length 1
            Vector3 anteriorVector = new Vector3(-hipsLineRight.z, 0, hipsLineRight.x);
            anterior = anteriorVector.normalized;
        }

        // get a line length 1 on the XZ plane representing the playercontroller's forward direction
        private Vector3 _GetPlayerForward()
        {
            Vector3 dir = head.rotation * Vector3.forward;

            dir.y = 0f;
            dir = dir.normalized;

            return dir;
        }

        private void _UpdateRotationFromRoom()
        {
            if (vr)
            {
                // represent anterior direction as a forward direction rotated around Y
                Quaternion anteriorRot = Quaternion.LookRotation(anterior, Vector3.up);

                // difference of Quaternion B from Quaternion A is: B * Inverse(A)
                lpRotFromRoom = anteriorRot * Quaternion.Inverse(room.rotation);
            }
            else
            {
                // for screen mode, just use the player's rotation about Y
                lpRotFromRoom = Quaternion.LookRotation(anterior, Vector3.up);
            }
        }

        // TODO this is still framerate-dependent, I think
        private void _CalibrateHeight()
        {
            float headTrackY = head.position.y;
            float currentHeight = headTrackY - room.position.y;

            // if delta from last frame is too high, reset the average
            // this accounts for avatar changes, also allows occasional resets for sanity
            float delta = Mathf.Abs(heightCalibrationContinuous - currentHeight);
            if (delta < heightChangeDeltaThreshold)
                heightCalibrationContinuous = Mathf.Lerp(heightCalibrationContinuous, currentHeight, Time.deltaTime);
            //{
            //    float weight = 0.99f;
            //    float weightPrevious = heightCalibRollingAvg * weight;
            //    float weightCurrent = currentHeight * (1f - weight);
            //    heightCalibRollingAvg = weightPrevious + weightCurrent;
            //}
            else
                heightCalibrationContinuous = currentHeight;
        }
        
        private void _MomentumAndDirection()
        {
            // check for new inflection state
            _UpdateInflection();

            // if not inflecting, accept adding momentum and direction changes
            if (Time.time > inflectionEndTime)
            {
                // apply forward bias
                //  if bias is 0, this just allows player to thumbstick accelerate from backward into the forward state smoothly
                if (moveState == MoveState.back && (momentum < forwardBias))
                {
                    moveState = MoveState.forward;
                    momentum = -momentum;
                }

                // controller input
                _InputMomentum();

                // check foot plantedness, add momentum from foot movement or inputs
                _UpdateFeetData();
                if (_GetFeetPlanted())
                    _FeetMomentum();

                // direction happens after all the momentum code, since some of it can affect move state
                _UpdateDirection();
            }

            // drag always applies
            momentum *= (1f - (drag * Time.deltaTime));
        }

        private void _UpdateInflection()
        {
            // skip if already in state
            if (Time.time < inflectionEndTime) return;

            // listen for state transition event
            if (inflectingLast)
                _OnInflectionEnd();

            // check for the beginning of an inflection
            // determine change in angular position over time since last frame (angular velocity); if threshold exceeded then begin inflection
            float deltaPhi = Quaternion.Angle(lpRotFromRoomLast, lpRotFromRoom);
            float angVel = deltaPhi / Time.deltaTime;
            if (angVel > inflectionThreshold)
            {
                // enter the inflecting state (momentum continues)
                inflectingLast = true;
                inflectionEndTime = Time.time + inflectionPeriod;
            }
        }

        private void _UpdateDirection()
        {
            if (moveState == MoveState.forward)
            {
                direction = anterior;
                return;
            }
            else if (moveState == MoveState.back)
            {
                direction = -anterior;
                return;
            }

            // right/left stopping movement requires more physics modeling calculations
            Vector3 right = new Vector3(anterior.z, 0, -anterior.x);

            // if player's blades (anterior +/-) have drifted too close to direction of momentum, run a state update
            float stopAngleHalf = stopAngleRange / 2;
            float bladeDragAngle = Vector3.Angle(right, direction);
            bool outOfStopRange = bladeDragAngle > stopAngleHalf && bladeDragAngle < (180 - stopAngleHalf);
            if (outOfStopRange)
            {
                _UpdateMoveState();
                _ApplyMoveState();

                if (moveState == MoveState.forward)
                {
                    direction = anterior;
                    return;
                }
                else if (moveState == MoveState.back)
                {
                    direction = -anterior;
                    return;
                }
            }

            // if player has slowed down to an effective stop, kick them to foward state
            if (Mathf.Abs(momentum) < stopMinMomentum)
            {
                moveState = MoveState.forward;
                _ApplyMoveState();
                direction = anterior;
                return;
            }

            // if player is really still stopping, calculate new direction
            if (moveState == MoveState.right)
            {
                direction = _StoppingDrift(right, direction);
                return;
            }
            //
            if (moveState == MoveState.left)
            {
                direction = _StoppingDrift(-right, direction);
                return;
            }

            // default to forward
            direction = anterior;
        }

        private Vector3 _StoppingDrift(Vector3 hipDir, Vector3 currentDir)
        {
            // when stopping, change in direction is toward momentum that is not perpendicular to hips
            // e.g. facing slightly backward while stopping will move you slightly backward
            Vector3 bladeDriftDir = (Vector3.Reflect(hipDir, currentDir) * -1);
            float maxDriftRate = (Mathf.PI / 2) * Time.deltaTime * surfaceHardness;
            Vector3 driftedDir = Vector3.RotateTowards(currentDir, bladeDriftDir, maxDriftRate, 0f);

            // effects
            if (fx)
                fx._PlaceStopSpray(hipDir);

            return driftedDir;
        }

        private void _UpdateFeetData()
        {
            lFootPos = lp.GetBonePosition(HumanBodyBones.LeftFoot);
            rFootPos = lp.GetBonePosition(HumanBodyBones.RightFoot);
        }

        // check that both feet are on the surface
        private bool _GetFeetPlanted()
        {
            // note: foot heights are not measured in room scale, they're measured in world scale
            //  but so is the height threshold
            float footHeightThreshold = heightCalibrationContinuous * footHeightThresholdPortion;

            float lfHeight = lFootPos.y - room.position.y;
            if (lfHeight > footHeightThreshold)
                return false;

            float rfHeight = rFootPos.y - room.position.y;
            if (rfHeight > footHeightThreshold)
                return false;

            return true;
        }

        private void _InputMomentum()
        {
            float verticalInput = input.forward;
            if ((verticalInput == 0f) || !(moveState == MoveState.forward || moveState == MoveState.back)) return;

            if (moveState == MoveState.forward)
            {
                if (verticalInput < 0)
                {
                    // if we're in a forward bias state, backward input forces you into a backward move state
                    //  this happens after application of forward bias and overrides it
                    if (momentum <= 0f)
                    {
                        moveState = MoveState.back;
                        momentum = -momentum;
                        verticalInput = -verticalInput;
                    }
                    else
                        // otherwise dampen slowing
                        verticalInput *= inputReversePortion * dragStopping;
                }
            }
            else // moveState is back
            {
                if (verticalInput > 0)
                {
                    // if player has slowed past zero, swap them to the opposite movestate
                    if (momentum <= 0f)
                    {
                        moveState = MoveState.forward;
                        momentum = -momentum;
                    }
                    else
                        // otherwise dampen slowing
                        verticalInput *= inputReversePortion * dragStopping;
                }

                // backward state, backward input should add momentum
                verticalInput = -verticalInput;
            }

            float momentAdd = verticalInput * accelScale * Time.deltaTime;
            momentum += momentAdd;

            // sfx for controller input
            if (fx && (momentAdd > 0f))
                fx._SkateSFX(momentAdd);
        }

        private void _FeetMomentum()
        {
            // skip in screen mode and for non-fbt
            if (!vr || !hips) return;

            // calculate foot displacement change and add speed if displacement is positive
            float feetDist = _GetFeetDistance();
            float feetDelta = feetDist - feetDistLast;
            if (feetDelta > 0f)
            {
                // note: should not include deltaTime
                momentum += feetDelta * accelScale;

                // sfx
                if (fx)
                    fx._SkateSFX(feetDelta);
            }

            // store for next frame
            feetDistLast = feetDist;
        }

        private float _GetFeetDistance()
        {
            // reduce to XZ plane; faster using Vector2s
            Vector2 lfPos2 = new Vector2(lFootPos.x, lFootPos.z);
            Vector2 rfPos2 = new Vector2(rFootPos.x, rFootPos.z);
            return (lfPos2 - rfPos2).magnitude;
        }

        private void _Motion()
        {
            // must use playspace teleport because this works around an issue with player teleport
            //  must lerp on remote to avoid spamming network traffic
            Vector3 displacement = direction * momentum * Time.deltaTime;
#if !UNITY_EDITOR
            Vector3 roomPosUpdate = room.position + displacement;
            lp.TeleportTo(roomPosUpdate, room.rotation, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, true);
#else
            Vector3 playerPosUpdate = Networking.LocalPlayer.GetPosition() + displacement;
            lp.TeleportTo(playerPosUpdate, lp.GetRotation(), VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint, true);
#endif
        }

        private void _CheckIKRotation()
        {
            // if immobilize was released last frame, resume it now
            //  must check regardless of FBT status; otherwise risk getting stuck in state
            if (pauseImmobile)
            {
                pauseImmobile = false;
                lp.Immobilize(true);
            }

            // no need for IK fix in FBT
            if (hips) return;
            
            // check angular and position displacements from last immobile pause
            //  if displacement is high then pause immobilize in order to update IK
            //  thanks to Superbdtingray for this workaround
            Vector3 headFwd = head.rotation * Vector3.forward;
            headFwd.y = 0f;
            float angularDisplacement = Vector3.Angle(headFwd, headRotRef);
            if (angularDisplacement > immobileTurnThreshold)
            {
                headRotRef = headFwd; // look at me. I am the captain now.
                pauseImmobile = true;
                lp.Immobilize(false);
                return;
            }

            // check the angle that the player is "leaning"
            //  this implicitly factors player scale
            Vector3 headPosRoom = head.position - room.position;
            Vector3 posture = headPosRoom - roomPosRef;
            float postureAngle = Vector3.Angle(posture, Vector3.up);
            if (postureAngle > postureAngleThreshold)
            {
                // assume room position is always in the horizontal plane with room origin
                Vector3 roomOriginXZ = new Vector3(room.position.x, 0f, room.position.z);
                Vector3 headPosXZ = new Vector3(head.position.x, 0f, head.position.z);
                roomPosRef = headPosXZ - roomOriginXZ;

                pauseImmobile = true;
                lp.Immobilize(false);
                return;
            }
        }

        private void _OnInflectionEnd()
        {
            inflectingLast = false;

            // if we're in forward bias state, resolve direction first
            if (momentum <= 0f)
            {
                direction = -direction;
                momentum = -momentum;
            }

            _UpdateMoveState();
            _ApplyMoveState();
        }

        // check instantaneous angle between momentum direction and anterior
        // choose movement state between backward, forward, left, or right
        private void _UpdateMoveState()
        {
            float stopAngleHalf = stopAngleRange / 2f;

            // backwards, defined as left or right plus the rear half of the stopping angle range
            float directionAngleFromAnterior = Vector3.Angle(anterior, direction);
            float backwardsFarThreshold = 90f + stopAngleHalf;
            if (directionAngleFromAnterior >= backwardsFarThreshold)
            {
                moveState = MoveState.back;
                return;
            }

            // construct right and check for range
            Vector3 right = new Vector3(anterior.z, 0f, -anterior.x);
            float directionAngleFromRight = Vector3.Angle(right, direction);
            if (directionAngleFromRight <= stopAngleHalf)
            {
                moveState = MoveState.right;
                return;
            }

            // check for range of left analogously as backwards
            float leftFarThreshold = 180f - stopAngleHalf;
            if (directionAngleFromRight >= leftFarThreshold)
            {
                moveState = MoveState.left;
                return;
            }

            // default to forward
            moveState = MoveState.forward;
        }

        private void _ApplyMoveState()
        {
            // update drag
            _UpdateDrag();

            // fx
            if (fx)
                fx._SetMoveFX(moveState);
        }

        private void _UpdateDrag()
        {
            if (moveState == MoveState.forward || moveState == MoveState.back)
                drag = dragMoving;
            else
                drag = dragStopping;
        }

        private void _StoreSpatialData()
        {
            //feetDistLast = feetDist;
            lpRotFromRoomLast = lpRotFromRoom;
        }

        public void _DebugAccessor(SkateDebug debug)
        {
            if (!debug) return;

            // state
            debug.inflectionEndTime = inflectionEndTime;
            debug.inflectingLast = inflectingLast;
            debug.moveState = moveState;
            debug.hips = hips;

            // motion
            debug.momentum = momentum;
            debug.direction = direction;
            debug.drag = drag;

            // spatial
            debug.groundPos = groundPos;
            debug.room = room;
            debug.anterior = anterior;
            debug.lpRotFromRoom = lpRotFromRoom;
            debug.lpRotFromRoomLast = lpRotFromRoomLast;
            debug.feetDistLast = feetDistLast;
            debug.lFootPos = lFootPos;
            debug.rFootPos = rFootPos;
            debug.heightCalibrationContinuous = heightCalibrationContinuous;
            debug.head = head;
        }
    }
}

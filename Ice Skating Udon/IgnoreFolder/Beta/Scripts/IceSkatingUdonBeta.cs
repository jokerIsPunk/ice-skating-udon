
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

namespace jokerispunk
{[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class IceSkatingUdonBeta : UdonSharpBehaviour
    {
        // player refs and data
        private VRCPlayerApi lp;
        private float heightChangeDeltaThreshold = 0.1f;
        public float footHeightThresholdProportion = 0.2f;
        private float heightCalibRollingAvg;

        // TODO replace these comments with headers
        // state logic references
        public LayerMask iceRayMask;
        [Tooltip("Collider name must contain this string to trigger ice skating code running.")]
        public string iceName = "ice";
        public float inflectionThreshold = 270f;
        public float inflectionPeriod = 0.4f;
        public float stopRangeTotal = 90f;
        public float stopMinMomentum = 0.05f;

        // state logic data
        private bool onIceLast = false;
        private bool inflecting = false;
        private byte moveState = 0;

        // motion references
        [Range(0, 3)] public float accelScale = 2f;
        [Range(0, 2)] public float iceHardness = 0.1f;
        [Range(0, 1)] public float dragMoving = 0.05f;
        [Range(0, 1)] public float dragStopping = 0.7f;

        // motion data 
        [HideInInspector] public Vector3 momentumDir;
        [HideInInspector] public float momentum;
        [HideInInspector] public float dragInstant;
        private Quaternion lpRotFromRoomLast;
        private float feetDistLast;

        // VRC playercontroller data for reverting
        [HideInInspector] public float revWalkSpd, revRunSpd, revStrafeSpd;

        // sfx & vfx
        public Transform effectsTf;
        //public Transform vfxSpray;
        public float sfxSkateThreshold = 1.2f;
        public AudioSource sfx;
        public AudioClip sfxSkate, sfxStopping;
        public ParticleSystem vfxSpray;
        public float sprayOffset = 0.8f;
        //[Tooltip("x: speed, y: emit rate")]
        //public Vector2 sprayParams = new Vector2(1f, 300f);

        // debug
        public GameObject[] debugOutputs;
        public bool debug = false;
        public Transform debugLinesTf;
        private Vector3 hapticBump = new Vector3(0.7f, 1f, 90f);
        public Transform footHeightPlane, lfBall, rfBall;
        public Text dataScreen1, dataScreen2, logMessage;
        public LineRenderer dbgLMoment, dbgLAnt, dbgLnFtDist, dbgLRoom, dbgLAngVel;
        public Transform angVelThreshLn;
        public LineRenderer dbgLStopRng1, dbgLStopRng2;
        public LineRenderer dbgLStopRef, dbgLPump, dbgLBladeDrift;
        private Vector3 dbgHipsLine;
        private Vector3 dbgLfPos3, dbgRfPos3;
        private float dbgHeightProp;
        private float dbgFeetDist;

        void Start()
        {
            lp = Networking.LocalPlayer;
            foreach (GameObject go in debugOutputs)
                go.SetActive(debug);
        }

        public void Update()
        {
            // initialize debugging
            if (debug)
            {
                dataScreen2.text = string.Empty;
                dbgLnFtDist.SetPosition(1, Vector3.zero);
                dbgLPump.SetPosition(1, Vector3.zero);
                //dbgLStopRef.SetPosition(1, Vector3.zero);
                dbgLAngVel.SetPosition(1, Vector3.zero);
                //dbgLMoment.SetPosition(1, Vector3.zero);
            }

            // common references between states
            Vector3 lpPos = lp.GetPosition();

            // what state is player in? on ice or off?
            bool onIce = _CheckForIce(lpPos);

            if (onIce && !onIceLast)
                _OnIceStart();

            if (!onIce && onIceLast)
                _OnIceEnd();

            if (onIce)
                _WhileOnIce(lpPos);

            // data for the next frame
            onIceLast = onIce;

            // debugging
            if (debug)
            {
                dataScreen2.text += $"onIce: {onIce}\n";
                _DebugOutput();
            }
        }

        private bool _CheckForIce(Vector3 lpPos)
        {
            // player pos is exactly on collider, so move up a small margin and cast downward
            Vector3 iceRayOrigin = lpPos + (Vector3.up * 0.2f);
            RaycastHit iceRayHit;

            // layermask is walkable environment generally; always expecting a hit; check to see if ground is named "ice"
            // allows natural contours without needing a mesh collider
            bool envRaycast = Physics.Raycast(iceRayOrigin, Vector3.down, out iceRayHit, 25f, iceRayMask);
            if (envRaycast)
            {
                string ground = iceRayHit.collider.name;
                ground = ground.ToLower();

                // debugging
                if (debug)
                    dataScreen2.text += $"ground: {ground}\n";

                return ground.Contains(iceName);
            }

            return false;
        }

        private void _WhileOnIce(Vector3 lpPos)
        {
            // move fx source
            effectsTf.position = lpPos;

            // common references between on-ice states
            Vector3 anterior = _GetSkeletonAnterior();
            VRCPlayerApi.TrackingData room = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
            Quaternion lpRotFromRoom = _GetRotationFromRoom(anterior, room.rotation);
            Vector3 lfPos3 = lp.GetBonePosition(HumanBodyBones.LeftFoot);
            Vector3 rfPos3 = lp.GetBonePosition(HumanBodyBones.RightFoot);
            float feetDist = _GetFeetDistance(lfPos3, rfPos3);

            // what state is player in? moving or inflecting?
            if (!inflecting)
                _WhileIceMoving(anterior, lpRotFromRoom, feetDist, lfPos3.y, rfPos3.y, room.position.y);
            // else, momentum is automatic for the inflection period!

            // apply drag for every on-ice state
            momentum = momentum * (1 - (dragInstant * Time.deltaTime));

            // finally, do lerped teleport for custom locomotion
            _IceLocomotion(room.position, room.rotation);

            // data for next frame
            feetDistLast = feetDist;
            lpRotFromRoomLast = lpRotFromRoom;
            _CalibrateHeight(room.position.y);
            
            // debugging
            if (debug)
            {
                dataScreen2.text += $"feetDist: {feetDist}\n";
                dbgLnFtDist.SetPosition(1, Vector3.forward * feetDist);
                dbgLRoom.transform.localRotation = lpRotFromRoom;
                lfBall.position = lfPos3;
                rfBall.position = rfPos3;

                // anterior line
                dbgLAnt.SetPosition(0, debugLinesTf.position);
                dbgLAnt.SetPosition(1, debugLinesTf.position + anterior);

                // stop angle ranges (relative to anterior)
                float stopRange = stopRangeTotal / 2;
                Quaternion stopRangeClose = Quaternion.Euler(0f, 90f - stopRange, 0f);
                Quaternion stopRangeFar = Quaternion.Euler(0f, 90f + stopRange, 0f);
                Vector3 stopRangeLnClose = stopRangeClose * anterior;
                Vector3 stopRangeLnFar = stopRangeFar * anterior;
                dbgLStopRng1.SetPosition(0, debugLinesTf.position - stopRangeLnClose);
                dbgLStopRng1.SetPosition(1, debugLinesTf.position + stopRangeLnClose);
                dbgLStopRng2.SetPosition(0, debugLinesTf.position - stopRangeLnFar);
                dbgLStopRng2.SetPosition(1, debugLinesTf.position + stopRangeLnFar);
            }
        }

        private void _IceLocomotion(Vector3 roomPos, Quaternion roomRot)
        {
            Vector3 displacement = momentumDir * momentum * Time.deltaTime;
            Vector3 roomPosUpdate = roomPos + displacement;
            lp.TeleportTo(roomPosUpdate, roomRot, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, true);
        }

        private void _WhileIceMoving(Vector3 anterior, Quaternion lpRotFromRoom, float feetDist, float leftFootY, float rightFootY, float roomFloor)
        {
            // check for the beginning of an inflection
            // determine change in angular position over time since last frame (angular velocity); if threshold exceeded then begin inflection
            float deltaPhi = Quaternion.Angle(lpRotFromRoomLast, lpRotFromRoom);
            float angVel = deltaPhi / Time.deltaTime;

            if (angVel < inflectionThreshold)
            {
                // alwaysupdate direction based on hip angle and forward, back, left, right
                momentumDir = _GetMomentumDir(anterior, momentumDir, moveState);

                // if moving forward or back, pumping your feet adds momentum
                if (moveState == 0 || moveState == 1)
                {
                    // check that both feet are on the ice
                    // note: foot heights are not measured in room scale, they're measured in world scale
                    //      but so is the height threshold
                    float footHeightThreshold = heightCalibRollingAvg * footHeightThresholdProportion;
                    float lfHeight = leftFootY - roomFloor;
                    float rfHeight = rightFootY - roomFloor;
                    bool feetPlanted = (lfHeight < footHeightThreshold && rfHeight < footHeightThreshold);
                    if (feetPlanted)
                        _AddMomentum(feetDist);

                    // debugging
                    if (debug)
                    {
                        dataScreen2.text += $"footThresh: {footHeightThreshold}\nfeetPlanted: {feetPlanted}\n";
                        footHeightPlane.localPosition = footHeightThreshold * Vector3.up;
                    }
                }
            }
            else
            {
                // enter the inflecting state (momentum continues) and invoke the end of the state after delay
                inflecting = true;
                SendCustomEventDelayedSeconds(nameof(_OnInflectionEnd), inflectionPeriod);

                if (debug)
                    lp.PlayHapticEventInHand(VRC_Pickup.PickupHand.Right, hapticBump.x, hapticBump.y, hapticBump.z);
            }

            // debugging
            if (debug)
            {
                float dbgAngVel = angVel / 1000;
                float dbgAVThresh = inflectionThreshold / 1000;
                dbgLAngVel.SetPosition(1, (dbgAngVel * Vector3.forward));
                angVelThreshLn.localPosition = dbgAVThresh * Vector3.forward;
            }
        }

        private void _CalibrateHeight(float roomFloor)
        {
            float headTrackY = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position.y;
            float currentHeight = headTrackY - roomFloor;

            // if delta from last frame is too high, reset the average
            // this accounts for avatar changes, also allows occasional resets for sanity
            float delta = Mathf.Abs(heightCalibRollingAvg - currentHeight);
            if (delta < heightChangeDeltaThreshold)
            {
                float weight = 0.99f;
                float weightPrevious = heightCalibRollingAvg * weight;
                float weightCurrent = currentHeight * (1f - weight);
                heightCalibRollingAvg = weightPrevious + weightCurrent;
            }
            else
                heightCalibRollingAvg = currentHeight;
        }

        public void _OnInflectionEnd()
        {
            inflecting = false;

            // need to update this value independently because it's called via delayed invoke
            Vector3 anterior = _GetSkeletonAnterior();
            _UpdateMoveState(anterior);
            if (debug) _Log($"[IceSkating] Move state: {moveState}");
        }

        private byte _GetMoveState(Vector3 anterior)
        {
            // check instantaneous angle between momentum direction and anterior
            // choose movement state between backward, forward, left, or right
            // this essentially is programming to "snap" to one of those directions based on what's closest after inflection
            // the big brain way to do this is probably a matrix operation or smth, but I'm just gonna use a lookup table
            float stopRange = stopRangeTotal / 2;
            float inflectedForward = Vector3.Angle(anterior, momentumDir);

            // backwards defined as left or right plus the rear half of the stopping angle range
            float backwardsFarThreshold = 90 + stopRange;
            if (inflectedForward >= backwardsFarThreshold)
                return 1;
            else
            {
                // construct left and check for range
                Vector3 left = new Vector3(-anterior.z, 0, anterior.x);
                float inflectedLeft = Vector3.Angle(left, momentumDir);
                if (inflectedLeft <= stopRange)
                    return 2;

                // check for range of right analogously as backwards
                float rightFarThreshold = 180 - stopRange;
                if (inflectedLeft >= rightFarThreshold)
                    return 3;
            }

            return 0;
        }

        private float _GetDrag(byte state)
        {
            if (state == 0 || state == 1) // fore or back
                return dragMoving;

            if (state == 2 || state == 3) // left or right
                return dragStopping;

            return dragMoving;
        }

        // TODO does this work well?
        private Vector3 _GetMomentumDir(Vector3 anterior, Vector3 currentDir, byte state)
        {
            if (state == 1) // back
                return -anterior;
            if (state == 0) // forward
                return anterior;

            // right/left stopping movement requires more physics modeling calculations
            Vector3 left = new Vector3(-anterior.z, 0, anterior.x);

            // but first, check if stop state is still appropriate
            // if player's blades (anterior +/-) have drifted too close to momentum, run a state update
            float stopRange = stopRangeTotal / 2;
            float bladeDragAngle = Vector3.Angle(left, currentDir);
            bool outOfStopRange = bladeDragAngle > stopRange && bladeDragAngle < (180 - stopRange);
            if (outOfStopRange)
            {
                _UpdateMoveState(anterior);
                state = moveState;
                if (state == 1)
                    return -anterior;
                if (state == 0)
                    return anterior;
            }

            // but first, check if player has slowed down to an effective stop and, if so, kick them to foward state
            // I expect players to naturally drift blade-to-momentum, or actively inflect, before this point
            if (momentum < stopMinMomentum)
            {
                moveState = 0;
                dragInstant = dragMoving;
                return anterior;
            }

            // if player is really still stopping, calculate new momentum direction
            if (state == 2) // left
                return _StoppingDrift(left, currentDir);

            if (state == 3) // right
                return _StoppingDrift((left * -1), currentDir);

            // default to forward
            return anterior;
        }

        private Vector3 _StoppingDrift(Vector3 hipDir, Vector3 currentDir)
        {
            // when stopping, change in direction is toward momentum that is not perpendicular to hips
            // e.g. facing slightly backward while stopping will move you slightly backward
            Vector3 bladeDriftDir = (Vector3.Reflect(hipDir, currentDir) * -1);
            float maxDriftRate = (Mathf.PI / 2) * Time.deltaTime * iceHardness;
            Vector3 driftedDir = Vector3.RotateTowards(currentDir, bladeDriftDir, maxDriftRate, 0f);
            
            // orient spray effect
            if (vfxSpray.transform != null)
            {
                // TODO replace with parent pivot
                Vector3 lpPos = lp.GetPosition();
                vfxSpray.transform.rotation = Quaternion.LookRotation(hipDir, Vector3.up);
                vfxSpray.transform.position = lpPos + (hipDir * sprayOffset);

                //ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
                //emitParams.velocity = momentumDir * (momentum + sprayParams.x);
                //int count = Mathf.RoundToInt(sprayParams.y / Time.deltaTime);
                //vfxSpray.Emit(emitParams, count);
            }

            // debugging
            if (debug)
            {
                dbgLStopRef.SetPosition(0, debugLinesTf.position);
                dbgLStopRef.SetPosition(1, debugLinesTf.position + hipDir);
                dbgLBladeDrift.SetPosition(0, debugLinesTf.position);
                dbgLBladeDrift.SetPosition(1, debugLinesTf.position + bladeDriftDir);
            }

            return driftedDir;
        }

        private void _AddMomentum(float feetDist)
        {
            // calculate foot displacement change and add speed if positive
            float feetDelta = feetDist - feetDistLast;
            if (feetDelta > 0f)
            {
                momentum += (feetDelta * accelScale);

                // check for sfx
                if (sfx != null && !sfx.isPlaying)
                {
                    float pumpSpeed = feetDelta / Time.deltaTime;
                    if (pumpSpeed > sfxSkateThreshold)
                    {
                        sfx.clip = sfxSkate;
                        if (sfx.clip != null) sfx.Play();
                    }

                    // debugging
                    if (debug)
                        dbgLPump.SetPosition(1, pumpSpeed * Vector3.forward);
                }
            }
        }

        private float _GetFeetDistance(Vector3 lfPos3, Vector3 rfPos3)
        {
            // check that bones exist
            if (lfPos3 == Vector3.zero || rfPos3 == Vector3.zero)
                return 0f;

            // reduce to XZ plane; faster using Vector2s
            Vector2 lfPos2 = new Vector2(lfPos3.x, lfPos3.z);
            Vector2 rfPos2 = new Vector2(rfPos3.x, rfPos3.z);

            // use difference and magnitude to find distance
            float feetDist = (lfPos2 - rfPos2).magnitude;

            return feetDist;
        }

        public Vector3 _GetSkeletonAnterior()
        {
            // the line between hips, from left to right
            Vector3 lHipPos = lp.GetBonePosition(HumanBodyBones.LeftUpperLeg);
            Vector3 rHipPos = lp.GetBonePosition(HumanBodyBones.RightUpperLeg);
            Vector3 hipsLineRight = rHipPos - lHipPos;

            // the line anticlockwise perpendicular from hips line, on the XZ plane, length 1
            Vector3 anterior = new Vector3(-hipsLineRight.z, 0, hipsLineRight.x);
            anterior = anterior.normalized;

            return anterior;
        }

        private Quaternion _GetRotationFromRoom(Vector3 anterior, Quaternion roomRot)
        {
            // represent anterior direction as a forward direction rotated around Y
            Quaternion anteriorRot = Quaternion.LookRotation(anterior, Vector3.up);

            // difference of Quaternion B from Quaternion A is: B * Inverse(A)
            Quaternion lpRotFromRoom = anteriorRot * Quaternion.Inverse(roomRot);

            return lpRotFromRoom;
        }

        // calls getters redundantly because it doesn't happen every frame
        // and I don't want the spaghetti of passing the values down yet another layer of function
        private void _OnIceStart()
        {
            // logging
            if (debug) _Log("[IceSkating] Called _OnIceStart...");

            // initialize foot distance
            Vector3 lfPos3 = lp.GetBonePosition(HumanBodyBones.LeftFoot);
            Vector3 rfPos3 = lp.GetBonePosition(HumanBodyBones.RightFoot);
            feetDistLast = _GetFeetDistance(lfPos3, lfPos3);

            // initialize player rotation from room
            Vector3 anterior = _GetSkeletonAnterior();
            Quaternion roomRot = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin).rotation;
            lpRotFromRoomLast = _GetRotationFromRoom(anterior, roomRot);

            // initialize momentum
            Vector3 lpVel = lp.GetVelocity();
            lpVel.y = 0f;
            momentum = lpVel.magnitude;
            momentumDir = lpVel.normalized;

            // initialize movement state and drag (can go straight to stopping!)
            _UpdateMoveState(anterior);
            inflecting = false;

            // TODO decide what to do with jump, if anything
            // save all VRC playercontroller data and set to zero
            revWalkSpd = lp.GetWalkSpeed();
            lp.SetWalkSpeed(0f);
            revRunSpd = lp.GetRunSpeed();
            lp.SetRunSpeed(0f);
            revStrafeSpd = lp.GetStrafeSpeed();
            lp.SetStrafeSpeed(0f);
            lp.SetVelocity(Vector3.zero);
        }

        private void _OnIceEnd()
        {
            // revert to VRC playercontroller
            lp.SetWalkSpeed(revWalkSpd);
            lp.SetRunSpeed(revRunSpd);
            lp.SetStrafeSpeed(revStrafeSpd);

            // stop effects
            _HandleMoveState(0);
            //if (vfxSpray != null) vfxSpray.Stop();
        }

        private void _Log(string message)
        {
            if (logMessage != null)
                logMessage.text = message;
            Debug.Log(message, gameObject);
        }

        private void _UpdateMoveState(Vector3 anterior)
        {
            moveState = _GetMoveState(anterior);
            _HandleMoveState(moveState);
        }

        private void _HandleMoveState(byte state)
        {
            // update drag
            dragInstant = _GetDrag(state);

            // update FX
            if ((state == 2 || state == 3))
            {
                // sfx
                if (sfx != null)
                {
                    sfx.loop = true;
                    if (sfxStopping != null)
                    {
                        sfx.clip = sfxStopping;
                        sfx.Play();
                    }
                }

                // vfx
                if (vfxSpray != null)
                    vfxSpray.gameObject.SetActive(true);
            }
            else
            {
                // sfx
                if (sfx != null)
                {
                    sfx.loop = false;
                    sfx.Stop();
                    if (sfxSkate != null)
                        sfx.clip = sfxSkate;
                }

                // vfx
                if (vfxSpray != null)
                    vfxSpray.gameObject.SetActive(false);
            }
        }

        public void _DebugOutput()
        {
            Vector3 lpPos = lp.GetPosition();
            lpPos.y += 0.05f;

            dataScreen1.text =
                $"onIceLast: {onIceLast}\ninflecting: {inflecting}\nmovestate: {moveState}\ndrag: {dragInstant}\nheight: {heightCalibRollingAvg}\n";

            // stop angle ranges
            //float stopRange = stopRangeTotal / 2;
            //float stopRangeSin = Mathf.Sin(stopRange);
            //float stopRangeCos = Mathf.Cos(stopRange);
            //Vector3 stopRangeACW = new Vector3(stopRangeCos, 0f, stopRangeSin);
            //Vector3 stopRangeCW = new Vector3(stopRangeCos, 0f, -stopRangeSin);
            //dbgLStopRng1.SetPosition(0, debugLinesTf.position - stopRangeACW);
            //dbgLStopRng1.SetPosition(1, debugLinesTf.position + stopRangeACW);
            //dbgLStopRng2.SetPosition(0, debugLinesTf.position - stopRangeCW);
            //dbgLStopRng2.SetPosition(1, debugLinesTf.position + stopRangeCW);

            // momentum and momentum direction
            dbgLMoment.SetPosition(0, debugLinesTf.position);
            dbgLMoment.SetPosition(1, debugLinesTf.position + (momentumDir * momentum));
        }
    }
}

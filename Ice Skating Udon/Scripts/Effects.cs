
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace jokerispunk.IceSkatingUdon
{
    // this class manages sound and visual effects
    //  including their execution and their properties from frame to frame
    // written by github.com/jokerispunk
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Effects : BaseIceSkatingUdon
    {
        [SerializeField] float sfxSkateThreshold = 1.2f;
        [SerializeField] AudioSource pump, loopA, loopB;
        [SerializeField] AudioClip sfxStoppingA, sfxStoppingB;
        [SerializeField] AudioClip sfxGlidingA, sfxGlidingB;
        [SerializeField] AnimationCurve stopVol, glideVol;
        [SerializeField] AnimationCurve pumpVol;
        [SerializeField] AnimationCurve stopPitch, glidePitch;
        [SerializeField] float pumpPitchRangeHalf = 0.1f;
        [SerializeField] float cooldownVR = 0.5f, cooldownScreen = 1f;
        private float cooldown;

        [Space]
        [SerializeField] ParticleSystem vfxSpray;
        //[SerializeField] float sprayOffset = 0.8f;

        private float cooldownEnd;

        private void Start()
        {
            bool vr = Networking.LocalPlayer.IsUserInVR();
            cooldown = (vr ? cooldownVR : cooldownScreen);
        }

        public void _SkateSFX(float feetDelta)
        {
            if (!pump || (Time.time < cooldownEnd)) return;

            float pumpSpeed = feetDelta / Time.deltaTime;
            if (pumpSpeed > sfxSkateThreshold)
            {
                cooldownEnd = Time.time + cooldown;

                pump.pitch = 1f + Random.Range(-pumpPitchRangeHalf, pumpPitchRangeHalf);
                pump.volume = pumpVol.Evaluate(pumpSpeed);
                pump.Play();
            }
        }

        public void _PlaceStopSpray(Vector3 hipDir)
        {
            // don't need to move the transform since that's inherited from parent
            transform.rotation = Quaternion.LookRotation(hipDir);
        }

        public void _PitchAndVolume(float momentum, MoveState moveState)
        {
            // determine curves based on state
            bool stopping = (moveState == MoveState.left) || (moveState == MoveState.right);
            AnimationCurve volCurve = stopping ? stopVol : glideVol;
            AnimationCurve pitchCurve = stopping ? stopPitch : glidePitch;
            
            // evaluate curves
            float volume = volCurve.Evaluate(momentum);
            float pitch = pitchCurve.Evaluate(momentum);

            // apply values
            loopA.volume = volume;
            loopB.volume = volume;
            loopA.pitch = pitch;
            loopB.pitch = pitch;
        }

        public void _SetMoveFX(MoveState moveState)
        {
            // play stopping effects if stopping, else stop effects
            if (moveState == MoveState.left || moveState == MoveState.right)
            {
                // looping sfx
                _SwapClipLooping(loopA, sfxStoppingA);
                _SwapClipLooping(loopB, sfxStoppingB);

                // vfx
                vfxSpray.gameObject.SetActive(true);
            }
            else
            {
                // looping sfx
                _SwapClipLooping(loopA, sfxGlidingA);
                _SwapClipLooping(loopB, sfxGlidingB);

                // vfx
                vfxSpray.gameObject.SetActive(false);
            }
        }

        private void _SwapClipLooping(AudioSource src, AudioClip clip)
        {
            if (!src) return;

            if (!clip)
            {
                src.Stop();
                return;
            }

            src.clip = clip;

            if (src.enabled && src.gameObject.activeInHierarchy)
                src.Play();
        }
    }
}

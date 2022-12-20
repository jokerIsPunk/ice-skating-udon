
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

namespace jokerispunk.IceSkatingUdon
{
    // this class enables manipulation of the skate program's references and data
    //  it stores initial values to allow the player to reset changes
    //  it also automatically applies platform-specific values to the skate program, like disabling FBT mode for desktop and android
    // written by github.com/jokerispunk
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ControlUI : BaseIceSkatingUdon
    {
        [SerializeField] Slider accelSlider, stopSlider, inflSlider, dragSlider, biasSlider, footHtSlider, hardSlider;
        [SerializeField] Toggle hipsToggle;
        [SerializeField] Text accelTxt, stopTxt, inflTxt, dragTxt, biasTxt, footHtTxt, hardTxt;
        [SerializeField] Skating skateProgram;
        [SerializeField] Image mouseInfo, controllersInfo;

        private float accelReset, stopReset, inflReset, dragReset, biasReset, footHtReset, hardReset;

        public bool flagDebug = false;

        void Start()
        {
            if (flagDebug)
                _LogMsg($"hips: {skateProgram.hips}");

            // scope cache to avoid redundant Udon GetProgVar calls
            float accelScale = skateProgram.accelScale;
            float surfaceHardness = skateProgram.surfaceHardness;
            float dragMoving = skateProgram.dragMoving;
            float dragStopping = skateProgram.dragStopping;
            float footHeightThresholdPortion = skateProgram.footHeightThresholdPortion;
            float inflectionThreshold = skateProgram.inflectionThreshold;
            float forwardBias = skateProgram.forwardBias;

            // class cache starting values for reset
            accelReset = accelScale;
            hardReset = surfaceHardness;
            dragReset = dragMoving;
            stopReset = dragStopping;
            footHtReset = footHeightThresholdPortion;
            inflReset = inflectionThreshold;
            biasReset = forwardBias;

            // move the controls to start values without triggering value change vents
            accelSlider.SetValueWithoutNotify(accelScale);
            hardSlider.SetValueWithoutNotify(surfaceHardness);
            dragSlider.SetValueWithoutNotify(dragMoving);
            stopSlider.SetValueWithoutNotify(dragStopping);
            footHtSlider.SetValueWithoutNotify(footHeightThresholdPortion);
            inflSlider.SetValueWithoutNotify(inflectionThreshold);
            hipsToggle.SetIsOnWithoutNotify(skateProgram.hips);
            biasSlider.SetValueWithoutNotify(forwardBias);

            // set text values to start values
            accelTxt.text = accelScale.ToString("N1");
            hardTxt.text = surfaceHardness.ToString("N1");
            dragTxt.text = dragMoving.ToString("N2");
            stopTxt.text = dragStopping.ToString("N2");
            footHtTxt.text = footHeightThresholdPortion.ToString("N1");
            inflTxt.text = inflectionThreshold.ToString("N0");
            biasTxt.text = forwardBias.ToString("N2");

            // check for screen mode and android (setting the toggle field automatically updates the program value)
            bool vr = Networking.LocalPlayer.IsUserInVR();
#if !UNITY_ANDROID
            hipsToggle.isOn = vr;
#else
            hipsToggle.isOn = false;
#endif
            hipsToggle.gameObject.SetActive(vr);
            if (controllersInfo) controllersInfo.enabled = vr;
            if (mouseInfo) mouseInfo.enabled = !vr;
        }

        public void _Accel()
        {
            float value = accelSlider.value;

            skateProgram.accelScale = value;
            accelTxt.text = value.ToString("N1");
        }

        public void _Hardness()
        {
            float value = hardSlider.value;

            skateProgram.surfaceHardness = value;
            hardTxt.text = value.ToString("N1");
        }

        public void _Drag()
        {
            float value = dragSlider.value;

            skateProgram.dragMoving = value;
            dragTxt.text = value.ToString("N2");
        }

        public void _Stopping()
        {
            float value = stopSlider.value;

            skateProgram.dragStopping = value;
            stopTxt.text = value.ToString("N2");
        }

        public void _FootHeight()
        {
            float value = footHtSlider.value;

            skateProgram.footHeightThresholdPortion = value;
            footHtTxt.text = value.ToString("N1");
        }

        public void _Pivot()
        {
            float value = inflSlider.value;

            skateProgram.inflectionThreshold = value;
            inflTxt.text = value.ToString("N0");
        }

        public void _Hips()
        {
            skateProgram.hips = hipsToggle.isOn;
        }

        public void _Bias()
        {
            float value = biasSlider.value;

            skateProgram.forwardBias = value;
            biasTxt.text = value.ToString("N2");
        }

        public void _Reset()
        {
            // setting the values triggers the above method calls which takes care of all the updates
            accelSlider.value = accelReset;
            hardSlider.value = hardReset;
            dragSlider.value = dragReset;
            stopSlider.value = stopReset;
            footHtSlider.value = footHtReset;
            inflSlider.value = inflReset;
        }
    }
}

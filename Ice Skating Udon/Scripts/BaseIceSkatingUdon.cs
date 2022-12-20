
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace jokerispunk.IceSkatingUdon
{
    // base class, for inheriting debug and error stuff
    //  also declaring the enum
    // written by github.com/jokerispunk
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class BaseIceSkatingUdon : UdonSharpBehaviour
    {
        public void _LogMsg(string logString)
        {
            Debug.Log($"[IceSkatingUdon][{gameObject.name}] {logString}");
        }

        public void _LogWarn(string logString)
        {
            Debug.LogWarning($"[IceSkatingUdon][{gameObject.name}] {logString}");
        }

        public void _LogErr(string logString)
        {
            Debug.LogError($"[IceSkatingUdon][{gameObject.name}] {logString}");
        }
    }

    // special enums
    public enum MoveState : byte { forward, back, left, right };
}

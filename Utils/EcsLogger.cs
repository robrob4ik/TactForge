namespace OneBitRob.Constants
{
#if UNITY_EDITOR // keep logs out of builds
    using UnityEngine;
#endif

    static class EcsLogger
    {
#if UNITY_EDITOR
        public static void Info(object who, string msg) => Debug.Log($"<color=#84CEEB>[{who}]</color> {msg}");
#endif
    }
}
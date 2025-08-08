using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.Profiling;
#endif

public class ProfilerSetup : MonoBehaviour
{
    void Awake()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Profiler.maxUsedMemory = 1073741824; // 1 GB
#endif
    }
}
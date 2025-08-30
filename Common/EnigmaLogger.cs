using UnityEngine;

public static class EnigmaLogger
{
    public const bool DebugLogs = false;
    
    public const string DEBUG_LEVEL = "DEBUG";
    public const string INFO_LEVEL = "INFO";
    public const string WARN_LEVEL = "WARN";

    public static void Log(string msg, string level = DEBUG_LEVEL)
    {
        if (level == DEBUG_LEVEL && !DebugLogs) return;
        Debug.Log(msg);
    }
}
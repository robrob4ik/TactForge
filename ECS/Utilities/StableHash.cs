// Runtime/ECS/Utilities/StableHash.cs
using System;
using System.Text;

namespace OneBitRob.ECS
{
    /// <summary>
    /// Stable, deterministic 32-bit FNV-1a string hash (ASCII/UTF8 safe).
    /// Use for mapping authoring IDs to ECS ints.
    /// </summary>
    public static class StableHash
    {
        // FNV-1a 32-bit constants
        private const uint OffsetBasis = 2166136261u;
        private const uint Prime = 16777619u;

        public static int String32(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            uint hash = OffsetBasis;
            // UTF-8 guarantees consistency across platforms
            var bytes = Encoding.UTF8.GetBytes(s);
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= Prime;
            }
            return unchecked((int)hash);
        }
    }
}
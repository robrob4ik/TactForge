using UnityEngine;

namespace OneBitRob.Core
{
    public static class DebugPalette
    {
        // General tones
        public static readonly Color Info        = new Color(0.80f, 0.90f, 1.00f, 0.95f);
        public static readonly Color Warning     = new Color(1.00f, 0.60f, 0.10f, 0.90f);
        public static readonly Color Danger      = new Color(1.00f, 0.25f, 0.25f, 0.95f);
        public static readonly Color Success     = new Color(0.20f, 1.00f, 0.35f, 0.95f);

        // Gameplay‑centric
        public static readonly Color AttackRange          = new Color(1.00f, 0.25f, 0.25f, 0.95f);
        public static readonly Color AutoTargetSwitch     = new Color(0.25f, 0.55f, 1.00f, 0.95f);
        public static readonly Color MoveIntent           = Color.cyan;
        public static readonly Color TargetLine           = new Color(1.00f, 0.50f, 0.10f, 0.95f);
        public static readonly Color Facing               = Color.yellow;
        public static readonly Color ProjectilePath       = new Color(1.00f, 0.95f, 0.20f, 0.95f);
        public static readonly Color MeleeArc             = new Color(0.20f, 1.00f, 0.20f, 0.95f);
        public static readonly Color NavLocked            = new Color(1.00f, 0.60f, 0.10f, 0.90f);
        public static readonly Color DotAreaPositive      = new Color(0.20f, 1.00f, 0.35f, 0.90f);
        public static readonly Color DotAreaNegative      = new Color(1.00f, 0.25f, 0.25f, 0.95f);
        public static readonly Color TransformSync        = new Color(1.00f, 0.00f, 1.00f, 0.85f);
        public static readonly Color SpellRange           = new Color(0.85f, 0.20f, 0.20f, 0.95f);

        // Keep existing names for compatibility
        public static readonly Color SpellPrepare = Info;
        public static readonly Color SpellFire    = new Color(1.00f, 0.45f, 0.20f, 0.95f);
        public static readonly Color Banner       = MeleeArc;

        // Alias to ease migration
        public static readonly Color MoveTo = MoveIntent;
    }
}

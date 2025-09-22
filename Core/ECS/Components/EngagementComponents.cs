using Unity.Entities;

namespace OneBitRob.ECS
{
    /// <summary>
    /// Tactical engagement preferences (NOT the navmesh agent stopping distance).
    /// Controls how far from the target a unit prefers to hold.
    /// </summary>
    public struct EngagementPreferences : IComponentData
    {
        /// <summary>Preferred distance from target to stop (units).</summary>
        public float EngageDistance;

        /// <summary>Optional extra backward bias for kiting (0 = off).</summary>
        public float KiteBuffer;
    }

    /// <summary>
    /// Per-unit leash derived from the assigned banner strategy.
    /// Keeps units from over-chasing and provides a clear "return-to-home".
    /// </summary>
    public struct BannerLeash : IComponentData
    {
        /// <summary>Max distance from the assigned banner center.</summary>
        public float Radius;

        /// <summary>Additional grace distance before snapping back.</summary>
        public float Slack;
    }
}
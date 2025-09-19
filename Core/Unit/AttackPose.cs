using Unity.Mathematics;
using Unity.Transforms;

namespace PROJECT.Scripts.AI.Brain
{
   
    namespace OneBitRob.Core
    {
        /// <summary>Immutable snapshot of an entity's pose with cached basis vectors.</summary>
        public readonly struct AttackPose
        {
            public readonly float3 Position;
            public readonly quaternion Rotation;
            public readonly float3 Forward;
            public readonly float3 Right;
            public readonly float3 Up;

            public AttackPose(float3 position, quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
                Forward  = math.normalizesafe(math.mul(rotation, new float3(0, 0, 1)));
                Up       = math.normalizesafe(math.mul(rotation, new float3(0, 1, 0)));
                Right    = math.normalizesafe(math.mul(rotation, new float3(1, 0, 0)));
            }

            public static AttackPose FromLocalTransform(LocalTransform lt)
                => new AttackPose(lt.Position, lt.Rotation);
        }
    }
}
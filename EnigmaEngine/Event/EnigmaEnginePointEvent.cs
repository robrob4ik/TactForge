using MoreMountains.Tools;

namespace OneBitRob.EnigmaEngine
{
    public enum PointsMethods
    {
        Add,
        Set
    }
    
    public struct EnigmaEnginePointEvent
    {
        public PointsMethods PointsMethod;
        public int Points;
        
        public EnigmaEnginePointEvent(PointsMethods pointsMethod, int points)
        {
            PointsMethod = pointsMethod;
            Points = points;
        }

        static EnigmaEnginePointEvent e;

        public static void Trigger(PointsMethods pointsMethod, int points)
        {
            e.PointsMethod = pointsMethod;
            e.Points = points;
            MMEventManager.TriggerEvent(e);
        }
    }
}
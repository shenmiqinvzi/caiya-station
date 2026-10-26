using System;

namespace CaiYaStation.Domain
{
    /// <summary>
    /// Placement arithmetic shared by the Unity layer and the tests. Every number that decides
    /// "can a kiosk stand here and can a player walk to it" lives here so it can be exercised
    /// without a running game.
    ///
    /// Why this exists: the first version accepted any collider under a 2.5m navmesh snap, so
    /// the water plane, railing tops and bridge edges all counted as ground. A kiosk could then
    /// land in a river, two metres below the road, and never be reachable.
    /// </summary>
    public static class PlacementPolicy
    {
        /// <summary>Metres the navmesh may sit away from the solid ground under a candidate.</summary>
        public const double MaximumNavMeshGap = 0.5;

        /// <summary>Metres the four base corners of a kiosk may differ in height.</summary>
        public const double MaximumFootprintStep = 0.35;

        /// <summary>How much longer than the straight line a walk to a kiosk may be.</summary>
        public const double MaximumPathDetour = 1.6;

        /// <summary>Metres of slack allowed on top of <see cref="MaximumPathDetour"/>.</summary>
        public const double PathDetourSlack = 6.0;

        /// <summary>
        /// Ground and navmesh have to agree; otherwise the "solid" surface is a decoration or a
        /// water plane rather than the floor the pathfinder walks on.
        /// </summary>
        public static bool NavMeshAligned(double groundY, double navMeshY, double tolerance = MaximumNavMeshGap)
            => Math.Abs(groundY - navMeshY) <= tolerance;

        /// <summary>
        /// All four base corners of the kiosk have to stand at the same height, which is what
        /// stops the booth from hanging over a railing, a bank or the water.
        /// </summary>
        public static bool FootprintFlat(double lowestY, double highestY, double tolerance = MaximumFootprintStep)
            => highestY - lowestY <= tolerance;

        /// <summary>
        /// Height a corner reaches on the plane that runs through the centre with the ground's
        /// own normal. Comparing corners against this plane instead of against each other lets a
        /// kiosk sit on a gentle slope (which is walkable) while still catching a step, a kerb or
        /// a bridge edge, where one corner leaves the plane the ground was following.
        /// </summary>
        public static double PredictedCornerY(double centerY, double normalX, double normalY, double normalZ,
            double dx, double dz)
        {
            if (Math.Abs(normalY) < 1e-4) return centerY;
            return centerY - (normalX * dx + normalZ * dz) / normalY;
        }

        /// <summary>Metres a base corner may deviate from the plane predicted by the centre normal.</summary>
        public static bool CornerOnPlane(double actualY, double predictedY,
            double tolerance = MaximumFootprintStep)
            => Math.Abs(actualY - predictedY) <= tolerance;

        /// <summary>
        /// A point can be eight metres away and still be a two hundred metre walk around a
        /// river. Reject detours the player would never make.
        /// </summary>
        public static bool PathIsReasonable(double pathLength, double straightDistance,
            double maximumDetour = MaximumPathDetour, double slack = PathDetourSlack)
        {
            if (pathLength <= 0.0) return false;
            if (straightDistance < 0.0) return false;
            return pathLength <= straightDistance * maximumDetour + slack;
        }

        /// <summary>
        /// Whether an extraction point this far away is acceptable for the given relaxation
        /// pass. Pass 0 keeps the designed band, pass 1 only keeps "not on top of an
        /// extraction", and pass 2 drops the band while still refusing to sit on one. The old
        /// pass 2 accepted anything, which is how a kiosk ended up on the river bank.
        /// </summary>
        public static bool ExtractionAllows(double nearest, int pass, double minimum, double maximum)
        {
            if (nearest < minimum) return false;
            if (pass <= 0) return nearest <= maximum;
            return true;
        }

        /// <summary>
        /// Lower is better. A closer search ring wins, and between equal rings the shorter walk
        /// wins; the first-hit-wins loop used to drop the kiosk on the very first 8m candidate
        /// even when a better point existed at 12m.
        /// </summary>
        public static double Score(double pathLength, double ring, double ringWeight = 0.5)
            => pathLength + ring * ringWeight;
    }
}

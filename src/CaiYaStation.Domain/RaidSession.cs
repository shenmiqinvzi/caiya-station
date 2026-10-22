using System;
using System.Collections.Generic;
using System.Linq;

namespace CaiYaStation.Domain
{
    public sealed class StationRecord
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "菜鸭驿站";
        public string MapId { get; set; } = "";
        public string SceneName { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Yaw { get; set; }
        public bool Used { get; set; }

        public double DistanceTo(StationRecord other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            var dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    /// <summary>One map's saved station layout. A raid may cover several maps; usage never rerolls inside a session.</summary>
    public sealed class RaidSession
    {
        public string Token { get; set; } = "";
        public bool Active { get; set; } = true;
        public string StartedUtc { get; set; } = "";
        public string EndedUtc { get; set; } = "";
        public List<StationRecord> Stations { get; set; } = new List<StationRecord>();

        public IEnumerable<StationRecord> ForMap(string mapId) =>
            Stations.Where(station => string.Equals(station.MapId, mapId, StringComparison.Ordinal));

        public StationRecord? Find(string stationId) =>
            Stations.FirstOrDefault(station => string.Equals(station.Id, stationId, StringComparison.Ordinal));

        public bool MarkUsed(string stationId)
        {
            var station = Find(stationId);
            if (station == null || station.Used) return false;
            station.Used = true;
            return true;
        }

        public void AddStations(IEnumerable<StationRecord> stations)
        {
            foreach (var station in stations)
            {
                if (Find(station.Id) == null) Stations.Add(station);
            }
        }

        public int UsedCount(string mapId) => ForMap(mapId).Count(station => station.Used);
        public int AvailableCount(string mapId) => ForMap(mapId).Count(station => !station.Used);
    }

    public sealed class StationCandidate
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "菜鸭驿站";
        public string MapId { get; set; } = "";
        public string SceneName { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Yaw { get; set; }

        public double DistanceTo(StationCandidate other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            var dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    public static class StationPlanning
    {
        /// <summary>
        /// Picks this raid's stations from the configured anchor pool. Positions are later snapped to safe
        /// ground by the Unity layer; this method only decides identity and rough separation.
        /// </summary>
        public static List<StationRecord> Choose(
            IReadOnlyList<StationCandidate> candidates,
            int count,
            double minimumSeparation,
            Random random)
        {
            if (count <= 0) return new List<StationRecord>();
            var pool = candidates.ToList();
            Shuffle(pool, random);
            var chosen = new List<StationCandidate>();
            foreach (var candidate in pool)
            {
                if (chosen.Count >= count) break;
                if (chosen.Any(other => other.DistanceTo(candidate) < minimumSeparation)) continue;
                if (chosen.Any(other => string.Equals(other.Id, candidate.Id, StringComparison.Ordinal))) continue;
                chosen.Add(candidate);
            }
            // If the pool is tight, relax separation rather than spawning fewer stations than configured.
            foreach (var candidate in pool)
            {
                if (chosen.Count >= count) break;
                if (chosen.Any(other => string.Equals(other.Id, candidate.Id, StringComparison.Ordinal))) continue;
                chosen.Add(candidate);
            }
            return chosen.Select(candidate => new StationRecord
            {
                Id = candidate.Id,
                Name = candidate.Name,
                MapId = candidate.MapId,
                SceneName = candidate.SceneName,
                X = candidate.X,
                Y = candidate.Y,
                Z = candidate.Z,
                Yaw = candidate.Yaw,
                Used = false
            }).ToList();
        }

        private static void Shuffle<T>(IList<T> list, Random random)
        {
            for (var index = list.Count - 1; index > 0; index--)
            {
                var swap = random.Next(index + 1);
                (list[index], list[swap]) = (list[swap], list[index]);
            }
        }
    }
}

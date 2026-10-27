using System;
using Pathfinding;
using Pathfinding.Pooling;
using UnityEngine;

namespace CaiYaStation
{
    /// <summary>
    /// Reachability proof against the pathfinder this game actually ships.
    ///
    /// Why this exists: Escape from Duckov uses the A* Pathfinding Project, not Unity's NavMesh.
    /// The previous version asked <c>NavMesh.SamplePosition</c> for every candidate, which fails
    /// everywhere in this title, so the placement loop rejected 96 of 96 points and no kiosk
    /// spawned at all. Every "can the player stand here / walk here" question now goes through
    /// the same graph the enemies use.
    /// </summary>
    internal static class PathProbe
    {
        /// <summary>
        /// Metres a candidate may sit away from the walkable node it snaps to.
        ///
        /// Deliberately generous: a recast node is the centroid of a whole triangle, and outdoor
        /// triangles are large, so an honest road point can sit ten metres from the node it
        /// belongs to. A tight bound here is exactly the mistake that rejected 96 of 96 points
        /// and left the maps with no kiosks at all. The real guard against a bad snap is the walk
        /// itself — <see cref="PlacementPolicy.PathIsReasonable"/> rejects anything that needs a
        /// detour to reach — so this only has to catch a snap that lands in a different place
        /// altogether.
        /// </summary>
        internal const float SnapTolerance = 12f;

        /// <summary>
        /// Path calculations one placement sweep may spend. Each one blocks the main thread
        /// until the graph answers, and a sweep would otherwise ask for up to a few hundred, so
        /// the budget bounds the stall a scene load can take. Geometric checks run first and
        /// reject most points for free, so in practice the allowance is rarely reached; when it
        /// is, the reject summary in the log names it as <c>budget-spent</c>.
        /// </summary>
        internal const int PlacementBudget = 64;

        /// <summary>
        /// The graph is only usable once <c>AstarPath</c> has scanned it and holds at least one
        /// graph; asking earlier returns empty results, and asking with no graphs makes the
        /// pathfinder log an error for every request.
        /// </summary>
        internal static bool Ready
        {
            get
            {
                var active = AstarPath.active;
                if (active == null || active.isScanning || active.data == null) return false;
                var graphs = active.data.graphs;
                return graphs != null && graphs.Length > 0;
            }
        }

        private static int _spent;

        /// <summary>Starts a fresh sweep so the budget applies per placement attempt.</summary>
        internal static void ResetBudget()
        {
            _spent = 0;
        }

        /// <summary>
        /// Node count across every graph, for the diagnostics log. A zero here means the
        /// pathfinder has not scanned yet, which is the single most useful fact when a scene
        /// refuses to place any kiosk.
        /// </summary>
        internal static int NodeCount()
        {
            var active = AstarPath.active;
            if (active == null || active.data == null) return 0;
            var graphs = active.data.graphs;
            if (graphs == null || graphs.Length == 0) return 0;
            var total = 0;
            foreach (var graph in graphs)
            {
                if (graph == null) continue;
                try
                {
                    total += graph.CountNodes();
                }
                catch (Exception)
                {
                    // A graph that is still being scanned is not worth failing over.
                }
            }
            return total;
        }

        /// <summary>Nearest walkable node to a point. <c>NNConstraint.Walkable</c> is what the game's own
        /// agents use, so an unwalkable node never counts as "standable ground".
        /// </summary>
        internal static bool TrySnap(Vector3 point, out Vector3 snapped, out string reason)
        {
            snapped = point;
            reason = "";
            if (!Ready)
            {
                reason = "graph-not-ready";
                return false;
            }
            NNInfo nearest;
            try
            {
                nearest = AstarPath.active.GetNearest(point, NNConstraint.Walkable);
            }
            catch (Exception exception)
            {
                reason = "graph-error:" + exception.GetType().Name;
                return false;
            }
            if (nearest.node == null)
            {
                reason = "no-node";
                return false;
            }
            snapped = nearest.position;
            return true;
        }

        /// <summary>
        /// Length of the walk an agent would take between two points. The path is calculated
        /// synchronously, so the caller may read the result immediately.
        ///
        /// <paramref name="budgeted"/> is for sweeps that try hundreds of candidates: those draw
        /// from <see cref="PlacementBudget"/>. Interaction checks pass false — a single distance
        /// query must never be refused because a placement sweep happened to exhaust the
        /// allowance, which would silently disable every kiosk in the map.
        /// </summary>
        internal static bool TryMeasure(Vector3 from, Vector3 to, out float length, out string reason,
            bool budgeted = true)
        {
            length = 0f;
            reason = "";
            if (!Ready)
            {
                reason = "graph-not-ready";
                return false;
            }
            if (budgeted)
            {
                if (_spent >= PlacementBudget)
                {
                    reason = "budget-spent";
                    return false;
                }
                _spent++;
            }
            ABPath? path = null;
            try
            {
                path = ABPath.Construct(from, to, null);
                AstarPath.StartPath(path, false, false);
                // StartPath does not block in play mode, so the wait has to be explicit.
                AstarPath.BlockUntilCalculated(path);
                if (path.CompleteState != PathCompleteState.Complete)
                {
                    reason = path.error ? FirstLine(path.errorLog) : "incomplete-" + path.CompleteState;
                    return false;
                }
                var total = path.GetTotalLength();
                // A failed path reports Infinity; never let that reach the detour arithmetic.
                if (float.IsNaN(total) || float.IsInfinity(total) || total <= 0f)
                {
                    reason = "bad-length";
                    return false;
                }
                length = total;
                return true;
            }
            catch (Exception exception)
            {
                reason = "path-error:" + exception.GetType().Name;
                return false;
            }
            finally
            {
                Release(path);
            }
        }

        /// <summary>
        /// Hands a finished path back to the pool. A calculated path is released by the return
        /// queue with <c>silent: true</c>, which leaves it unpooled, so every candidate would
        /// allocate a fresh instance; pooling here keeps the cost flat across the hundreds of
        /// points one placement sweep tries. A path that never left the queue is deliberately
        /// left alone: returning it to the pool while the processor still holds a reference
        /// would hand the same instance to a second caller.
        /// </summary>
        private static void Release(ABPath? path)
        {
            if (path == null) return;
            if (path.PipelineState < PathState.Returned) return;
            try
            {
                path.Release(AstarPath.active, false);
            }
            catch (Exception)
            {
                // Already released by the return queue; nothing to hand back.
            }
            try
            {
                PathPool.Pool(path);
            }
            catch (Exception)
            {
                // Already pooled by the release above.
            }
        }

        private static string FirstLine(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "path-failed";
            var newline = text!.IndexOf('\n');
            var line = newline < 0 ? text : text.Substring(0, newline);
            return line.Length > 60 ? line.Substring(0, 60) : line;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// The centre line of all road chunks that currently exist, as one line with a distance "s" along it (meters from the
    /// start of the road). Traffic and the generator use it to know where the road is: where is distance s, which way does
    /// it point, and how far along the road is this position.
    ///
    /// Chunks are added at the far end and removed at the near end, so s keeps growing and never restarts.
    /// </summary>
    public class RoadPath
    {
        readonly List<Vector3> points = new List<Vector3>();
        readonly List<float> distance = new List<float>(); // s of each point

        public int Count => points.Count;
        public float StartS => distance.Count > 0 ? distance[0] : 0f;
        public float EndS => distance.Count > 0 ? distance[distance.Count - 1] : 0f;

        public void Clear()
        {
            points.Clear();
            distance.Clear();
        }

        /// <summary>
        /// Adds a chunk's points at the end. The first point is skipped when it is where the line already ends (chunks join
        /// there). Returns how many points were added.
        /// </summary>
        public int Append(List<Vector3> worldPoints)
        {
            int added = 0;
            for (int i = 0; i < worldPoints.Count; i++)
            {
                Vector3 p = worldPoints[i];
                if (points.Count > 0)
                {
                    float step = Vector3.Distance(points[points.Count - 1], p);
                    if (step < 0.01f) continue;
                    distance.Add(distance[distance.Count - 1] + step);
                }
                else
                {
                    distance.Add(0f);
                }
                points.Add(p);
                added++;
            }
            return added;
        }

        /// <summary>Removes points from the near end (a chunk that is far behind).</summary>
        public void RemoveFirst(int count)
        {
            count = Mathf.Min(count, points.Count);
            if (count <= 0) return;
            points.RemoveRange(0, count);
            distance.RemoveRange(0, count);
        }

        // ------------------------------------------------------------------ queries

        int SegmentAt(float s)
        {
            if (points.Count < 2) return 0;

            int lo = 0, hi = distance.Count - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (distance[mid] <= s) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }

        /// <summary>The point on the centre line at distance s (clamped to the ends).</summary>
        public Vector3 Evaluate(float s)
        {
            if (points.Count == 0) return Vector3.zero;
            if (points.Count == 1) return points[0];

            int i = SegmentAt(s);
            float length = distance[i + 1] - distance[i];
            float t = length > 0.0001f ? Mathf.Clamp01((s - distance[i]) / length) : 0f;
            return Vector3.Lerp(points[i], points[i + 1], t);
        }

        /// <summary>The direction of the road at s, flat and normalized.</summary>
        public Vector3 Tangent(float s)
        {
            if (points.Count < 2) return Vector3.forward;

            int i = SegmentAt(s);
            Vector3 d = points[i + 1] - points[i];
            d.y = 0f;
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
        }

        /// <summary>Which way is right at s (flat and normalized).</summary>
        public Vector3 Right(float s) => Vector3.Cross(Vector3.up, Tangent(s)).normalized;

        /// <summary>
        /// How far along the road a position is. With a hint (a nearby s from before) only the road around it is searched, so
        /// roads that come close to themselves do not confuse it. A negative hint searches everything.
        /// </summary>
        public float ProjectS(Vector3 position, float sHint)
        {
            if (points.Count < 2) return 0f;

            int from = 0, to = points.Count - 2;
            if (sHint >= 0f)
            {
                int around = SegmentAt(sHint);
                from = Mathf.Max(0, around - 3);
                to = Mathf.Min(points.Count - 2, around + 8);
            }

            float bestS = distance[from];
            float bestSqr = float.MaxValue;
            Search(position, from, to, ref bestS, ref bestSqr);

            // lost it (teleported far away): look at the whole road
            if (sHint >= 0f && bestSqr > 30f * 30f)
                Search(position, 0, points.Count - 2, ref bestS, ref bestSqr);

            return bestS;
        }

        void Search(Vector3 position, int from, int to, ref float bestS, ref float bestSqr)
        {
            for (int i = from; i <= to; i++)
            {
                Vector3 a = points[i];
                Vector3 ab = points[i + 1] - a;
                float lengthSqr = ab.sqrMagnitude;
                float t = lengthSqr > 0.0001f ? Mathf.Clamp01(Vector3.Dot(position - a, ab) / lengthSqr) : 0f;

                float sqr = (a + ab * t - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestS = distance[i] + t * (distance[i + 1] - distance[i]);
                }
            }
        }
    }
}

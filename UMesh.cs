using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Projekt
{
    internal class UMesh
    {
        private readonly Func<double, double> GetContourLevelFromU;

        List<double> knownGridPoints = new List<double>();

        private PriorityQueue<(double Start, double End), double> intervalsToRefine = new();

        /// <summary>
        /// Gets the set of gridpoints in an ordered array.
        /// </summary>
        public double[] KnownGridPoints
        {
            get
            {
                var arr = knownGridPoints.ToArray();
                Array.Sort(arr);
                return arr;
            }
        }


        public UMesh(Func<double, double> getContourLevelFromU)
        {
            this.GetContourLevelFromU = getContourLevelFromU;

            knownGridPoints.Add(0);
            knownGridPoints.Add(1);

            intervalsToRefine.Enqueue((Start: 0, End: 1), -1);

            RefineIntervals();
        }

        private void RefineIntervals()
        {
            while (intervalsToRefine.Count > 0)
            {
                var intervalToRefine = intervalsToRefine.Dequeue();

                var contourAtStart = GetContourLevelFromU(intervalToRefine.Start);
                var contourAtEnd = GetContourLevelFromU(intervalToRefine.End);

                var uMid = (intervalToRefine.Start + intervalToRefine.End) / 2;

                var contourAtMid = GetContourLevelFromU(uMid);

                var interpolatedContourAtMiddle = (contourAtStart + contourAtEnd) / 2;

                if (Math.Abs(interpolatedContourAtMiddle - contourAtMid) > 0.01)
                {
                    knownGridPoints.Add(uMid);
                    intervalsToRefine.Enqueue((Start: intervalToRefine.Start, End: uMid), -(uMid - intervalToRefine.Start));
                    intervalsToRefine.Enqueue((Start: uMid, End: intervalToRefine.End), -(intervalToRefine.End - uMid));
                }
            }
        }
    }
}

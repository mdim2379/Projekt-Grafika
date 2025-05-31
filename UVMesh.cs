using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Projekt
{
    class UVMesh
    {
        private Func<double, double, Vector3D<float>> get3DPointFromUV;

        /// <summary>
        /// Each point can be accessed using its id.
        /// </summary>
        private Dictionary<int, Point> points;

        /// <summary>
        /// Each triangle appears in this dictionary cached for all three edges in anticlockwise order. A triangle is only contained at most once.
        /// </summary>
        private Dictionary<string, Triangle> trianglesByEdges;

        /// <summary>
        /// Contains every edge once, marked with its length.
        /// </summary>
        private PriorityQueue<Edge, double> edgesToRefineByLength;

        public IEnumerable<Triangle> Triangles => trianglesByEdges.Values.ToHashSet();

        public UVMesh(Func<double, double, Vector3D<float>> get3DPointFromUV)
        {
            this.get3DPointFromUV = get3DPointFromUV;

            this.points = new Dictionary<int, Point>();
            this.trianglesByEdges = new Dictionary<string, Triangle>();
            this.edgesToRefineByLength = new();

            // set the initial two tirangles to be refined
            var p00 = new Point(0, 0, points.Count);
            points.Add(p00.Id, p00);
            var p10 = new Point(1, 0, points.Count);
            points.Add(p10.Id, p10);
            var p01 = new Point(0, 1, points.Count);
            points.Add(p01.Id, p01);
            var p11 = new Point(1, 1, points.Count);
            points.Add(p11.Id, p11);

            RegisterTriangle(p00, p01, p10);
            RegisterTriangle(p01, p11, p10);

            RefineEdges();
        }

        /// <summary>
        /// Process every triangle in the order of longest edges and see if a refinement step is needed. Refine and iterate if needed.
        /// </summary>
        private void RefineEdges()
        {
            while (edgesToRefineByLength.Count > 0)
            {
                var edgeToRefine = edgesToRefineByLength.Dequeue();

                // get hlaf point in UV and in 3D
                var halfU = (edgeToRefine.A.U + edgeToRefine.B.U) / 2;
                var halfV = (edgeToRefine.A.V + edgeToRefine.B.V) / 2;

                var a3D = get3DPointFromUV(edgeToRefine.A.U, edgeToRefine.A.V);
                var b3D = get3DPointFromUV(edgeToRefine.B.U, edgeToRefine.B.V);

                var half3D = (a3D + b3D) / 2;
                var half3DEstimate = get3DPointFromUV(halfU, halfV);

                // if the approximated half point is close enough to the mapped half
                //if ((half3D - half3DEstimate).LengthSquared < (a3D - b3D).LengthSquared * 0.001)
                if ((half3D - half3DEstimate).LengthSquared < 0.001)
                {
                    continue;
                }

                // difference too big, split the edge, createa a new point and create 4 out of 2 triangles
                var halfPoint = new Point(halfU, halfV, points.Count);
                points.Add(halfPoint.Id, halfPoint);

                // get the two triangles on the two sides of the edge
                var triangleKeyAB = TriangleKeyFromEdge(edgeToRefine.A, edgeToRefine.B);
                var triangleKeyBA = TriangleKeyFromEdge(edgeToRefine.B, edgeToRefine.A);
                if (trianglesByEdges.ContainsKey(triangleKeyAB))
                {
                    // if there is a triangle on tha AB edge
                    var triangleAB = trianglesByEdges[triangleKeyAB];
                    // figure out the third point
                    var thirdPoint = triangleAB.OtherPoint(edgeToRefine);
                    // remove the triangle for each edge
                    UnRegisterTriangle(triangleAB);

                    // add the two new triangles
                    RegisterTriangle(edgeToRefine.A, halfPoint, thirdPoint);
                    RegisterTriangle(halfPoint, edgeToRefine.B, thirdPoint);
                }
                if (trianglesByEdges.ContainsKey(triangleKeyBA))
                {
                    // if there is a triangle on tha BA edge
                    var triangleBA = trianglesByEdges[triangleKeyBA];
                    // figure out the third point
                    var thirdPoint = triangleBA.OtherPoint(edgeToRefine);
                    // remove the triangle for each edge

                    UnRegisterTriangle(triangleBA);
                    // add the two new triangles
                    RegisterTriangle(edgeToRefine.B, halfPoint, thirdPoint);
                    RegisterTriangle(halfPoint, edgeToRefine.A, thirdPoint);
                }
            }
        }

        private void UnRegisterTriangle(Triangle triangleAB)
        {
            trianglesByEdges.Remove(TriangleKeyFromEdge(triangleAB.A, triangleAB.B));
            trianglesByEdges.Remove(TriangleKeyFromEdge(triangleAB.B, triangleAB.C));
            trianglesByEdges.Remove(TriangleKeyFromEdge(triangleAB.C, triangleAB.A));
        }

        /// <summary>
        /// Registers a triangle for all three edges defined by the three points.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void RegisterTriangle(Point a, Point b, Point c)
        {
            // create the triangle
            Triangle face = new(a, b, c);

            // register the triangle for all three edges
            trianglesByEdges.Add(TriangleKeyFromEdge(a, b), face);
            trianglesByEdges.Add(TriangleKeyFromEdge(b, c), face);
            trianglesByEdges.Add(TriangleKeyFromEdge(c, a), face);

            // register all three edge for refinement
            RegisterEdgeForRefinement(a, b);
            RegisterEdgeForRefinement(b, c);
            RegisterEdgeForRefinement(c, a);
        }

        private void RegisterEdgeForRefinement(Point a, Point b)
        {
            var ab = new Edge(a, b);
            if (ab.Length > 0.01)
                // priority queue work with smalles prio, so use - length
                edgesToRefineByLength.Enqueue(ab, -ab.Length);
        }

        private static string TriangleKeyFromEdge(Point a, Point b)
        {
            return $"{a.Id}-{b.Id}";
        }

        internal class Point
        {
            public readonly int Id;
            public readonly double U;
            public readonly double V;

            public Point(double u, double v, int id)
            {
                this.U = u;
                this.V = v;
                this.Id = id;
            }
        }

        internal class Edge
        {
            public readonly Point A;
            public readonly Point B;

            public Edge(Point a, Point b)
            {
                this.A = a;
                this.B = b;
            }

            public double Length => Math.Sqrt(Math.Pow(A.U - B.U, 2) + Math.Pow(A.V - B.V, 2));
        }

        internal class Triangle
        {
            public readonly Point A;
            public readonly Point B;
            public readonly Point C;

            public Triangle(Point a, Point b, Point c)
            {
                this.A = a;
                this.B = b;
                this.C = c;
            }

            public Edge LongestEdge
            {
                get
                {
                    double dAB = Math.Sqrt(Math.Pow(A.U - B.U, 2) + Math.Pow(A.V - B.V, 2));
                    double dBC = Math.Sqrt(Math.Pow(B.U - C.U, 2) + Math.Pow(B.V - C.V, 2));
                    double dCA = Math.Sqrt(Math.Pow(C.U - A.U, 2) + Math.Pow(C.V - A.V, 2));
                    if (dAB >= dBC && dAB >= dCA)
                        // AB is at leas as the other two
                        return new Edge(A, B);
                    else if (dBC >= dAB && dBC >= dCA)
                        // BC is at leas as the other two
                        return new Edge(B, C);
                    else
                        // CA is at leas as the other two
                        return new Edge(C, A);
                }
            }

            internal Point OtherPoint(Edge edgeToRefine)
            {
                if (A.Id != edgeToRefine.A.Id && A.Id != edgeToRefine.B.Id)
                    return A;
                if (B.Id != edgeToRefine.A.Id && B.Id != edgeToRefine.B.Id)
                    return B;
                if (C.Id != edgeToRefine.A.Id && C.Id != edgeToRefine.B.Id)
                    return C;
                else
                    throw new Exception("This should be unreachable.");
            }
        }
    }
}
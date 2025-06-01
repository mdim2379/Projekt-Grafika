using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace Projekt
{
    public class GlObject
    {
        public uint Vao { get; }
        public uint Vertices { get; }
        public uint Colors { get; }
        public uint Indices { get; }
        public uint IndexArrayLength { get; }
        
        public uint TextureId { get; set; } = 0;

        public float xEltolas { get; set; } = 0;
        public float zEltolas { get; set; } = 0;
        
        public Vector2D<double> position { get; set; } = new Vector2D<double>(0, 0); 

        private GL Gl;

        public GlObject(uint vao, uint vertices, uint colors, uint indeces, uint indexArrayLength, GL gl)
        {
            this.Vao = vao;
            this.Vertices = vertices;
            this.Colors = colors;
            this.Indices = indeces;
            this.IndexArrayLength = indexArrayLength;
            this.Gl = gl;
        }

        internal void ReleaseGlObject()
        {
            // always unbound the vertex buffer first, so no halfway results are displayed by accident
            Gl.DeleteBuffer(Vertices);
            Gl.DeleteBuffer(Colors);
            Gl.DeleteBuffer(Indices);
            Gl.DeleteVertexArray(Vao);
        }

        private static unsafe GlObject CreateGlObjectFromVertexDescriptions(GL Gl, List<float> vertexDescriptionList, List<float> colorsList, List<uint> triangleIndices)
        {
            uint vao = Gl.GenVertexArray();
            Gl.BindVertexArray(vao);

            uint offsetPos = 0;
            uint offsetNormal = offsetPos + (3 * sizeof(float));
            uint vertexSize = offsetNormal + (3 * sizeof(float));

            uint vertices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, vertices);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)vertexDescriptionList.ToArray().AsSpan(), GLEnum.StaticDraw);
            Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetPos);
            Gl.EnableVertexAttribArray(0);

            Gl.EnableVertexAttribArray(2);
            Gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetNormal);

            uint colors = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, colors);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)colorsList.ToArray().AsSpan(), GLEnum.StaticDraw);
            Gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 0, null);
            Gl.EnableVertexAttribArray(1);

            uint indices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, indices);
            Gl.BufferData(GLEnum.ElementArrayBuffer, (ReadOnlySpan<uint>)triangleIndices.ToArray().AsSpan(), GLEnum.StaticDraw);

            // release array buffer
            Gl.BindBuffer(GLEnum.ArrayBuffer, 0);
            uint indexArrayLength = (uint)triangleIndices.Count;

            return new GlObject(vao, vertices, colors, indices, indexArrayLength, Gl);
        }


        /// <summary>
        /// Creates a sphere at the origin with given <paramref name="radius"/> using <paramref name="minResolution"/> number of units in the u-v plane. 
        /// </summary>
        /// <param name="radius"></param>
        /// <param name="minResolution"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        internal static unsafe GlObject CreateSphere(float radius, GL Gl)
        {
            Dictionary<string, int> vertexDescription2IndexTable = new Dictionary<string, int>();
            List<float> vertexDescriptionList = new List<float>();
            List<float> colorsList = new List<float>();
            List<uint> triangleIndices = new List<uint>();

            Func<double, double, Vector3D<float>> get3DPointFromUV = (double u, double v) => GetSphereVertexPostion(radius, u, v);
            Func<double, double, Vector3D<float>, Vector3D<float>> sphereNormalCalculator = (double u, double v, Vector3D<float> v3D) => Vector3D.Normalize(v3D);

            CreateTrainglesOfUVMesh(get3DPointFromUV, sphereNormalCalculator, vertexDescription2IndexTable, vertexDescriptionList, colorsList, triangleIndices);

            return CreateGlObjectFromVertexDescriptions(Gl, vertexDescriptionList, colorsList, triangleIndices);
        }

        private static unsafe void CreateTrainglesOfUVMesh(
            Func<double, double, Vector3D<float>> get3DPointFromUV,
            Func<double, double, Vector3D<float>, Vector3D<float>> get3DNormalFromUV,
            Dictionary<string, int> vertexDescription2IndexTable, List<float> vertexDescriptionList, List<float> colorsList, List<uint> triangleIndices)
        {
            UVMesh mesh = new(get3DPointFromUV);

            // foreach mesh. tirangles
            foreach (var triangle in mesh.Triangles)
            {
                var vertexA = get3DPointFromUV(triangle.A.U, triangle.A.V);
                var vertexB = get3DPointFromUV(triangle.B.U, triangle.B.V);
                var vertexC = get3DPointFromUV(triangle.C.U, triangle.C.V);

                CreateTriangleFromVertices(get3DNormalFromUV, vertexDescription2IndexTable, vertexDescriptionList, colorsList, triangleIndices, triangle, vertexA, vertexB, vertexC);
            }
        }

        private static unsafe void CreateTriangleFromVertices(Func<double, double, Vector3D<float>, Vector3D<float>> get3DNormalFromUV, Dictionary<string, int> vertexDescription2IndexTable, List<float> vertexDescriptionList, List<float> colorsList, List<uint> triangleIndices, UVMesh.Triangle triangle, Vector3D<float> vertexA, Vector3D<float> vertexB, Vector3D<float> vertexC)
        {
            int vertexAIndex = GetIndexForVertex(vertexDescription2IndexTable, vertexDescriptionList, vertexA, colorsList, get3DNormalFromUV, triangle.A.U, triangle.A.V);
            int vertexBIndex = GetIndexForVertex(vertexDescription2IndexTable, vertexDescriptionList, vertexB, colorsList, get3DNormalFromUV, triangle.B.U, triangle.B.V);
            int vertexCIndex = GetIndexForVertex(vertexDescription2IndexTable, vertexDescriptionList, vertexC, colorsList, get3DNormalFromUV, triangle.C.U, triangle.C.V);


            triangleIndices.Add((uint)vertexAIndex);
            triangleIndices.Add((uint)vertexBIndex);
            triangleIndices.Add((uint)vertexCIndex);
        }

        private static unsafe int GetIndexForVertex(Dictionary<string, int> vertexDescription2IndexTable, List<float> vertexDescriptionList, Vector3D<float> vertex, List<float> colorsList, Func<double, double, Vector3D<float>, Vector3D<float>> calculateNormalAtVertex, double u, double v)
        {
            int vertexIndex = -1;
            var normal = calculateNormalAtVertex(u, v, vertex);
            string key = $"v: {vertex.X}, {vertex.Y}, {vertex.Z}; n: {normal.X}, {normal.Y}, {normal.Z};";
            if (!vertexDescription2IndexTable.ContainsKey(key))
            {
                vertexDescriptionList.Add((float)vertex.X);
                vertexDescriptionList.Add((float)vertex.Y);
                vertexDescriptionList.Add((float)vertex.Z);
                vertexDescriptionList.Add((float)normal.X);
                vertexDescriptionList.Add((float)normal.Y);
                vertexDescriptionList.Add((float)normal.Z);

                colorsList.AddRange(new float[] { 1f, 0f, 0f, 1f });

                vertexIndex = vertexDescription2IndexTable.Count;
                vertexDescription2IndexTable.Add(key, vertexDescription2IndexTable.Count);
            }
            else
            {
                vertexIndex = vertexDescription2IndexTable[key];
            }

            return vertexIndex;
        }

        private static Vector3D<float> GetSphereVertexPostion(float radius, double u, double v)
        {
            return new Vector3D<float>(
                                    (float)(radius * Math.Cos(GetAlphaFromU(u)) * Math.Cos(GetBetaFromV(v))),
                                    (float)(radius * Math.Sin(GetAlphaFromU(u))),
                                    (float)(radius * Math.Cos(GetAlphaFromU(u)) * Math.Sin(GetBetaFromV(v))));
        }

        private static double GetBetaFromV(double v)
        {
            return v * 2 * Math.PI;
        }

        private static double GetAlphaFromU(double u)
        {
            return u * Math.PI - Math.PI / 2;
        }
    }
}

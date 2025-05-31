using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace Projekt
{
    internal class ObjResourceReader
    {
        public static unsafe GlObject CreateTeapotWithColor(GL Gl, float[] faceColor)
        {
            uint vao = Gl.GenVertexArray();
            Gl.BindVertexArray(vao);

            List<float[]> objVertices;
            List<float[]> objNormals;
            List<int[]> objFaces;

            ReadObjDataForTeapot(out objVertices, out objNormals, out objFaces);

            List<float> glVertices = new List<float>();
            List<float> glColors = new List<float>();
            List<uint> glIndices = new List<uint>();

            CreateGlArraysFromObjArrays(faceColor, objVertices, objNormals, objFaces, glVertices, glColors, glIndices);

            return CreateOpenGlObject(Gl, vao, glVertices, glColors, glIndices);
        }

        private static unsafe GlObject CreateOpenGlObject(GL Gl, uint vao, List<float> glVertices, List<float> glColors,
            List<uint> glIndices)
        {
            uint offsetPos = 0;
            uint offsetNormal = offsetPos + (3 * sizeof(float));
            uint vertexSize = offsetNormal + (3 * sizeof(float));

            uint vertices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, vertices);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)glVertices.ToArray().AsSpan(), GLEnum.StaticDraw);
            Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetPos);
            Gl.EnableVertexAttribArray(0);

            Gl.EnableVertexAttribArray(2);
            Gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetNormal);

            uint colors = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, colors);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)glColors.ToArray().AsSpan(), GLEnum.StaticDraw);
            Gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 0, null);
            Gl.EnableVertexAttribArray(1);

            uint indices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, indices);
            Gl.BufferData(GLEnum.ElementArrayBuffer, (ReadOnlySpan<uint>)glIndices.ToArray().AsSpan(),
                GLEnum.StaticDraw);

            // release array buffer
            Gl.BindBuffer(GLEnum.ArrayBuffer, 0);
            uint indexArrayLength = (uint)glIndices.Count;

            return new GlObject(vao, vertices, colors, indices, indexArrayLength, Gl);
        }

        private static unsafe void CreateGlArraysFromObjArrays(
            float[] faceColor,
            List<float[]> objVertices,
            List<float[]> objNormals,
            List<int[]> objFaces,
            List<float> glVertices,
            List<float> glColors,
            List<uint> glIndices)
        {
            Dictionary<string, int> glVertexIndices = new Dictionary<string, int>();

            foreach (var objFace in objFaces)
            {
                // process 3 vertices
                for (int i = 0; i < objFace.Length; ++i)
                {
                    // OBJ face format: vertex_index/texture_index/normal_index
                    var faceIndices = objFace[i].ToString().Split('/');
                    int vertexIndex = int.Parse(faceIndices[0]) - 1;
                    int normalIndex = faceIndices.Length > 2 && !string.IsNullOrEmpty(faceIndices[2])
                        ? int.Parse(faceIndices[2]) - 1
                        : -1;

                    var objVertex = objVertices[vertexIndex];
                    float[] normal;

                    if (normalIndex >= 0 && normalIndex < objNormals.Count)
                    {
                        // Use the normal from the OBJ file
                        normal = objNormals[normalIndex];
                    }
                    else
                    {
                        // Fallback: calculate normal from face vertices
                        var aObjVertex = objVertices[int.Parse(objFace[0].ToString().Split('/')[0]) - 1];
                        var a = new Vector3D<float>(aObjVertex[0], aObjVertex[1], aObjVertex[2]);
                        var bObjVertex = objVertices[int.Parse(objFace[1].ToString().Split('/')[0]) - 1];
                        var b = new Vector3D<float>(bObjVertex[0], bObjVertex[1], bObjVertex[2]);
                        var cObjVertex = objVertices[int.Parse(objFace[2].ToString().Split('/')[0]) - 1];
                        var c = new Vector3D<float>(cObjVertex[0], cObjVertex[1], cObjVertex[2]);

                        var calculatedNormal = Vector3D.Normalize(Vector3D.Cross(b - a, c - a));
                        normal = new float[] { calculatedNormal.X, calculatedNormal.Y, calculatedNormal.Z };
                    }

                    // create gl description of vertex
                    List<float> glVertex = new List<float>();
                    glVertex.AddRange(objVertex);
                    glVertex.AddRange(normal);

                    // add textrure, color if needed

                    // check if vertex exists
                    var glVertexStringKey = string.Join(" ", glVertex);
                    if (!glVertexIndices.ContainsKey(glVertexStringKey))
                    {
                        glVertices.AddRange(glVertex);
                        glColors.AddRange(faceColor);
                        glVertexIndices.Add(glVertexStringKey, glVertexIndices.Count);
                    }

                    glIndices.Add((uint)glVertexIndices[glVertexStringKey]);
                }
            }
        }

        private static unsafe void ReadObjDataForTeapot(
            out List<float[]> objVertices,
            out List<float[]> objNormals,
            out List<int[]> objFaces)
        {
            objVertices = new List<float[]>();
            objNormals = new List<float[]>();
            objFaces = new List<int[]>();

            using (Stream objStream =
                   typeof(ObjResourceReader).Assembly.GetManifestResourceStream("Projekt.Resources.goose.obj"))
            using (StreamReader objReader = new StreamReader(objStream))
            {
                while (!objReader.EndOfStream)
                {
                    var line = objReader.ReadLine()?.Trim();

                    if (String.IsNullOrEmpty(line) || line.StartsWith("#"))
                        continue;

                    // Find the first space safely
                    int spaceIndex = line.IndexOf(' ');
                    if (spaceIndex < 0)
                        continue; // Skip lines without any space

                    var lineClassifier = line.Substring(0, spaceIndex);
                    var lineData = line.Substring(spaceIndex).Trim()
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    switch (lineClassifier)
                    {
                        case "v":
                            float[] vertex = new float[3];
                            for (int i = 0; i < vertex.Length && i < lineData.Length; ++i)
                                vertex[i] = float.Parse(lineData[i]);
                            objVertices.Add(vertex);
                            break;
                        case "vn":
                            float[] normal = new float[3];
                            for (int i = 0; i < normal.Length && i < lineData.Length; ++i)
                                normal[i] = float.Parse(lineData[i]);
                            objNormals.Add(normal);
                            break;
                        case "f":
                            int[] face = new int[3];
                            for (int i = 0; i < face.Length && i < lineData.Length; ++i)
                            {
                                var faceParts = lineData[i].Split('/');
                                if (faceParts.Length > 0 && !string.IsNullOrEmpty(faceParts[0]))
                                    face[i] = int.Parse(faceParts[0]);
                            }

                            objFaces.Add(face);
                            break;
                    }
                }
            }
        }
    }
}
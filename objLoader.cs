using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace Projekt
{ 
    internal class ObjLoader
    {
        private struct ObjVertex
        {
            public int PositionIndex;
            public int TexCoordIndex;
            public int NormalIndex;

            public ObjVertex(int pos, int tex, int norm)
            {
                PositionIndex = pos;
                TexCoordIndex = tex;
                NormalIndex = norm;
            }
        }

        public static unsafe GlObject CreateFromObj(
            GL gl, 
            string objResourceName, 
            string textureResourceName = null)
        {
            // Parse OBJ data
            var (vertices, texCoords, normals, faces, material) = ParseObjData(objResourceName);
            
            // Create interleaved vertex data
            var (vertexData, indices) = CreateInterleavedData(vertices, texCoords, normals, faces);
            
            // Load texture
            uint textureId = 0;
            if (!string.IsNullOrEmpty(textureResourceName))
            {
                textureId = LoadTexture(gl, textureResourceName);
            }
            else if (!string.IsNullOrEmpty(material?.DiffuseTexture))
            {
                textureId = LoadTexture(gl, material.DiffuseTexture);
            }
            
            // Create OpenGL objects
            return CreateGlObject(gl, vertexData, indices, textureId);
        }

        private static unsafe GlObject CreateGlObject(
            GL gl, 
            float[] vertexData, 
            uint[] indices, 
            uint textureId)
        {
            uint vao = gl.GenVertexArray();
            gl.BindVertexArray(vao);

            uint vertexBuffer = gl.GenBuffer();
            gl.BindBuffer(GLEnum.ArrayBuffer, vertexBuffer);
            fixed (void* v = &vertexData[0])
            {
                gl.BufferData(GLEnum.ArrayBuffer, (nuint)(vertexData.Length * sizeof(float)), 
                             v, GLEnum.StaticDraw);
            }

            // Set vertex attributes
            // Position (0)
            gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 
                                  8 * sizeof(float), (void*)0);
            gl.EnableVertexAttribArray(0);
            
            // Normal (1)
            gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 
                                  8 * sizeof(float), (void*)(3 * sizeof(float)));
            gl.EnableVertexAttribArray(1);
            
            // Texture Coordinate (2)
            gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 
                                  8 * sizeof(float), (void*)(6 * sizeof(float)));
            gl.EnableVertexAttribArray(2);

            uint elementBuffer = gl.GenBuffer();
            gl.BindBuffer(GLEnum.ElementArrayBuffer, elementBuffer);
            fixed (void* i = &indices[0])
            {
                gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), 
                             i, GLEnum.StaticDraw);
            }

            gl.BindVertexArray(0);

            return new GlObject(vao, vertexBuffer, elementBuffer, 
                              (uint)indices.Length, textureId, gl);
        }

        private static (float[] vertexData, uint[] indices) CreateInterleavedData(
            List<Vector3D<float>> positions,
            List<Vector2D<float>> texCoords,
            List<Vector3D<float>> normals,
            List<List<ObjVertex>> faces)
        {
            var vertexMap = new Dictionary<ObjVertex, uint>();
            var vertexData = new List<float>();
            var indices = new List<uint>();

            foreach (var face in faces)
            {
                // Triangulate faces (convert quads/polygons to triangles)
                for (int i = 1; i < face.Count - 1; i++)
                {
                    var vertices = new[] { face[0], face[i], face[i + 1] };
                    foreach (var vertex in vertices)
                    {
                        if (!vertexMap.TryGetValue(vertex, out uint index))
                        {
                            index = (uint)vertexMap.Count;
                            vertexMap[vertex] = index;
                            
                            // Add position
                            var pos = positions[vertex.PositionIndex];
                            vertexData.Add(pos.X);
                            vertexData.Add(pos.Y);
                            vertexData.Add(pos.Z);
                            
                            // Add normal
                            var normal = vertex.NormalIndex >= 0 && vertex.NormalIndex < normals.Count
                                ? normals[vertex.NormalIndex]
                                : Vector3D<float>.Zero;

                            vertexData.Add(normal.X);
                            vertexData.Add(normal.Y);
                            vertexData.Add(normal.Z);
                            
                            // Add texture coordinate
                            var texCoord = vertex.TexCoordIndex >= 0 ? 
                                texCoords[vertex.TexCoordIndex] : 
                                Vector2D<float>.Zero;
                            vertexData.Add(texCoord.X);
                            vertexData.Add(texCoord.Y);
                        }
                        indices.Add(index);
                    }
                }
            }

            return (vertexData.ToArray(), indices.ToArray());
        }

        private static unsafe uint LoadTexture(GL gl, string resourceName)
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(resourceName);
            
            using var image = Image.Load<Rgba32>(stream);
            image.Mutate(x => x.Flip(FlipMode.Vertical));
            
            uint texture = gl.GenTexture();
            gl.BindTexture(GLEnum.Texture2D, texture);
            
            // Get pixel data
            byte[] pixelData = new byte[4 * image.Width * image.Height];
            image.CopyPixelDataTo(pixelData);
            
            // Upload to GPU
            fixed (void* ptr = pixelData)
            {
                gl.TexImage2D(GLEnum.Texture2D, 0, (int)InternalFormat.Rgba, 
                             (uint)image.Width, (uint)image.Height, 
                             0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
            }
            
            // Set parameters
            gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.Repeat);
            gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.Repeat);
            gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
            gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            
            gl.GenerateMipmap(GLEnum.Texture2D);
            gl.BindTexture(GLEnum.Texture2D, 0);
            
            return texture;
        }

        private static (
            List<Vector3D<float>> positions,
            List<Vector2D<float>> texCoords,
            List<Vector3D<float>> normals,
            List<List<ObjVertex>> faces,
            ObjMaterial material
        ) ParseObjData(string resourceName)
        {
            var positions = new List<Vector3D<float>>();
            var texCoords = new List<Vector2D<float>>();
            var normals = new List<Vector3D<float>>();
            var faces = new List<List<ObjVertex>>();
            ObjMaterial material = null;
            string currentMaterial = null;

            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream);

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) 
                    continue;

                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                switch (parts[0])
                {
                    case "v": // Vertex position
                        positions.Add(new Vector3D<float>(
                            float.Parse(parts[1]),
                            float.Parse(parts[2]),
                            float.Parse(parts[3])
                        ));
                        break;
                    
                    case "vt": // Texture coordinate
                        texCoords.Add(new Vector2D<float>(
                            float.Parse(parts[1]),
                            1 - float.Parse(parts[2]) // Flip V coordinate
                        ));
                        break;
                    
                    case "vn": // Vertex normal
                        normals.Add(new Vector3D<float>(
                            float.Parse(parts[1]),
                            float.Parse(parts[2]),
                            float.Parse(parts[3])
                        ));
                        break;
                    
                    case "f": // Face
                        var face = new List<ObjVertex>();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            var indices = parts[i].Split('/');
                            int posIdx = int.Parse(indices[0]) - 1;
                            int texIdx = indices.Length > 1 && !string.IsNullOrEmpty(indices[1]) ? 
                                int.Parse(indices[1]) - 1 : -1;
                            int normIdx = indices.Length > 2 ? int.Parse(indices[2]) - 1 : -1;
                            
                            face.Add(new ObjVertex(posIdx, texIdx, normIdx));
                        }
                        faces.Add(face);
                        break;
                    
                    case "mtllib": // Material library
                        var mtlPath = parts[1];
                        material = LoadMaterial(Path.Combine(
                            Path.GetDirectoryName(resourceName) ?? string.Empty,
                            mtlPath
                        ));
                        break;
                    
                    case "usemtl": // Use material
                        currentMaterial = parts[1];
                        break;
                }
            }

            return (positions, texCoords, normals, faces, material);
        }

        private static ObjMaterial LoadMaterial(string mtlResourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            
            var resourceNames = assembly.GetManifestResourceNames();
            var fullName = resourceNames.FirstOrDefault(r => r.EndsWith(mtlResourceName, StringComparison.OrdinalIgnoreCase));
            if (fullName == null)
            {
                return null;
            }

            using var stream = assembly.GetManifestResourceStream(fullName);
            if (stream == null)
            {
                return null;
            }

            var material = new ObjMaterial();
            using var reader = new StreamReader(stream);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                if (parts[0] == "map_Kd" && parts[1] != "None")
                {
                    material.DiffuseTexture = parts[1];
                }
            }

            return material;
        }

        private class ObjMaterial
        {
            public string DiffuseTexture { get; set; }
        }
    }
}
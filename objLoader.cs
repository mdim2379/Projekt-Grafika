using Silk.NET.Maths;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Globalization;

namespace Projekt
{
    internal class ObjWithTextureLoader
    {
        public static unsafe GlObject LoadFromResources(GL gl, string objResourceName, string textureResourceName)
        {
            List<Vector3D<float>> objVertices = new();
            List<Vector3D<float>> objNormals = new();
            List<Vector2D<float>> objTexCoords = new();
            List<(int v, int t, int n)[]> objFaces = new();

            using (Stream? objStream = typeof(ObjWithTextureLoader).Assembly.GetManifestResourceStream(objResourceName))
            using (StreamReader sr =
                   new(objStream ?? throw new InvalidOperationException($"Missing resource: {objResourceName}")))
            {
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;

                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    switch (parts[0])
                    {
                        case "v":
                            objVertices.Add(new Vector3D<float>(
                                float.Parse(parts[1], CultureInfo.InvariantCulture),
                                float.Parse(parts[2], CultureInfo.InvariantCulture),
                                float.Parse(parts[3], CultureInfo.InvariantCulture)));
                            break;
                        case "vt":
                            objTexCoords.Add(new Vector2D<float>(
                                float.Parse(parts[1], CultureInfo.InvariantCulture),
                                1 - float.Parse(parts[2], CultureInfo.InvariantCulture))); // Flip V
                            break;
                        case "vn":
                            objNormals.Add(new Vector3D<float>(
                                float.Parse(parts[1], CultureInfo.InvariantCulture),
                                float.Parse(parts[2], CultureInfo.InvariantCulture),
                                float.Parse(parts[3], CultureInfo.InvariantCulture)));
                            break;
                        case "f":
                            var face = parts[1..4]
                                .Select(p =>
                                {
                                    var indices = p.Split('/');
                                    return (
                                        int.Parse(indices[0]) - 1,
                                        indices.Length > 1 && !string.IsNullOrEmpty(indices[1])
                                            ? int.Parse(indices[1]) - 1
                                            : -1,
                                        indices.Length > 2 && !string.IsNullOrEmpty(indices[2])
                                            ? int.Parse(indices[2]) - 1
                                            : -1
                                    );
                                }).ToArray();
                            objFaces.Add(face);
                            break;
                    }
                }
            }

            List<float> interleaved = new();
            List<uint> indices = new();
            Dictionary<string, uint> uniqueVertices = new();

            foreach (var face in objFaces)
            {
                foreach (var (v, t, n) in face)
                {
                    string key = $"{v}/{t}/{n}";
                    if (!uniqueVertices.ContainsKey(key))
                    {
                        Vector3D<float> vertex = objVertices[v];
                        Vector2D<float> tex = (t >= 0) ? objTexCoords[t] : new(0, 0);
                        Vector3D<float> norm = (n >= 0) ? objNormals[n] : new(0, 0, 1);

                        interleaved.AddRange([vertex.X, vertex.Y, vertex.Z]);
                        interleaved.AddRange([tex.X, tex.Y]);
                        interleaved.AddRange([norm.X, norm.Y, norm.Z]);

                        uniqueVertices[key] = (uint)(interleaved.Count / 8 - 1);
                    }

                    indices.Add(uniqueVertices[key]);
                }
            }

            uint vao = gl.GenVertexArray();
            gl.BindVertexArray(vao);

            uint vbo = gl.GenBuffer();
            gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
            fixed (float* data = interleaved.ToArray())
            {
                gl.BufferData(GLEnum.ArrayBuffer, (nuint)(interleaved.Count * sizeof(float)), data, GLEnum.StaticDraw);
            }

            int stride = 8 * sizeof(float);
            gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, (uint)stride, (void*)0);
            gl.EnableVertexAttribArray(0);

            gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, (uint)stride,
                (void*)(3 * sizeof(float)));
            gl.EnableVertexAttribArray(1);

            gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, (uint)stride,
                (void*)(5 * sizeof(float)));
            gl.EnableVertexAttribArray(2);

            uint ebo = gl.GenBuffer();
            gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
            fixed (uint* idx = indices.ToArray())
            {
                gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(indices.Count * sizeof(uint)), idx, GLEnum.StaticDraw);
            }

            uint textureId = LoadEmbeddedTexture(gl, textureResourceName);

            return new GlObject(vao, vbo, 0, ebo, (uint)indices.Count, gl, textureId);
        }

        private static unsafe uint LoadEmbeddedTexture(GL gl, string textureResourceName)
        {
            using Stream? stream = typeof(ObjWithTextureLoader).Assembly.GetManifestResourceStream(textureResourceName);
            if (stream == null)
                throw new InvalidOperationException($"Texture resource '{textureResourceName}' not found.");

            using Image<Rgba32> image = Image.Load<Rgba32>(stream);
            image.Mutate(x => x.Flip(FlipMode.Vertical));
            var pixels = new byte[4 * image.Width * image.Height];
            image.CopyPixelDataTo(pixels);

            fixed (void* data = pixels)
            {
                uint texture = gl.GenTexture();
                gl.BindTexture(TextureTarget.Texture2D, texture);
                gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba, (uint)image.Width,
                    (uint)image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data);

                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
                // gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
                // gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
                // gl.GenerateMipmap(TextureTarget.Texture2D);
                return texture;
            }
        }
    }

}

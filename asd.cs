// using Silk.NET.OpenGL;
// using System;
// using System.Drawing;
// using System.Drawing.Imaging;
// using System.IO;
// using System.Runtime.InteropServices;
//
// namespace Projekt
// {
//     internal static class TextureLoader
//     {
//         public static unsafe uint LoadTextureFromResource(GL Gl, string resourceName)
//         {
//             using var stream = typeof(ObjResourceReader).Assembly.GetManifestResourceStream(resourceName);
//             if (stream == null)
//                 throw new FileNotFoundException($"Resource {resourceName} not found.");
//
//             using var bitmap = new Bitmap(stream);
//             
//             bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
//
//             /// Lock bitmap data for reading pixels
//             var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
//             var bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
//
//             int length = Math.Abs(bitmapData.Stride) * bitmap.Height;
//             byte[] pixels = new byte[length];
//             Marshal.Copy(bitmapData.Scan0, pixels, 0, length);
//
//             bitmap.UnlockBits(bitmapData);
//
//             /// Generate and bind OpenGL texture
//             uint texture = Gl.GenTexture();
//             Gl.BindTexture(TextureTarget.Texture2D, texture);
//
//             /// Upload pixel data to OpenGL texture (BGRA to RGBA conversion)
//             fixed (byte* p = pixels)
//             {
//                 Gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)bitmap.Width, (uint)bitmap.Height, 0,
//                     Silk.NET.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, p);
//             }
//
//             /// Set texture parameters
//             Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
//             Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
//             Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
//             Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
//
//             /// Unbind texture
//             Gl.BindTexture(TextureTarget.Texture2D, 0);
//
//             return texture;
//         }
//     }
// }
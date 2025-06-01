using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace Projekt
{
    internal static class Program
    {
        private static int camera = 0;
        
        private static Random random = new();
        
        private static CameraDescriptor camPan = new();
        private static CameraDescriptor2 cam3rd = new();

        private static CubeArrangementModel cubeArrangementModel = new();

        private static IWindow window;

        private static IInputContext inputContext;

        private static GL Gl;

        private static ImGuiController controller;

        private static uint program;

        private static GlObject teapot;

        private static GlObject table;

        private static GlCube glCubeRotating;

        private static GlCube skyBox;

        private static GlObject[] glSphere = new GlObject[10];
        
        private static int[] eltolas = new int[10];

        private static float Shininess = 50;

        private static bool DrawWireFrameOnly = false;

        private const string ModelMatrixVariableName = "uModel";
        private const string NormalMatrixVariableName = "uNormal";
        private const string ViewMatrixVariableName = "uView";
        private const string ProjectionMatrixVariableName = "uProjection";

        private const string TextureUniformVariableName = "uTexture";

        private const string LightColorVariableName = "lightColor";
        private const string LightPositionVariableName = "lightPos";
        private const string ViewPosVariableName = "viewPos";
        private const string ShininessVariableName = "shininess";

        static void Main(string[] args)
        {
            WindowOptions windowOptions = WindowOptions.Default;
            windowOptions.Title = "Balls bonzana";
            windowOptions.Size = new Vector2D<int>(1000, 1000);
            
            windowOptions.PreferredDepthBufferBits = 24;

            window = Window.Create(windowOptions);

            window.Load += Window_Load;
            window.Update += Window_Update;
            window.Render += Window_Render;
            window.Closing += Window_Closing;

            window.Run();
        }

        private static void Window_Load()
        {
            
            inputContext = window.CreateInput();
            KeyStateTracker.Initialize(inputContext);

            Gl = window.CreateOpenGL();

            controller = new ImGuiController(Gl, window, inputContext);
            
            window.FramebufferResize += s =>
            {
                Gl.Viewport(s);
            };


            Gl.ClearColor(System.Drawing.Color.White);

            SetUpObjects();

            LinkProgram();

            // Gl.Enable(EnableCap.CullFace);
            Gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Line);
            
            Gl.Enable(EnableCap.DepthTest);
            Gl.DepthFunc(DepthFunction.Lequal);

            for (int i = 0; i < 10; i++)
                eltolas[i] = random.Next(180 - 2 * 5);
        }

        public static class KeyStateTracker
        {
            private static readonly HashSet<Key> _pressedKeys = new();

            public static void Initialize(IInputContext inputContext)
            {
                foreach (var keyboard in inputContext.Keyboards)
                {
                    keyboard.KeyDown += OnKeyDown;
                    keyboard.KeyUp += OnKeyUp;
                }
            }

            private static void OnKeyDown(IKeyboard keyboard, Key key, int arg3)
            {
                _pressedKeys.Add(key);
            }

            private static void OnKeyUp(IKeyboard keyboard, Key key, int arg3)
            {
                _pressedKeys.Remove(key);
            }

            public static bool IsKeyDown(Key key) => _pressedKeys.Contains(key);
        }
        
        private static void LinkProgram()
        {
            uint vshader = Gl.CreateShader(ShaderType.VertexShader);
            uint fshader = Gl.CreateShader(ShaderType.FragmentShader);

            Gl.ShaderSource(vshader, ReadShader("VertexShader.vert"));
            Gl.CompileShader(vshader);
            Gl.GetShader(vshader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
                throw new Exception("Vertex shader failed to compile: " + Gl.GetShaderInfoLog(vshader));

            Gl.ShaderSource(fshader, ReadShader("FragmentShader.frag"));
            Gl.CompileShader(fshader);

            program = Gl.CreateProgram();
            Gl.AttachShader(program, vshader);
            Gl.AttachShader(program, fshader);
            Gl.LinkProgram(program);
            Gl.GetProgram(program, GLEnum.LinkStatus, out var status);
            if (status == 0)
            {
                Console.WriteLine($"Error linking shader {Gl.GetProgramInfoLog(program)}");
            }
            Gl.DetachShader(program, vshader);
            Gl.DetachShader(program, fshader);
            Gl.DeleteShader(vshader);
            Gl.DeleteShader(fshader);
        }

        private static string ReadShader(string shaderFileName)
        {
            using (Stream shaderStream = typeof(Program).Assembly.GetManifestResourceStream("Projekt.Shaders." + shaderFileName))
            using (StreamReader shaderReader = new StreamReader(shaderStream))
                return shaderReader.ReadToEnd();
        }
        

        private static void Window_Update(double deltaTime)
        {
            controller.Update((float)deltaTime);
            
            cubeArrangementModel.AdvanceTime(deltaTime);
            if (KeyStateTracker.IsKeyDown(Key.D))
            {
                glSphere[0].xEltolas += 0.2f;
                glSphere[0].position += new Vector2D<double>(0.2f, 0f); 
            }
            if (KeyStateTracker.IsKeyDown(Key.W))
            {
                glSphere[0].zEltolas -= 0.2f;
                glSphere[0].position += new Vector2D<double>(0f, -0.2f); 
            }
            if (KeyStateTracker.IsKeyDown(Key.S))
            {
                glSphere[0].zEltolas += 0.2f;
                glSphere[0].position += new Vector2D<double>(0f, 0.2f); 
            }
            if (KeyStateTracker.IsKeyDown(Key.A))
            {
                glSphere[0].xEltolas -= 0.2f;
                glSphere[0].position += new Vector2D<double>(-0.2f, 0f); 
            }
            if (camera == 1)
            {
                cam3rd.GroundPosition = glSphere[0].position;
            }
        }

        private static unsafe void Window_Render(double deltaTime)
        {
            //Console.WriteLine($"Render after {deltaTime} [s].");

            // GL here
            Gl.Clear(ClearBufferMask.ColorBufferBit);
            Gl.Clear(ClearBufferMask.DepthBufferBit);

            if (DrawWireFrameOnly)
            {
                Gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
                Gl.Enable(EnableCap.LineSmooth);
                Gl.LineWidth(0.5f);
            }
            else
            {
                Gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
                Gl.Enable(EnableCap.LineSmooth);
                Gl.LineWidth(0.5f);
            }


            Gl.UseProgram(program);

            SetViewMatrix();
            SetProjectionMatrix();

            SetLightColor();
            SetLightPosition();
            SetViewerPosition();
            SetShininess();
            
            DrawSphere(0, 0 ,0);
            
            for (int i = 1; i < 10; i++) {
                float y = -180.0f/2 + (i + 0.5f) * (180.0f / 10);
                float x = -180.0f/2 + 5 + eltolas[i];
                DrawSphere(i, x, y);
            }

            DrawGoose();
            
            DrawSkyBox();
            if (ImGui.Button("switch"))
            {
                if(camera == 0)
                    camera = 1;
                else
                {
                    camera = 0;
                }
            }
            
            ImGuiNET.ImGui.End();
            controller.Render();
        }

        private static unsafe void DrawSkyBox()
        {
            Matrix4X4<float> modelMatrix = Matrix4X4.CreateScale(400f);
            SetModelMatrix(modelMatrix);
            Gl.BindVertexArray(skyBox.Vao);

            int textureLocation = Gl.GetUniformLocation(program, TextureUniformVariableName);
            if (textureLocation == -1)
            {
                throw new Exception($"{TextureUniformVariableName} uniform not found on shader.");
            }
            // set texture 0
            Gl.Uniform1(textureLocation, 0);

            Gl.ActiveTexture(TextureUnit.Texture0);
            Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (float)GLEnum.Linear);
            Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (float)GLEnum.Linear);
            Gl.BindTexture(TextureTarget.Texture2D, skyBox.Texture.Value);

            Gl.DrawElements(GLEnum.Triangles, skyBox.IndexArrayLength, GLEnum.UnsignedInt, null);
            Gl.BindVertexArray(0);

            CheckError();
            Gl.BindTexture(TextureTarget.Texture2D, 0);
            CheckError();
        }

        private static unsafe void DrawSphere(int i, float x, float z)
        {
            Matrix4X4<float> modelMatrix = Matrix4X4.CreateTranslation(x + glSphere[i].xEltolas, 5f, z + glSphere[i].zEltolas - 50f);
            SetModelMatrix(modelMatrix);
            Gl.BindVertexArray(glSphere[i].Vao);

            Gl.DrawElements(GLEnum.Triangles, glSphere[i].IndexArrayLength, GLEnum.UnsignedInt, null);
            Gl.BindVertexArray(0);

            CheckError();
        }

        private static unsafe void DrawGoose()
        {
            var scale = Matrix4X4.CreateScale(50f);
            float orbitRadius = 3f;
            float orbitHeight = 2f;
            float orbitSpeed = (float)cubeArrangementModel.CenterCubeOrbitAngle;

            float orbitX = orbitRadius * (float)Math.Sin(orbitSpeed * (float)Math.PI / 180.0f);
            float orbitZ = orbitRadius * (float)Math.Cos(orbitSpeed * (float)Math.PI / 180.0f);

            float nextAngle = orbitSpeed + 1f;
            float nextX = orbitRadius * (float)Math.Sin(nextAngle * (float)Math.PI / 180.0f);
            float nextZ = orbitRadius * (float)Math.Cos(nextAngle * (float)Math.PI / 180.0f);

            float forwardX = nextX - orbitX;
            float forwardZ = nextZ - orbitZ;

            float faceAngle = (float)Math.Atan2(forwardX, forwardZ) * (180.0f / (float)Math.PI);

            float wingFlap = (float)Math.Sin(orbitSpeed * 0.2f) * 0.3f;

            float bankAngle = (float)Math.Sin(orbitSpeed * 0.1f) * 10f;
            var bankRotation = Matrix4X4.CreateRotationZ(bankAngle * (float)Math.PI / 180.0f);

            var bodyRotation = Matrix4X4.CreateRotationY(faceAngle * (float)Math.PI / 180.0f);

            var orbit = Matrix4X4.CreateTranslation(orbitX, orbitHeight + wingFlap, orbitZ);

            SetModelMatrix(orbit * bodyRotation * bankRotation * scale);

            Gl.BindVertexArray(teapot.Vao);
            Gl.DrawElements(GLEnum.Triangles, teapot.IndexArrayLength, GLEnum.UnsignedInt, null);
            Gl.BindVertexArray(0);

            var modelMatrixForTable = Matrix4X4.CreateScale(1f, 1f, 1f);
            SetModelMatrix(modelMatrixForTable);
            Gl.BindVertexArray(table.Vao);
            Gl.DrawElements(GLEnum.Triangles, table.IndexArrayLength, GLEnum.UnsignedInt, null);
            Gl.BindVertexArray(0);
        }

        private static unsafe void SetLightColor()
        {
            int location = Gl.GetUniformLocation(program, LightColorVariableName);

            if (location == -1)
            {
                throw new Exception($"{LightColorVariableName} uniform not found on shader.");
            }

            Gl.Uniform3(location, 1f, 1f, 1f);
            CheckError();
        }

        private static unsafe void SetLightPosition()
        {
            int location = Gl.GetUniformLocation(program, LightPositionVariableName);

            if (location == -1)
            {
                throw new Exception($"{LightPositionVariableName} uniform not found on shader.");
            }

            Gl.Uniform3(location, 5f, 1f, 0f);
            //Gl.Uniform3(location, cameraDescriptor.Position.X, cameraDescriptor.Position.Y, cameraDescriptor.Position.Z);
            CheckError();
        }

        private static unsafe void SetViewerPosition()
        {
            int location = Gl.GetUniformLocation(program, ViewPosVariableName);

            if (location == -1)
            {
                throw new Exception($"{ViewPosVariableName} uniform not found on shader.");
            }

            if (camera == 0)
            {
                Gl.Uniform3(location, camPan.Position.X, camPan.Position.Y, camPan.Position.Z);
            }
            else
            {
                Gl.Uniform3(location, cam3rd.Position.X, cam3rd.Position.Y, cam3rd.Position.Z);
            }

            CheckError();
        }

        private static unsafe void SetShininess()
        {
            int location = Gl.GetUniformLocation(program, ShininessVariableName);

            if (location == -1)
            {
                throw new Exception($"{ShininessVariableName} uniform not found on shader.");
            }

            Gl.Uniform1(location, Shininess);
            CheckError();
        }
        
        private static unsafe void SetModelMatrix(Matrix4X4<float> modelMatrix)
        {
            int location = Gl.GetUniformLocation(program, ModelMatrixVariableName);
            if (location == -1)
            {
                throw new Exception($"{ModelMatrixVariableName} uniform not found on shader.");
            }

            Gl.UniformMatrix4(location, 1, false, (float*)&modelMatrix);
            CheckError();

            var modelMatrixWithoutTranslation = new Matrix4X4<float>(modelMatrix.Row1, modelMatrix.Row2, modelMatrix.Row3, modelMatrix.Row4);
            modelMatrixWithoutTranslation.M41 = 0;
            modelMatrixWithoutTranslation.M42 = 0;
            modelMatrixWithoutTranslation.M43 = 0;
            modelMatrixWithoutTranslation.M44 = 1;

            Matrix4X4<float> modelInvers;
            Matrix4X4.Invert<float>(modelMatrixWithoutTranslation, out modelInvers);
            Matrix3X3<float> normalMatrix = new Matrix3X3<float>(Matrix4X4.Transpose(modelInvers));
            location = Gl.GetUniformLocation(program, NormalMatrixVariableName);
            if (location == -1)
            {
                throw new Exception($"{NormalMatrixVariableName} uniform not found on shader.");
            }
            Gl.UniformMatrix3(location, 1, false, (float*)&normalMatrix);
            CheckError();
        }

        private static unsafe void SetUpObjects()
        {

            float[] face1Color = [1f, 0f, 0f, 1.0f];
            float[] face2Color = [0.0f, 1.0f, 0.0f, 1.0f];
            float[] face3Color = [0.0f, 0.0f, 1.0f, 1.0f];
            float[] face4Color = [1.0f, 0.0f, 1.0f, 1.0f];
            float[] face5Color = [0.0f, 1.0f, 1.0f, 1.0f];
            float[] face6Color = [1.0f, 1.0f, 0.0f, 1.0f];

            teapot = ObjLoader.CreateFromObj(Gl, "Projekt.Resources.goose.obj", "Projekt.Resources.goose.png");

            float[] tableColor = [System.Drawing.Color.Ivory.R/256f,
                                  System.Drawing.Color.Ivory.G/256f,
                                  System.Drawing.Color.Ivory.B/256f,
                                  1f];
            table = GlCube.CreateSquare(Gl, tableColor);

            glCubeRotating = GlCube.CreateCubeWithFaceColors(Gl, face1Color, face2Color, face3Color, face4Color, face5Color, face6Color);

            skyBox = GlCube.CreateInteriorCube(Gl, "");

            for (int i = 0; i < 10; i++)
                glSphere[i] = GlObject.CreateSphere(5f, Gl);
        }
        
        private static void Window_Closing()
        {
            teapot.ReleaseGlObject();
            glCubeRotating.ReleaseGlObject();
        }

        private static unsafe void SetProjectionMatrix()
        {
            var projectionMatrix = Matrix4X4.CreatePerspectiveFieldOfView<float>((float)Math.PI / 4f, 1024f / 768f, 0.1f, 1000);
            int location = Gl.GetUniformLocation(program, ProjectionMatrixVariableName);

            if (location == -1)
            {
                throw new Exception($"{ViewMatrixVariableName} uniform not found on shader.");
            }

            Gl.UniformMatrix4(location, 1, false, (float*)&projectionMatrix);
            CheckError();
        }

        private static unsafe void SetViewMatrix()
        {
            Matrix4X4<float> viewMatrix;
            if (camera == 0)
            {
                viewMatrix = Matrix4X4.CreateLookAt(camPan.Position, camPan.Target, camPan.UpVector);
            }
            else
            {
                viewMatrix = Matrix4X4.CreateLookAt(cam3rd.Position, cam3rd.Target, cam3rd.UpVector);
            }
            int location = Gl.GetUniformLocation(program, ViewMatrixVariableName);

            if (location == -1)
            {
                throw new Exception($"{ViewMatrixVariableName} uniform not found on shader.");
            }

            Gl.UniformMatrix4(location, 1, false, (float*)&viewMatrix);
            CheckError();
        }

        public static void CheckError()
        {
            var error = (ErrorCode)Gl.GetError();
            if (error != ErrorCode.NoError)
                throw new Exception("GL.GetError() returned " + error.ToString());
        }
    }
}
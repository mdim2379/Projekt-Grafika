using Silk.NET.Maths;
using System;

namespace Projekt
{
    internal class CameraDescriptor2
    {
        private Vector3D<double> _position = new Vector3D<double>(0, 15, 0);
        private double _viewDistance = 10; // How far ahead the camera looks
        private double _viewAngle = Math.PI; // Rotation around Y axis (in radians)
        
        private const double DistanceScaleFactor = 1.1;
        private const double AngleChangeStepSize = Math.PI / 180 * 5; // 5 degrees in radians

        /// <summary>
        /// Gets or sets the camera's XZ position (ground position)
        /// </summary>
        public Vector2D<double> GroundPosition
        {
            get => new Vector2D<double>(_position.X, _position.Z);
            set
            {
                _position.X = value.X;
                _position.Z = value.Y;
            }
        }

        /// <summary>
        /// Gets or sets the camera's height (Y position)
        /// </summary>
        public double Height
        {
            get => _position.Y;
            set => _position.Y = Math.Max(0.1, value);
        }

        /// <summary>
        /// Gets or sets how far ahead the camera looks along its view direction
        /// </summary>
        public double ViewDistance
        {
            get => _viewDistance;
            set => _viewDistance = Math.Max(1, value);
        }

        /// <summary>
        /// Gets or sets the view angle (rotation around Y axis in radians)
        /// </summary>
        public double ViewAngle
        {
            get => _viewAngle;
            set => _viewAngle = value;
        }

        /// <summary>
        /// Gets the camera's full 3D position
        /// </summary>
        public Vector3D<float> Position => ConvertToFloat(_position);

        /// <summary>
        /// Gets the camera's up vector (always Y-up)
        /// </summary>
        public Vector3D<float> UpVector => Vector3D<float>.UnitY;

        /// <summary>
        /// Gets the point the camera is looking at (level with camera height)
        /// </summary>
        public Vector3D<float> Target
        {
            get
            {
                var lookAtX = _position.X + _viewDistance * Math.Sin(_viewAngle);
                var lookAtZ = _position.Z + _viewDistance * Math.Cos(_viewAngle);
                return new Vector3D<float>((float)lookAtX, (float)_position.Y, (float)lookAtZ);
            }
        }

        // Movement methods
        public void MoveForward(double distance)
        {
            _position.X += distance * Math.Sin(_viewAngle);
            _position.Z += distance * Math.Cos(_viewAngle);
        }

        public void MoveBackward(double distance)
        {
            _position.X -= distance * Math.Sin(_viewAngle);
            _position.Z -= distance * Math.Cos(_viewAngle);
        }

        public void StrafeLeft(double distance)
        {
            _position.X -= distance * Math.Cos(_viewAngle);
            _position.Z += distance * Math.Sin(_viewAngle);
        }

        public void StrafeRight(double distance)
        {
            _position.X += distance * Math.Cos(_viewAngle);
            _position.Z -= distance * Math.Sin(_viewAngle);
        }

        public void RotateLeft()
        {
            _viewAngle -= AngleChangeStepSize;
        }

        public void RotateRight()
        {
            _viewAngle += AngleChangeStepSize;
        }

        public void IncreaseHeight()
        {
            _position.Y *= DistanceScaleFactor;
        }

        public void DecreaseHeight()
        {
            _position.Y /= DistanceScaleFactor;
        }

        // Helper method to convert double vector to float
        private static Vector3D<float> ConvertToFloat(Vector3D<double> vec)
        {
            return new Vector3D<float>((float)vec.X, (float)vec.Y, (float)vec.Z);
        }
    }
}
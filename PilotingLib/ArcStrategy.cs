using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Move the ship along the surface of a sphere towards a certain point (or it's projection onto the sphere).
    /// </summary>
    public class ArcStrategy : BasePilotingStrategy
    {
        public BoundingSphereD Sphere;
        public ArcStrategy(BoundingSphereD sphere, Location goal, IMyCubeBlock reference, Base6Directions.Direction forward = Base6Directions.Direction.Forward, Base6Directions.Direction up = Base6Directions.Direction.Up) 
            : base(goal, reference, forward, up)
        {
            Sphere = sphere;
        }

        public override bool Update(BasePilot owner, ref Vector3D linearV, ref Vector3D angularV)
        {
            IMyCubeBlock reference = Reference ?? owner.Controller;
            MatrixD wm = reference.WorldMatrix;
            Vector3D forward = wm.GetDirectionVector(ReferenceForward);
            Vector3D up = wm.GetDirectionVector(ReferenceUp);
            Vector3D right = forward.Cross(up);
            double max_up_accel = owner.GetMaxAccelerationFor(up);
            Vector3D currentV = linearV;
            Goal.Update(owner.elapsedTime);
            //determine radial velocity component
            Vector3D my_projection = wm.Translation;
            ProjectOntoSphere(ref my_projection);
            owner.Log?.Invoke($"Me: {my_projection}\n");
            Vector3D radius = my_projection - Sphere.Center;
            radius.Normalize();
            Vector3D radial_direction = my_projection - wm.Translation;
            double radial_distance = radial_direction.Normalize();
            owner.Log?.Invoke($"Radial error: {radial_distance:F1}\n");
            //determine vector pointing towards the projection of the target
            Vector3D goal_projection = Goal.Position;
            ProjectOntoSphere(ref goal_projection);
            owner.Log?.Invoke($"Goal: {goal_projection}\n");
            //determine tangential velocity component
            Vector3D goal_direction = goal_projection - wm.Translation;
            double goal_distance = goal_direction.Normalize();
            owner.Log?.Invoke($"Distance: {goal_distance:F1}\n");
            Vector3D tangent_direction = Vector3D.ProjectOnPlane(ref goal_direction, ref radius);
            if (tangent_direction.Normalize() < OrientationEpsilon)
            {   //if our goal is directly opposite of us, just go in the direction we are facing
                tangent_direction = wm.GetDirectionVector(ReferenceForward);
                tangent_direction = Vector3D.ProjectOnPlane(ref tangent_direction, ref radius);
                if (tangent_direction.Normalize() < OrientationEpsilon)
                {   //if we are facing straight down or up, use "down" direction instead
                    tangent_direction = -wm.GetDirectionVector(ReferenceUp);
                    tangent_direction = Vector3D.ProjectOnPlane(ref tangent_direction, ref radius);
                    tangent_direction.Normalize();
                }
            }
            double goal_speed = MaxSpeedFor(owner.GetMaxAccelerationFor(-tangent_direction), goal_distance);
            linearV = tangent_direction * goal_speed;
            linearV += radial_direction * MaxSpeedFor(owner.GetMaxAccelerationFor(-radial_direction), radial_distance);
            double diff = owner.RotationAid.Rotate(owner.elapsedTime, tangent_direction, radius, forward, up, ref angularV);
            angularV.X += tangent_direction.Dot(forward) * currentV.Dot(tangent_direction) / (Sphere.Radius + radial_distance);
            angularV.Z += tangent_direction.Dot(right) * currentV.Dot(tangent_direction) / (Sphere.Radius + radial_distance);
            if (goal_distance < PositionEpsilon || diff < OrientationEpsilon)
                angularV = Vector3D.Zero;
            return goal_distance < PositionEpsilon;
        }

        void ProjectOntoSphere(ref Vector3D src)
        {
            src -= Sphere.Center;
            src.Normalize();
            src = src * Sphere.Radius + Sphere.Center;
        }
    }
}

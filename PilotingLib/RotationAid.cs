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
    class RotationAidSimple : IRotationAid
    {
        public double Rotate(TimeSpan interval, Vector3D desired_forward, Vector3D desired_up, Vector3D forward, Vector3D up, ref Vector3D vel)
        {
            double dot = forward.Dot(desired_forward);
            double rolldot;
            Vector3D left = up.Cross(forward);
            Vector3D diff = forward - desired_forward;
            if (Vector3D.IsZero(desired_up))
            {
                rolldot = 0;
                vel.Z = 0;
            }
            else
            {
                rolldot = desired_up.Dot(up);
                Vector3D rollvector = up - desired_up;
                vel.Z = left.Dot(rollvector);
                if (rolldot < 0)
                    vel.Z += Math.Sign(vel.Z);
                rolldot = 1 - rolldot;
            }
            vel.X = up.Dot(diff);
            vel.Y = left.Dot(diff);
            if (dot < 0)
                vel.Y += Math.Sign(vel.Y);
            dot = 1 - dot;
            return Math.Max(dot, rolldot);
        }
    }

    class RotationAidPID : IRotationAid
    {
        public PIDVector pid;
        public RotationAidPID(double p, double i, double d, double max = double.PositiveInfinity, double min = double.NegativeInfinity)
        {
            pid = new PIDVector(p, i, d, max, min);
        }
        public double Rotate(TimeSpan interval, Vector3D desired_forward, Vector3D desired_up, Vector3D forward, Vector3D up, ref Vector3D vel)
        {
            Vector3D left = up.Cross(forward);
            double rolldot = 0;
            double dot = forward.Dot(desired_forward);
            Vector3D diff = forward - desired_forward;
            Vector3D err = new Vector3D(up.Dot(diff), left.Dot(diff), 0);
            if (!Vector3D.IsZero(desired_up))
            {
                rolldot = desired_up.Dot(up);
                Vector3D rollvector = up - desired_up;
                err.Z = left.Dot(rollvector);
                if (rolldot < 0)
                    err.Z += Math.Sign(err.Z);
                rolldot = 1 - rolldot;
            }
            if (dot < 0)
                err.Y += Math.Sign(err.Y);
            vel = pid.Update(err, interval);
            return Math.Max(dot, rolldot);
        }
    }
}

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
    class PID
    {
        double P, I, D;
        double Min, Max;
        double last_error, diff_error, accum_error;
        public PID(double p, double i, double d, double max = double.PositiveInfinity, double min = double.NegativeInfinity)
        {
            P = p; I = i; D = d;
            Min = min; Max = max;
            accum_error = 0;
            last_error = 0;
        }
        public void Reset() { accum_error = 0; }
        public double Update(double error, TimeSpan sincelastupdate)
        {
            double dt = sincelastupdate.TotalSeconds;
            diff_error = (error - last_error) / dt;
            accum_error += error * dt;
            last_error = error;
            double value = P * error + I * accum_error + D * diff_error;
            return MathHelperD.Clamp(value, Min, Max);
        }
    }
    class PIDVector
    {
        PID X, Y, Z;
        public PIDVector(double p, double i, double d, double max = double.PositiveInfinity, double min = double.NegativeInfinity)
        {
            X = new PID(p, i, d, max, min);
            Y = new PID(p, i, d, max, min);
            Z = new PID(p, i, d, max, min);
        }
        public void Reset() { X.Reset(); Y.Reset(); Z.Reset(); }
        public Vector3D Update(Vector3D error, TimeSpan sincelastupdate)
        {
            return new Vector3D(
                X.Update(error.X, sincelastupdate),
                Y.Update(error.Y, sincelastupdate),
                Z.Update(error.Z, sincelastupdate));
        }
    }
}

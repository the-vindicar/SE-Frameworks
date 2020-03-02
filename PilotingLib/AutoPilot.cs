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
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Maintains a list of piloting tasks to complete, and performs them in sequence.
    /// Call Update() method often to ensure corrections can be made on time.
    /// </summary>
    public class AutoPilot : BasePilot
    {
        public Gravity UseGravity = Gravity.None;
        /// <summary>
        /// Create autopilot using specific set of blocks.
        /// </summary>
        /// <param name="controller">Ship controller to use.</param>
        /// <param name="thrusters">Thrusters to use.</param>
        /// <param name="gyros">Gyros to use.</param>
        public AutoPilot(IMyShipController controller, List<IMyThrust> thrusters, List<IMyGyro> gyros, IRotationAid aid = null)
            : base()
        {
            Thrusters = thrusters;
            Gyros = gyros;
            Controller = controller;
            ThrustValues = new double[Thrusters.Count];
            RotationAid = aid ?? new RotationAidSimple();
        }
        /// <summary>
        /// Create autopilot and let it find the required blocks.
        /// </summary>
        /// <param name="gts">GridTerminalSystem to use for block lookup.</param>
        /// <param name="basis">Only blocks on the exact same grid as this one will be used.</param>
        public AutoPilot(IMyGridTerminalSystem gts, IMyTerminalBlock basis, IRotationAid aid = null)
        {
            Controller = basis as IMyShipController;
            RotationAid = aid ?? new RotationAidSimple();
            if (Controller == null)
                gts.GetBlocksOfType<IMyShipController>(null, (b) => 
                {
                    if (b.IsFunctional && b.CubeGrid.EntityId == basis.CubeGrid.EntityId && b.CanControlShip)
                        Controller = b;
                    return false;
                });
            if (Controller == null)
                throw new Exception("No controller found on this grid!");
            Thrusters = new List<IMyThrust>();
            gts.GetBlocksOfType(Thrusters, (t) => t.IsFunctional && t.CubeGrid.EntityId == Controller.CubeGrid.EntityId);
            ThrustValues = new double[Thrusters.Count];
            Gyros = new List<IMyGyro>();
            gts.GetBlocksOfType(Gyros, (g) => g.IsFunctional && g.CubeGrid.EntityId == Controller.CubeGrid.EntityId);
        }

        List<IMyThrust> Thrusters;
        double[] ThrustValues;
        List<IMyGyro> Gyros;
        /// <summary>
        /// Determines how quickly the ship can accelerate in a given direction without changing its orientation.
        /// Useful for determining how quickly you can slow down to a full stop.
        /// </summary>
        /// <param name="direction">Direction vector in world-space.</param>
        /// <returns>Acceleration in m/s^2</returns>
        public override double GetMaxAccelerationFor(Vector3D direction)
        {
            double thrust = 0.0;
            direction.Normalize();
            foreach (IMyThrust t in Thrusters)
                if (t.IsWorking)
                {
                    double projection = t.WorldMatrix.Backward.Dot(direction);
                    if (projection > 0)
                        thrust += t.MaxEffectiveThrust * projection;
                }
            return thrust / Mass.TotalMass;
        }

        public override double GetThrustChangeDelay(Vector3D from, Vector3D to) { return 0; }

        /// <summary>
        /// Queries the current task for suggestions on linear and angular velocities, and sets up thrusters and gyros accordingly.
        /// </summary>
        /// <param name="elapsed">How many seconds elapsed since the last update.</param>
        /// <returns>True if all tasks have been completed.</returns>
        public override bool Update(TimeSpan elapsed)
        {
            if (CurrentTask == null) //if there is nothing to do, drift
                return true; //report that we are done
            else //there is something to do
            {
                elapsedTime = elapsed;
                Velocities = Controller.GetShipVelocities();
                Mass = Controller.CalculateShipMass();
                // current velocities
                Vector3D LinearV = Velocities.LinearVelocity;
                Vector3D TargetAngularV = Velocities.AngularVelocity;
                //task can always set respective velocities to 0 to avoid linear and/or angular motion
                Controller.DampenersOverride = CurrentTask.DampenersOverride;
                //query the task.
                bool done = CurrentTask.Update(this, ref LinearV, ref TargetAngularV);
                Log?.Invoke($"Task {CurrentTask.ToString()}\nDone: {done}\nLinear {LinearV.ToString()}\nAngular {TargetAngularV.ToString()}");
                //whether its done or not, apply changes to thrust/rotation
                SetRotationVelocity(TargetAngularV, CurrentTask.Reference, CurrentTask.ReferenceForward, CurrentTask.ReferenceUp);
                SetThrustVector(LinearV);
                return done;
            }
        }
        /// <summary>
        /// Disables overrides on used thrusters/gyros. 
        /// Useful if you need to quickly switch the ship to manual piloting.
        /// Does not change the state of inertia dampeners!
        /// </summary>
        public void DisableOverrides()
        {
            foreach (IMyThrust t in Thrusters)
                t.ThrustOverride = 0.0f;
            foreach (IMyGyro g in Gyros)
            {
                g.Yaw = 0.0f;
                g.Pitch = 0.0f;
                g.Roll = 0.0f;
                g.GyroOverride = false;
            }
        }
        /// <summary>
        /// Calculates and thrust required to reach target velocity and configures thrusters to do so.
        /// </summary>
        /// <param name="targetvel">Desired velocity in world-space.</param>
        void SetThrustVector(Vector3D targetvel)
        {
            //how much our current velocity differs from desired one?
            Vector3D deltaV = targetvel - Velocities.LinearVelocity; 
            if (!Vector3D.IsZero(deltaV, 1e-3)) // it does, significantly.
            {
                //if (!Vector3D.IsZero(angularvel) && !Vector3D.IsZero(deltaV))
                //{
                //    MatrixD rotmatrix = MatrixD.CreateFromYawPitchRoll(-angularvel.Y * elapsedTime / 2, -angularvel.X * elapsedTime / 2, -angularvel.Z * elapsedTime / 2);
                //    deltaV = Vector3D.Rotate(deltaV, rotmatrix);
                //}
                Vector3D grav;
                switch (UseGravity)
                {
                    case Gravity.Natural: grav = Controller.GetNaturalGravity(); break;
                    case Gravity.Artificial: grav = Controller.GetArtificialGravity(); break;
                    case Gravity.Total: grav = Controller.GetTotalGravity(); break;
                    default: grav = Vector3D.Zero; break;
                }
                double deltavabs = deltaV.Normalize();
                //Estimate thrust required to stop before the next update.
                //Prevents the ship from oscillating back and forth around the target.
                double maxaccel = deltavabs / elapsedTime.TotalSeconds; 
                MyShipMass mass = Controller.CalculateShipMass();
                double requiredthrust = mass.TotalMass * maxaccel;
                if (!Vector3D.IsZero(grav))
                {
                    deltaV = requiredthrust * deltaV - mass.TotalMass * grav;
                    requiredthrust = deltaV.Normalize();
                }
                //Calculate how much thrust each thruster can contribute to pushing the ship in required direction.
                //It depends on thruster overall power, its effectiveness (ion/atmo) and direction.
                double totalthrustvalue = 0.0;
                for (int i = Thrusters.Count - 1; i >= 0; i--)
                    if (Thrusters[i].IsWorking) //disabled/damaged/unfueled thrusters are ignored
                    {
                        double projection = Thrusters[i].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Backward).Dot(deltaV);
                        ThrustValues[i] = (projection > 0)
                            ? projection * Thrusters[i].MaxEffectiveThrust
                            : 0;
                        totalthrustvalue += ThrustValues[i];
                    }
                    else
                        ThrustValues[i] = 0.0;
                //Set thruster overrides accordingly.
                for (int i = Thrusters.Count - 1; i >= 0; i--)
                    Thrusters[i].ThrustOverride = (float)(requiredthrust * ThrustValues[i] / totalthrustvalue);
            }
            else //We have already achieved target velocity
                foreach (IMyThrust t in Thrusters)
                    t.ThrustOverride = 0.0f;
        }
        /// <summary>
        /// Configures gyros to apply required rotation.
        /// </summary>
        /// <param name="gridRotation">Angular velocity of the ship in grid-space.</param>
        /// <param name="reference">Reference block used (controller if null).</param>
        /// <param name="forward">Direction of the reference block to be considered forward.</param>
        /// <param name="up">Direction of the reference block to be considered up.</param>
        void SetRotationVelocity(Vector3D gridRotation, 
            IMyCubeBlock reference = null, 
            Base6Directions.Direction forward = Base6Directions.Direction.Forward,
            Base6Directions.Direction up = Base6Directions.Direction.Up)
        {
            if (Vector3D.IsZero(gridRotation)) //do we need to stop?
            {   //well, that's easy
                foreach (IMyGyro g in Gyros)
                {
                    g.Pitch = 0.0f;
                    g.Yaw = 0.0f;
                    g.Roll = 0.0f;
                    g.GyroOverride = true;
                }
            }
            else //no, we need to rotate
            {
                //a bit of a mess with X axis sign... no idea why, but it's necessary to flip it.
                //Vector3D fixedGridRotation = new Vector3D(-gridRotation.X, gridRotation.Y, gridRotation.Z);
                //Translate rotation vector from grid-space to world-space.
                IMyCubeBlock refblock = reference ?? Controller;
                MatrixD wm = refblock.WorldMatrix;
                if (forward != Base6Directions.Direction.Forward || up != Base6Directions.Direction.Up)
                {   //We change the trasformation matrix, so now forward/up directions are the ones we chose.
                    Vector3D new_forward = wm.GetDirectionVector(forward);
                    Vector3D new_up = wm.GetDirectionVector(up);
                    wm.Forward = new_forward;
                    wm.Up = new_up;
                    wm.Right = new_forward.Cross(new_up);
                }
                Vector3D worldRotation = Vector3D.TransformNormal(gridRotation, wm);
                foreach (IMyGyro g in Gyros)
                {
                    //transform rotation vector into this gyro's block-space
                    Vector3D gyroRotation = Vector3D.TransformNormal(worldRotation, MatrixD.Transpose(g.WorldMatrix));
                    g.Pitch = (float)gyroRotation.X;
                    g.Yaw = (float)gyroRotation.Y;
                    g.Roll = (float)gyroRotation.Z;
                    g.GyroOverride = true;
                }
            }
        }
    }
}

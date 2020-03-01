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
    /// Aims the ship in the direction of the target, and flies directly there.
    /// The ship will fly as fast as possible, but will decelerate to zero in the end.
    /// Flight will only start after correct orientation has been achieved.
    /// </summary>
    public class StrafingStrategy : BasePilotingStrategy
    {
        protected Location AimGoal;
        /// <summary>
        /// Constructs the strategy with given goal and (optional) reference block.
        /// </summary>
        /// <param name="move_target">Goal to pursue.</param>
        /// <param name="reference">Reference block to use, or null to use ship controller.</param>
        /// <param name="forward">Direction on the reference block that is considered "forward".</param>
        /// <param name="up">Direction on the reference block that is considered "up".</param>
        public StrafingStrategy(Location move_target, Location aim_target, IMyTerminalBlock reference,
            Base6Directions.Direction forward = Base6Directions.Direction.Forward,
            Base6Directions.Direction up = Base6Directions.Direction.Up) : base(move_target, reference, forward, up)
        {
            AimGoal = aim_target;
        }
        /// <summary>
        /// Queries the strategy on which linear and angular velocities the ship should have.
        /// </summary>
        /// <param name="owner">AutoPilot instance that queries the strategy.</param>
        /// <param name="linearV">Initial value - current linear velocity. Is set to desired linear velocity.</param>
        /// <param name="angularV">Initial value - current rotation. Is set to desired rotation.</param>
        /// <returns>True if goal is considered achieved.</returns>
        public override bool Update(BasePilot owner, ref Vector3D linearV, ref Vector3D angularV)
        {
            bool distanceok = false;
            bool orientationok = false;
            IMyCubeBlock reference = Reference ?? owner.Controller;
            MatrixD wm = reference.WorldMatrix;
            Goal.Update(owner.elapsedTime);
            AimGoal.Update(owner.elapsedTime);
            Vector3D direction = Goal.Position - wm.Translation;
            double distance = direction.Normalize();
            if (distance < Goal.Distance) //Are we too close to the goal?
            {   // yep! better back off.
                direction *= -1;
                distance = Goal.Distance - distance;
            }
            else //nah, we aren't there yet - just cut the distance we need to travel.
                distance -= Goal.Distance;
            //how quickly can we go, assuming we still need to stop at the end?
            double accel = owner.GetMaxAccelerationFor(-direction);
            //moving relative to the target
            linearV = direction * MaxSpeedFor(accel, distance) + Goal.Velocity;
            if (distance < PositionEpsilon)
            {   //we are close to our ideal position - attempting to rotate the ship is not a good idea.
                distanceok = true;
                linearV = Goal.Velocity;
            }
            Vector3D facingdirection = AimGoal.Position - wm.Translation; //we should face our goal, still.
            facingdirection.Normalize();
            //rotate the ship to face it
            double diff = owner.RotationAid.Rotate(owner.elapsedTime, facingdirection, Vector3D.Zero,
                wm.GetDirectionVector(ReferenceForward),
                wm.GetDirectionVector(ReferenceUp),
                ref angularV);
            if (diff < OrientationEpsilon)
            {   //we are good
                orientationok = true;
                angularV = Vector3D.Zero;
            }
            return distanceok && orientationok;
        }
    }
}

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
    /// Approaches the target slowly, until the selected block locks in.
    /// </summary>
    class DockingStrategy : BasePilotingStrategy
    {
        /// <summary>
        /// How close (in meters) we should be to the target to engage autolock.
        /// Non-positive values disable this behaviour.
        /// If docking block does not support autolock, it's ignored.
        /// </summary>
        public double AutoLockDistance = 0.0;
        /// <summary>
        /// Vector pointing directly away from the dock, world-space. 
        /// If non-zero, ship will orient itself to face opposite of this vector, and will approach the dock riding on it.
        /// If zero, ship will fly directly towards the dock, which may result in crooked or failed docking.
        /// </summary>
        public Vector3D Approach;
        public Vector3D Facing;
        BaseLock Lock;

        public DockingStrategy(
            Location goal,
            IMyCubeBlock clamp,
            Vector3D approach,
            Base6Directions.Direction? up = null,
            Vector3D ? facing = null)
            : base(goal, clamp, Base6Directions.Direction.Forward, Base6Directions.Direction.Up)
        {
            MaxLinearSpeed = 2.0;
            Approach = approach;
            if (!Vector3D.IsZero(Approach))
                Approach.Normalize();
            Facing = facing.HasValue ? facing.Value : Vector3D.Zero;
            Lock = CreateLockForBlock(Reference, AutoLockDistance);
            ReferenceForward = Lock.Forward;
            ReferenceUp = up.HasValue ? up.Value : Lock.Up;
            if (!Base6Directions.IsValidBlockOrientation(ReferenceForward, ReferenceUp))
                throw new ArgumentException("Invalid up direction!");
        }

        public static BaseLock CreateLockForBlock(IMyCubeBlock block, double AutoLockDistance = 0)
        {
            if (block == null)
                throw new ArgumentException("Block reference is null");
            if (block is IMyShipConnector) return new ConnectorLock(block as IMyShipConnector, AutoLockDistance);
            else if (block is IMyLandingGear) return new LandingLock(block as IMyLandingGear, AutoLockDistance);
            else if (block is IMyShipMergeBlock) return new MergeLock(block as IMyShipMergeBlock, AutoLockDistance);
            else if (block is IMyMotorStator) return new StatorLock(block as IMyMotorStator, AutoLockDistance);
            else if (block is IMyMotorRotor) return new RotorLock(block as IMyMotorRotor, AutoLockDistance);
            else
                throw new ArgumentException("Reference block is not a lockable one!");
        }

        public override bool Update(BasePilot owner, ref Vector3D linearV, ref Vector3D angularV)
        {
            IMyCubeBlock reference = Reference ?? owner.Controller;
            MatrixD wm = reference.WorldMatrix;
            Goal.Update(owner.elapsedTime);
            Vector3D currentGoalPos = Goal.Position;
            Vector3D direction = currentGoalPos - wm.Translation;
            double distance = direction.Normalize();
            double target_distance = distance;
            double diff;

            if (!Vector3D.IsZero(Approach))
            {
                Vector3D minusApproach = -Approach;
                diff = owner.RotationAid.Rotate(owner.elapsedTime,
                    Approach, Facing,
                    wm.GetDirectionVector(ReferenceForward),
                    wm.GetDirectionVector(ReferenceUp),
                    ref angularV);
                PlaneD alignment = new PlaneD(wm.Translation, minusApproach);
                Vector3D alignedPos = alignment.Intersection(ref currentGoalPos, ref minusApproach);
                Vector3D correction = alignedPos - wm.Translation;
                if (!Vector3D.IsZero(correction, PositionEpsilon)) //are we on approach vector?
                {   //no - let's move there
                    direction = correction;
                    distance = direction.Normalize();
                }
                //otherwise, we can keep our current direction
            }
            else
                diff = owner.RotationAid.Rotate(owner.elapsedTime,
                    direction, Facing,
                    wm.GetDirectionVector(ReferenceForward),
                    wm.GetDirectionVector(ReferenceUp),
                    ref angularV);
            //rotate the ship to face it
            if (diff > OrientationEpsilon) //we still need to rotate
                linearV = Goal.Velocity; //match velocities with our target, then.
            else //we are good
            {
                //how quickly can we go, assuming we still need to stop at the end?
                double accel = owner.GetMaxAccelerationFor(-direction);
                double braking_time = Math.Sqrt(2 * distance / accel);
                double acceptable_velocity = Math.Min(VelocityUsage * accel * braking_time, MaxLinearSpeed);
                //extra slowdown when close to the target
                acceptable_velocity = Math.Min(acceptable_velocity, distance);
                //moving relative to the target
                linearV = direction * acceptable_velocity + Goal.Velocity;
                angularV = Vector3D.Zero;
            }
            return Lock.TryLockIn(distance) || (target_distance < PositionEpsilon);
        }

        public void Release() { Lock.Unlock(); }
        public static bool GetApproach(IMyCubeBlock block, out Vector3D position, out Vector3D approach)
        {
            var lockobj = CreateLockForBlock(block, 0);
            return lockobj.TryGetCurrentHome(out position, out approach);
        }

        public abstract class BaseLock
        {
            public Base6Directions.Direction Forward;
            public Base6Directions.Direction Up;
            /// <summary>
            /// Returns true if the block is locked onto something.
            /// </summary>
            /// <returns></returns>
            public abstract bool IsLocked();
            /// <summary>
            /// Attempts to lock onto the target and returns true if attempt succeeds.
            /// </summary>
            /// <param name="distance"></param>
            /// <returns></returns>
            public abstract bool TryLockIn(double distance);
            /// <summary>
            /// Attempts to release the target.
            /// </summary>
            public abstract void Unlock();
            /// <summary>
            /// Checks if current block is locked onto a matching target block, and calculates position and approach vector for that target block.
            /// </summary>
            /// <param name="pos">Position of the target block.</param>
            /// <param name="approach">Normal pointing away from the target block's working surface.</param>
            /// <returns>True if current block is locked onto a mathcing target.</returns>
            public abstract bool TryGetCurrentHome(out Vector3D pos, out Vector3D approach);
        }
        abstract class BaseLock<T> : BaseLock
            where T: IMyCubeBlock
        {
            protected T Block;
            protected double Distance;
            public BaseLock(T block, double distance)
            {
                Block = block;
                Distance = distance;
                Forward = Base6Directions.Direction.Forward;
                Up = Base6Directions.Direction.Up;
            }
        }
        class RotorLock : BaseLock<IMyMotorRotor>
        {
            public RotorLock(IMyMotorRotor block, double distance) : base(block, distance)
            {
                Forward = Base6Directions.Direction.Down;
                Up = Base6Directions.Direction.Forward;
            }
            public override bool IsLocked() => Block.IsAttached;
            public override bool TryLockIn(double distance) => Block.IsAttached;
            public override void Unlock() => Block.Base?.Detach();
            public override bool TryGetCurrentHome(out Vector3D pos, out Vector3D approach)
            {
                if (Block.Base != null && Block.IsAttached)
                {
                    pos = Block.Base.WorldMatrix.Translation;
                    approach = Block.Base.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Up);
                    return true;
                }
                else
                {
                    pos = approach = Vector3D.Zero;
                    return false;
                }
            }
        }
        class StatorLock : BaseLock<IMyMotorStator>
        {
            public StatorLock(IMyMotorStator block, double distance) : base(block, distance)
            {
                Forward = Base6Directions.Direction.Up;
                Up = Base6Directions.Direction.Forward;
            }
            public override bool IsLocked() => Block.IsAttached;
            public override bool TryLockIn(double distance)
            {
                Block.Enabled = true;
                Block.RotorLock = true;
                Block.Attach();
                return Block.IsAttached;
            }
            public override void Unlock() => Block.Detach();
            public override bool TryGetCurrentHome(out Vector3D pos, out Vector3D approach)
            {
                if (Block.Top != null && Block.IsAttached)
                {
                    pos = Block.Top.WorldMatrix.Translation;
                    approach = Block.Top.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Down);
                    return true;
                }
                else
                {
                    pos = approach = Vector3D.Zero;
                    return false;
                }
            }
        }
        class MergeLock : BaseLock<IMyShipMergeBlock>
        {
            public MergeLock(IMyShipMergeBlock block, double distance) : base(block, distance) { Forward = Base6Directions.Direction.Right; }
            public override bool IsLocked() => Block.IsConnected;
            public override bool TryLockIn(double distance)
            {
                Block.Enabled = true;
                return Block.IsConnected;
            }
            public override void Unlock() => Block.Enabled = false;
            public override bool TryGetCurrentHome(out Vector3D pos, out Vector3D approach)
            {
                if (Block.IsConnected)
                {
                    approach = Block.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Left);
                    pos = Block.WorldMatrix.Translation - approach * Block.RealSize().X;
                    return true;
                }
                else
                {
                    pos = approach = Vector3D.Zero;
                    return false;
                }
            }
        }
        class LandingLock : BaseLock<IMyLandingGear>
        {
            public LandingLock(IMyLandingGear block, double distance) : base(block, distance)
            {
                Forward = Base6Directions.Direction.Down;
                Up = Base6Directions.Direction.Forward;
            }
            public override bool IsLocked() => Block.LockMode == LandingGearMode.Locked;
            public override bool TryLockIn(double distance)
            {
                Block.Enabled = true;
                Block.AutoLock = (Distance <= 0) || (distance < Distance);
                if (Block.LockMode == LandingGearMode.ReadyToLock)
                    Block.Lock();
                return Block.LockMode == LandingGearMode.Locked;
            }
            public override void Unlock()
            {
                Block.Unlock();
                Block.AutoLock = false;
            }
            public override bool TryGetCurrentHome(out Vector3D pos, out Vector3D approach)
            {
                if (Block.IsLocked)
                {
                    approach = Block.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Up);
                    pos = Block.WorldMatrix.Translation - approach * 0.5 * Block.RealSize().Y;
                    return true;
                }
                else
                {
                    pos = approach = Vector3D.Zero;
                    return false;
                }
            }
        }
        class ConnectorLock : BaseLock<IMyShipConnector>
        {
            public ConnectorLock(IMyShipConnector block, double distance) : base(block, distance) { }
            public override bool IsLocked() => Block.Status == MyShipConnectorStatus.Connected;
            public override bool TryLockIn(double distance)
            {
                Block.Enabled = true;
                if (Block.Status == MyShipConnectorStatus.Connectable)
                    Block.Connect();
                return Block.Status == MyShipConnectorStatus.Connected;
            }
            public override void Unlock() => Block.Disconnect();
            public override bool TryGetCurrentHome(out Vector3D pos, out Vector3D approach)
            {
                if (Block.OtherConnector != null && Block.Status == MyShipConnectorStatus.Connected)
                {
                    pos = Block.OtherConnector.WorldMatrix.Translation;
                    approach = Block.OtherConnector.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward);
                    return true;
                }
                else
                {
                    pos = approach = Vector3D.Zero;
                    return false;
                }
            }
        }
    }
}

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
    /// Describes a ratio in form of Used/Capacity. Examples: item storage, batteries, gas tanks.
    /// </summary>
    class Ratio
    {
        public int Count;
        public float Total;
        public float Current;
        public float Unused { get { return Total - Current; } set { Current = Total - value; } }
        public Ratio(float total = 0, float current = 0, int count = 0)
        {
            Count = count;
            Current = current;
            Total = total;
        }
        public void Reset() { Count = 0; Current = Total = 0; }
        public void Add(Ratio other)
        {
            Count += other.Count;
            Total += other.Total;
            Current += other.Current;
        }
        public void Subtract(Ratio other)
        {
            Count -= other.Count;
            Total -= other.Total;
            Current -= other.Current;
        }
        public float GetRatio(float ifnone = float.NaN) { return Total == 0 ? ifnone : Current / Total; }
        public string ToString(string format)
        {
            if (format[0] == 'P' || format[0] == 'p')
                return GetRatio().ToString(format);
            else
                return $"{Current.ToString(format)}/{Total.ToString(format)}";
        }
    }
    /// <summary>
    /// Describes a ratio in form of Used/Capacity. Also stores a reference to a linked object.
    /// </summary>
    class Ratio<T> : Ratio
    {
        /// <summary>Object, related to the data stored. For example, a block reference.</summary>
        public readonly T Target;
        public Ratio(T target, float total = 0, float current = 0, int count = 0) 
            : base(total, current, count) { Target = target; }
    }
}

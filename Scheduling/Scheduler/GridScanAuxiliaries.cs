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
    /// <summary>This structure is used during grid group and grid block enumeration.</summary>
    struct GridScanArgs<T>
    {
        /// <summary> Currently viewed item in the collection. </summary>
        public readonly T Item;
        /// <summary> If true, this item is the first during current grid scan. </summary>
        public readonly bool First;
        /// <summary> If true, this item is the last during current grid scan. No further calls to the handler will be made.</summary>
        public readonly bool Last;
        public GridScanArgs(T value, bool first, bool last) { Item = value; First = first; Last = last; }
    }
    /// <summary>Describes which blocks the job is allowed to use, i.e.: blocks on the same grid, blocks on the same construct, blocks in specific group, etc.</summary>
    struct GridPolicy
    {
        #region mdk preserve
        /// <summary>Describes the rules of accepting blocks.</summary>
        [Flags]
        public enum Types : byte
        {
            /// <summary>Accept any block found.</summary>
            Any = 0,
            /// <summary>Only accept blocks with specific name(can be multiple blocks).</summary>
            BlockName = 1,
            /// <summary>Only accept blocks with names containing specific substring.</summary>
            BlockTag = 2,
            /// <summary>Only accept blocks belonging to a group with specific name.</summary>
            GroupName = 4,
            /// <summary>Only accept blocks belonging to a group with name containing specific substring.</summary>
            GroupTag = 8,
            /// <summary>Only accept blocks on the same grid.</summary>
            SameGrid = 32,
            /// <summary>Only accept blocks on the same construct.</summary>
            SameConstruct = 64,
            /// <summary>Only accept blocs with the same owner.</summary>
            SameOwner = 128
        };
        #endregion
        /// <summary>General type of the policy.</summary>
        public readonly Types Type;
        /// <summary>Name is used by BlockTag, BlockName and BlockGroup policies.</summary>
        public readonly string Name;
        public GridPolicy(Types type, string name = null)
        {
            byte v = (byte)(type & (Types.BlockName | Types.BlockTag | Types.GroupName));
            if ((v & (v - 1)) != 0) //ensure no more than one of those flags is set
                throw new ArgumentException("Can't combine BlockName, BlockTag and GroupName");
            if (v != 0 && string.IsNullOrEmpty(name))
                throw new ArgumentException("You need to specify a name when using BlockName, BlockTag or GroupName");
            Type = type;
            Name = (v != 0) ? name : null;
        }
        public static bool TryParse(string data, out GridPolicy policy)
        {
            policy = new GridPolicy();
            Types type = Types.Any;
            Types part;
            int name_sep = data.IndexOf(':');
            string types = (name_sep < 0) ? data : data.Substring(0, name_sep);
            int lastidx = 0;
            while (lastidx < types.Length)
            {
                int idx = types.IndexOf("|", lastidx);
                if (idx < 0) idx = types.Length;
                if (!Enum.TryParse(types.Substring(lastidx, idx - lastidx), true, out part))
                    return false;
                type |= part;
                lastidx = idx + 2;
            }
            string name = (name_sep < 0) ? string.Empty : data.Substring(name_sep + 1);
            byte v = (byte)(type & (Types.BlockName | Types.BlockTag | Types.GroupName));
            //ensure no more than one of those flags is set
            //ensure if one of those flags is set, name is set too
            if (((v & (v - 1)) != 0) || (v != 0 && string.IsNullOrEmpty(name)))
                return false;
            policy = new GridPolicy(type, name);
            return true;
        }
        public static GridPolicy Parse(string data)
        {
            GridPolicy p;
            if (TryParse(data, out p))
                return p;
            else
                throw new ArgumentException($"'{data}' is not a valid policy descriptor.");
        }
        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (Types t in Enum.GetValues(typeof(Types)))
                if ((Type & t) != 0)
                {
                    if (builder.Length > 0) builder.Append('|');
                    builder.Append(t.ToString());
                }
            if (!string.IsNullOrEmpty(Name))
            {
                builder.Append(':');
                builder.Append(Name);
            }
            return builder.ToString();
        }
        public static implicit operator GridPolicy(Types type) { return new GridPolicy(type, null); }
        public static implicit operator GridPolicy(string value) { return GridPolicy.Parse(value); }
    }
}

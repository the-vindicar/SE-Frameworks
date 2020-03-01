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
    public static class InventoryHelper
    {
        /// <summary>
        /// Attempts to move certain item type from one inventory to another.
        /// </summary>
        /// <param name="from">Source inventory.</param>
        /// <param name="to">Destination inventory.</param>
        /// <param name="type">Item type to move.</param>
        /// <param name="amount">How much to move. If not specified, attempts to move every item of set type.</param>
        /// <returns>The amount actually moved.</returns>
        public static MyFixedPoint MoveItem(IMyInventory from, IMyInventory to, MyItemType type, MyFixedPoint? amount = null)
        {
            MyFixedPoint amountmoved = new MyFixedPoint();
            MyInventoryItem? item;
            bool moved;
            for (int i = from.ItemCount; i >= 0; i--)
            {
                item = from.GetItemAt(i);
                if (item.HasValue && item.Value.Type == type)
                {
                    MyFixedPoint amounttomove = amount.HasValue
                        ? MyFixedPoint.Min(amount.Value - amountmoved, item.Value.Amount)
                        : item.Value.Amount;
                    moved = to.TransferItemFrom(from, item.Value, amounttomove);
                    if (!moved) return amountmoved;
                    amountmoved += amounttomove;
                    if (amountmoved >= amount) return amountmoved;
                }
            }
            return amountmoved;
        }
        /// <summary>
        /// Attempts to equalize the amounts of certain item between connected inventories.
        /// </summary>
        /// <param name="invs">Collection of inventories to equalize.</param>
        /// <param name="type">Item to transfer.</param>
        public static void EqualizeItemCount(IList<IMyInventory> invs, MyItemType type)
        {
            var info = type.GetItemInfo();
            MyFixedPoint norm = new MyFixedPoint();
            for (int idx = invs.Count - 1; idx >= 0; idx--)
                norm += invs[idx].GetItemAmount(type);
            if (info.UsesFractions)
                norm = (MyFixedPoint)((double)norm / invs.Count);
            else
                norm = (MyFixedPoint)((int)norm / invs.Count);

            for (int i = invs.Count - 1; i > 0; i--) // not including 0
            {
                MyFixedPoint delta = invs[i].GetItemAmount(type) - norm;
                if (delta > 0)
                    for (int j = i-1; (delta > 0) && (j >= 0); j--)
                        delta -= MoveItem(invs[i], invs[j], type, delta);
                else if (delta < 0)
                    for (int j = i - 1; (delta < 0) && (j >= 0); j--)
                        delta += MoveItem(invs[j], invs[i], type, -delta);
            }
        }
    }
}

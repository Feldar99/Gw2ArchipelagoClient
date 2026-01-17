using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using Blish_HUD;
using SharpDX.MediaFoundation.DirectX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace Gw2Archipelago
{

    internal class ItemTracker
    {
        private static readonly Logger logger = Logger.GetLogger<Module>();
        public Dictionary<string, int> ItemCounts { private set; get; } = new Dictionary<string, int>();

        public delegate void UnlockEvent(string itemName, int unlockCount);

        public event UnlockEvent ItemUnlocked;

        public void Initialize(ArchipelagoSession apSession)
        {
            ItemCounts.Clear();
            // We want to maintain the callback list in the event of a disconnect
            if (apSession != null)
            {
                UnlockItems(apSession.Items);
                apSession.Items.ItemReceived += UnlockItems;
            }
        }

        public void UnlockItem(ItemInfo apItem)
        {
            var itemId = apItem.ItemId;
            var itemName = apItem.ItemName;
            logger.Debug("Unlock Item {}: {}, {}", apItem, itemId, itemName);

            if (ItemCounts.ContainsKey(itemName))
            {
                ItemCounts[itemName]++;
            }
            else
            {
                ItemCounts[itemName] = 1;
            }

            if (ItemUnlocked != null)
            {
                ItemUnlocked.Invoke(itemName, ItemCounts[itemName]);
            }

        }

        public void UnlockItems(IReceivedItemsHelper helper)
        {
            while (helper.PeekItem() != null)
            {
                ItemInfo apItem = helper.DequeueItem();
                UnlockItem(apItem);
            }
        }

        internal int GetUnlockedItemCount(string itemName)
        {
            int count;
            if (ItemCounts.TryGetValue(itemName, out count))
            {
                return count;
            }
            return 0;
        }
    }
}

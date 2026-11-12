using System;
using System.Collections.Generic;
using Looter.Items;

namespace Looter.Raid
{
    public enum RaidOutcome { Extracted, Failed }
    public enum RaidFailureReason { None, PlayerDied, TimeExpired }

    [Serializable]
    public sealed class RaidResult
    {
        private readonly List<ItemStack> extractedItems;
        private readonly List<ItemStack> securedItems;
        private readonly List<ItemStack> lostItems;

        public RaidOutcome Outcome { get; }
        public RaidFailureReason FailureReason { get; }
        public float ElapsedSeconds { get; }
        public IReadOnlyList<ItemStack> ExtractedItems => extractedItems;
        public IReadOnlyList<ItemStack> SecuredItems => securedItems;
        public IReadOnlyList<ItemStack> LostItems => lostItems;
        public int TotalItemCount => Count(extractedItems) + Count(securedItems) + Count(lostItems);
        public int TotalLootValue => Value(extractedItems) + Value(securedItems);
        public int TotalLostValue => Value(lostItems);

        public RaidResult(RaidOutcome outcome, RaidFailureReason failureReason, float elapsedSeconds,
            IReadOnlyList<ItemStack> extracted, IReadOnlyList<ItemStack> secured, IReadOnlyList<ItemStack> lost)
        {
            Outcome = outcome;
            FailureReason = failureReason;
            ElapsedSeconds = Math.Max(0f, elapsedSeconds);
            extractedItems = Copy(extracted);
            securedItems = Copy(secured);
            lostItems = Copy(lost);
        }

        private static List<ItemStack> Copy(IReadOnlyList<ItemStack> source)
        {
            List<ItemStack> copy = new List<ItemStack>();
            if (source == null) return copy;
            for (int i = 0; i < source.Count; i++)
            {
                ItemStack stack = source[i];
                if (stack != null && stack.Item != null)
                    copy.Add(stack.Copy());
            }
            return copy;
        }

        private static int Count(List<ItemStack> stacks)
        {
            int count = 0;
            foreach (ItemStack stack in stacks) if (stack != null) count += stack.Quantity;
            return count;
        }

        private static int Value(List<ItemStack> stacks)
        {
            int value = 0;
            foreach (ItemStack stack in stacks)
                if (stack != null && stack.Item != null) value += stack.Item.SellValue * stack.Quantity;
            return value;
        }
    }
}

using System.Collections.Generic;
using GameKit.Events;

namespace ZooTycoon.Core
{
    // 설계 54: 퀘스트 받기 · 상자 열기 보상을 지갑에 넣고 한 번 알린다(웜뱃 머리 위 「+N」)
    internal static class Rewards
    {
        public static void Grant(ZooState wallet, EventBus bus, double coins, IReadOnlyDictionary<string, int> items)
        {
            if (coins > 0d)
            {
                wallet.AddCoins(coins);
            }

            foreach (KeyValuePair<string, int> item in items)
            {
                wallet.AddItem(item.Key, item.Value);
            }

            bus.Publish(new Events.GoalRewarded(coins, items));
        }
    }
}

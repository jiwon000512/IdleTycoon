using System;
using System.Collections.Generic;
using System.Linq;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 43: 자리를 비운 동안 번 것(팝업이 보인다). 코인은 판 돈에서 낸 월급을 뺀 만큼, 재료는 늘어난(줄어든) 개수
    public sealed class OfflineReport
    {
        public double Seconds;
        public double Sales;
        public double Wages;
        public Dictionary<string, int> Items = new Dictionary<string, int>();

        public double Coins => Sales - Wages;
    }

    // 설계 43 D1: 오프라인 정산 = 공식 근사(한 번에 계산). 웜뱃이 하던 일은 멈추고 점원 자리만 돈다.
    // 점원 한 바퀴 = 일 + 딴짓 몫(확률 (100 − 일머리)/100 × 평균 딴짓 초). 빵집은 손님 · 계산 · 진열 공급 중 좁은 곳으로 팔고,
    // 재료가 모자라면 바닥난 때부터 농장이 거두는 만큼만 굽는다. 월급은 판 돈 안에서만(D3), 상한은 offlineMaxSeconds(D2).
    // ponytail: 빈 진열대 앞에서 기다리다 지쳐 나가는 손님 · 오프라인 거름 · 행상 방문 · 평가는 셈하지 않는다. 시뮬과 크게 벌어지면 OfflineTests 대조부터
    public static class Offline
    {
        public static OfflineReport Settle(ZooState state, Mall mall, TableSet tables, double seconds)
        {
            double t = Math.Max(0d, Math.Min(seconds, tables.Get<ConfigTable>(ConfigTable.k_OfflineMaxSeconds).Value));
            OfflineReport report = new OfflineReport { Seconds = t };

            if (t <= 0d)
            {
                return report;
            }

            // 축복은 떠날 때 남은 시간 비율만큼만 건다
            Blessing blessing = state.Blessing;
            double share = blessing.Active == null ? 0d : Math.Min(1d, blessing.Remaining / t);
            Func<string, double> scale = effect => state.Scale(effect) / (1d + blessing.Boost(effect)) * (1d + blessing.Boost(effect) * share);
            double trip = tables.Get<ConfigTable>(ConfigTable.k_OfflineTripSeconds).Value;
            ClerkConfigTable clerkConfig = tables.Get<ClerkConfigTable>(ClerkConfigTable.k_Main);
            double alarm = state.Relics.Value(RelicTable.k_Clock);
            // 수다는 딴짓 시간이 아니라 대화가 끝나면 끝난다(상대에게 걸어가는 몫 + 줄 수 × 줄 간격의 평균)
            double chat = clerkConfig.ChatDialogues.Count == 0 ? 0d : trip + clerkConfig.ChatDialogues.Select(id => tables.Get<DialogueTable>(id)).Average(d => d.Lines.Count * d.LineSeconds);
            Func<Clerk, double> idle = clerk => IdleShare(clerk, clerkConfig, alarm, chat);

            // 농장: 층마다 점원이 갈아 둔 밭을 돌며 거둔다(초당 거두는 밭 수)
            Dictionary<string, double> made = new Dictionary<string, double>();
            double bonuses = 0d;
            FarmConfigTable farmConfig = tables.Get<FarmConfigTable>(FarmConfigTable.k_Main);
            double basket = state.Relics.Value(RelicTable.k_Basket);

            foreach (FarmArea farm in mall.Farms)
            {
                Clerk clerk = farm.ClerkOf(farm.Barn);
                CropTable crop = clerk == null ? null : tables.GetAll<CropTable>().FirstOrDefault(c => c.Id == clerk.Product);

                if (crop == null)
                {
                    continue;
                }

                double harvests = farm.Plots.Count(p => p.IsTilled) / (crop.GrowSeconds / scale(BlessingTable.k_Grow) + trip + idle(clerk));
                Add(made, crop.Item, harvests * (crop.Yield + basket));
                bonuses += harvests * farmConfig.BonusChance * (1d + blessing.Boost(BlessingTable.k_Bonus) * share);
            }

            // 빵집: 손님(광장에서 가게로 오는 몫) · 계산 점원 · 오븐 점원(빵마다)
            BakeryArea bakery = mall.Bakery;
            PlazaConfigTable plazaConfig = tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
            double demand = scale(BlessingTable.k_Visitors) / plazaConfig.ArrivalSeconds * (1d - plazaConfig.BrowseChance);
            double checkout = 0d;

            foreach (CounterInteractable counter in bakery.Counters)
            {
                Clerk clerk = bakery.ClerkOf(counter);

                if (clerk != null)
                {
                    checkout += 1d / (bakery.Config.CheckoutSeconds / (counter.UpgradeValue(counter.UpgradeLevel) * scale(BlessingTable.k_Checkout)) + idle(clerk));
                }
            }

            Dictionary<BreadTable, double> supply = new Dictionary<BreadTable, double>();

            foreach (OvenInteractable oven in bakery.Ovens)
            {
                Clerk clerk = bakery.ClerkOf(oven);
                BreadTable bread = clerk == null ? null : bakery.UnlockedBreads.FirstOrDefault(b => b.Id == clerk.Product);

                if (bread != null)
                {
                    double bake = bread.BakeSeconds / (oven.UpgradeValue(oven.UpgradeLevel) * scale(BlessingTable.k_Bake));
                    supply[bread] = (supply.TryGetValue(bread, out double s) ? s : 0d) + bread.BatchSize / (bake + trip + idle(clerk));
                }
            }

            double cap = Math.Min(demand, checkout);
            // 빵마다 1초에 파는 개수: 손님은 빵을 비중으로 고르고 모자라면 다른 빵을 산다(2026-10-09 밸런스방: 굽는 몫대로 나누면 빵이 여럿일 때 30~40% 적게 셌다)
            Dictionary<BreadTable, double> sells = Split(cap, supply);

            // 재료: 쓰는 속도가 거두는 속도보다 빠른 재료는 바닥나는 때(firstOut)부터 거두는 만큼만(gain/use배) 굽는다 → 재료마다 구울 수 있는 초
            Dictionary<string, double> used = new Dictionary<string, double>();

            foreach (KeyValuePair<BreadTable, double> pair in sells)
            {
                foreach (IngredientData ingredient in pair.Key.Ingredients)
                {
                    Add(used, ingredient.Item, pair.Value / pair.Key.BatchSize * ingredient.Count);
                }
            }

            Dictionary<string, double> lasts = new Dictionary<string, double>();

            foreach (KeyValuePair<string, double> pair in used)
            {
                double gain = made.TryGetValue(pair.Key, out double g) ? g : 0d;

                if (pair.Value > gain)
                {
                    double firstOut = Math.Min(t, state.Count(pair.Key) / (pair.Value - gain));
                    lasts[pair.Key] = firstOut + gain / pair.Value * (t - firstOut);
                }
            }

            // 빵은 제 재료 중 가장 먼저 바닥나는 것까지만 굽는다(2026-10-08 밸런스방: 딸기가 0이면 케이크 오븐뿐 아니라 식빵 오븐까지 0이던 것)
            Dictionary<BreadTable, double> baking = sells.Keys.ToDictionary(bread => bread, bread => bread.Ingredients.Select(i => lasts.TryGetValue(i.Item, out double s) ? s : t).DefaultIfEmpty(t).Min());
            // 재료가 떨어지는 빵은 구울 수 있는 만큼(평균), 아닌 빵은 오븐만큼 안에서 다시 나눈다: 떨어진 빵을 찾던 손님이 다른 빵을 산다
            sells = Split(cap, supply.ToDictionary(pair => pair.Key, pair => baking[pair.Key] < t ? sells[pair.Key] * baking[pair.Key] / t : pair.Value));
            double madeCount = sells.Values.Sum() * t;
            int stock = bakery.Shelves.Sum(shelf => shelf.Stock);
            double stockValue = bakery.Shelves.Sum(shelf => shelf.Bread == null ? 0d : shelf.Bread.Price * shelf.Stock);
            double sold = Math.Min(cap * t, stock + madeCount);
            int fromStock = (int)Math.Min(stock, Math.Floor(sold));
            double madePrice = madeCount > 0d ? sells.Sum(pair => pair.Key.Price * pair.Value) * t / madeCount : 0d;
            double value = (stock > 0 ? fromStock * stockValue / stock : 0d) + (sold - fromStock) * madePrice;

            // 값 배수 · 주판(N번째마다 ×2) · 팁 기대값
            double abacus = state.Relics.Value(RelicTable.k_Abacus);
            StarConfigTable stars = state.Stars.Config(bakery.Id);
            double tip = stars == null ? 0d : state.Stars.TipChance(bakery.Id) * stars.TipRate;
            report.Sales = Math.Round(value * scale(BlessingTable.k_Price) * (abacus > 0d ? 1d + 1d / abacus : 1d) * (1d + tip));

            // 월급날: 지나간 날 수만큼, 판 돈 안에서만
            Payroll payroll = mall.Payroll;
            int paydays = t < payroll.UntilPayday ? 0 : 1 + (int)Math.Floor((t - payroll.UntilPayday) / payroll.Period);
            double wages = paydays * payroll.Clerks.Sum(clerk => clerk.Wage);
            report.Wages = Math.Min(wages, report.Sales);
            payroll.UntilPayday += paydays * payroll.Period - t;

            // 재료 · 반짝돌을 넣고 빼고, 판 재고를 진열대에서 뺀다
            foreach (string item in made.Keys.Union(used.Keys).ToList())
            {
                double gain = made.TryGetValue(item, out double g) ? g * t : 0d;
                double use = sells.Sum(pair => pair.Value * t / pair.Key.BatchSize * pair.Key.Ingredients.Where(i => i.Item == item).Sum(i => i.Count));
                AddItem(state, report, item, (int)Math.Max(-state.Count(item), Math.Round(gain - use)));
            }

            AddItem(state, report, farmConfig.BonusItem, (int)Math.Round(bonuses * t));
            state.AddCoins(report.Coins);
            Pick(bakery, fromStock);

            // 시간: 축복 · 행상 · 평가 쉬는 시간, 점원 없는 오븐 · 밭은 그대로 흐른다(점원 자리는 위에서 셈했다)
            blessing.Tick(t);
            RelicMerchant merchant = mall.Plaza.Merchant;
            double until = merchant.UntilNext - t;
            merchant.Restore(until > 0d ? until : plazaConfig.MerchantEvery - (-until % plazaConfig.MerchantEvery));
            bakery.Evaluation.Cooldown = Math.Max(0d, bakery.Evaluation.Cooldown - t);

            foreach (OvenInteractable oven in bakery.Ovens.Where(o => bakery.ClerkOf(o) == null))
            {
                oven.Tick(t);
            }

            foreach (FarmArea farm in mall.Farms.Where(f => f.ClerkOf(f.Barn) == null))
            {
                foreach (PlotInteractable plot in farm.Plots)
                {
                    plot.Tick(t);
                }
            }

            return report;
        }

        // 점원 한 바퀴에 붙는 평균 딴짓 초 = 확률 × 평균 시간(알람 시계가 있으면 그 상한). 같은 곳에 다른 점원이 있으면 chatChance만큼은 수다 시간.
        // ponytail: 남이 걸어온 수다로 멈추는 몫은 뺐다(2026-10-04 시뮬 대조에서 수 % 안)
        private static double IdleShare(Clerk clerk, ClerkConfigTable config, double alarm, double chat)
        {
            double chance = (100 - clerk.Skill) / 100d;
            double seconds = config.IdleSecondsMin + (config.IdleSecondsMax - config.IdleSecondsMin) * chance;
            seconds = alarm > 0d ? Math.Min(seconds, alarm) : seconds;
            double chatShare = clerk.Home.Clerks.Count > 1 ? config.ChatChance : 0d;
            return chance * ((1d - chatShare) * seconds + chatShare * chat);
        }

        // 손님 몫(1초에 total명)을 빵 비중대로 나누되 빵마다 caps(1초에 개수)를 넘지 않게. 넘친 손님은 남은 빵을 비중대로 고른다
        private static Dictionary<BreadTable, double> Split(double total, Dictionary<BreadTable, double> caps)
        {
            Dictionary<BreadTable, double> sells = new Dictionary<BreadTable, double>(caps);
            List<BreadTable> open = caps.Keys.ToList();

            while (open.Count > 0)
            {
                double weights = open.Sum(bread => (double)bread.Weight);
                double left = total - caps.Keys.Except(open).Sum(bread => sells[bread]);
                BreadTable full = open.FirstOrDefault(bread => caps[bread] <= left * bread.Weight / weights);

                if (full == null)
                {
                    open.ForEach(bread => sells[bread] = left * bread.Weight / weights);
                    break;
                }

                open.Remove(full);
            }

            return sells;
        }

        private static void Add(Dictionary<string, double> rates, string item, double rate)
        {
            rates[item] = (rates.TryGetValue(item, out double r) ? r : 0d) + rate;
        }

        private static void AddItem(ZooState state, OfflineReport report, string item, int count)
        {
            if (count == 0)
            {
                return;
            }

            if (count > 0)
            {
                state.AddItem(item, count);
            }
            else
            {
                state.TrySpendItem(item, -count);
            }

            report.Items[item] = (report.Items.TryGetValue(item, out int c) ? c : 0) + count;
        }

        private static void Pick(BakeryArea bakery, int count)
        {
            foreach (ShelfInteractable shelf in bakery.Shelves)
            {
                while (count > 0 && shelf.TryPick())
                {
                    count--;
                }
            }
        }
    }
}

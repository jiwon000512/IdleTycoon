using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 43: 게임 전체 ↔ 저장 모양. 불러오기는 새 게임을 평소처럼 만든 뒤 그 위에 덮는다(값을 치르거나 난수를 굴리는 길은 쓰지 않는다).
    // 표에서 사라진 id(빵 · 사물 · 유물 · 외형)는 건너뛴다. 웜뱃은 늘 빵집 첫 계산대 뒤에서 시작한다
    public static class GameSave
    {
        public const int k_Version = 1;

        public static SaveData Capture(ZooState state, Mall mall)
        {
            SaveData data = new SaveData
            {
                Coins = state.Coins,
                Items = new Dictionary<string, int>(state.Items),
                Blessing = state.Blessing.Active?.Id,
                BlessingLeft = state.Blessing.Remaining,
                BlessingCooldown = state.Blessing.Cooldown,
                Relics = new Dictionary<string, int>(state.Relics.AllStars),
                RelicSlots = state.Relics.Slots.Select(relic => relic?.Id).ToList(),
                RelicSales = state.Relics.Sales,
                Stars = new Dictionary<string, int>(state.Stars.Counts),
                UntilPayday = mall.Payroll.UntilPayday,
            };

            foreach (WombatArea area in mall.Areas)
            {
                data.Areas.Add(CaptureArea(area));
            }

            return data;
        }

        public static void Apply(SaveData data, ZooState state, Mall mall, TableSet tables)
        {
            state.Restore(data.Coins, data.Items);
            state.Blessing.Restore(Find<BlessingTable>(tables, data.Blessing), data.BlessingLeft, data.BlessingCooldown);
            state.Relics.Restore(data.Relics.Where(pair => Find<RelicTable>(tables, pair.Key) != null).ToDictionary(pair => pair.Key, pair => pair.Value),
                data.RelicSlots.Select(id => Find<RelicTable>(tables, id)).ToList(), data.RelicSales);
            state.Stars.Restore(data.Stars);
            mall.Payroll.UntilPayday = data.UntilPayday;

            foreach (AreaSave save in data.Areas)
            {
                WombatArea area = mall.Areas.FirstOrDefault(a => a.Id == save.Id);

                if (area != null)
                {
                    ApplyArea(save, area, tables);
                }
            }

            mall.Bakery.PlaceWombatHome();
        }

        private static AreaSave CaptureArea(WombatArea area)
        {
            AreaSave save = new AreaSave
            {
                Id = area.Id,
                Stored = new Dictionary<string, int>(area.Stored),
                Upgrades = new Dictionary<string, int>(area.UpgradeLevels),
            };
            List<IPlaced> things = area.SavedThings();

            foreach (IPlaced thing in things)
            {
                ThingSave t = new ThingSave { Kind = thing.Kind.Id, X = thing.Position.X, Y = thing.Position.Y };

                switch (thing)
                {
                    case ShelfInteractable shelf:
                        t.Bread = shelf.Bread?.Id;
                        t.Stock = shelf.Stock;
                        break;
                    case OvenInteractable oven:
                        t.Bread = oven.Bread?.Id;
                        t.LastBread = oven.LastBread?.Id;
                        t.Remaining = oven.Remaining;
                        t.Ready = oven.Ready;
                        break;
                    case TankInteractable tank when tank.Stock > 0:
                        t.Fish = tank.Restaurant.Dishes.Where(dish => tank.CountOf(dish) > 0).ToDictionary(dish => dish.Id, tank.CountOf);
                        break;
                }

                save.Things.Add(t);
            }

            foreach (Clerk clerk in area.Clerks)
            {
                save.Clerks.Add(new ClerkSave
                {
                    Thing = things.IndexOf((IPlaced)clerk.Thing),
                    Name = clerk.Name,
                    Skill = clerk.Skill,
                    Wage = clerk.Wage,
                    Look = clerk.Look.Id,
                    Product = clerk.Product,
                });
            }

            foreach (Candidate candidate in area.WaitingCandidates)
            {
                save.Candidates.Add(new CandidateSave { Name = candidate.Name, Skill = candidate.Skill, Look = candidate.Look.Id });
            }

            switch (area)
            {
                case BakeryArea bakery:
                    save.Cells = CaptureCells(bakery.Grid);
                    save.Unlocked = bakery.UnlockedBreads.Select(bread => bread.Id).ToList();
                    save.EvaluationCooldown = bakery.Evaluation.Cooldown;
                    break;
                case FarmArea farm:
                    save.Open = farm.IsOpen;
                    save.Cells = CaptureCells(farm.Grid);
                    save.Unlocked = farm.UnlockedCrops.Select(crop => crop.Id).ToList();

                    foreach (PlotInteractable plot in farm.Plots)
                    {
                        save.Plots.Add(new PlotSave
                        {
                            Col = plot.Cell.Col,
                            Row = plot.Cell.Row,
                            Tilled = plot.IsTilled,
                            Crop = plot.Crop?.Id,
                            Remaining = plot.Remaining,
                            Fertilized = plot.IsFertilized,
                        });
                    }

                    break;
                case PlazaArea plaza:
                    save.MerchantUntil = plaza.Merchant.UntilNext;
                    break;
                case RestaurantArea restaurant:
                    save.Open = restaurant.IsOpen;
                    save.Cells = CaptureCells(restaurant.Grid);
                    break;
                case FishingArea fishing:
                    save.Fishing = new FishingSave
                    {
                        Stage = fishing.Stage,
                        Summons = fishing.Summons,
                        Upgrades = new Dictionary<string, int>(fishing.Upgrades),
                        Dug = fishing.Dug,
                        Stakes = fishing.Stakes.Select(stake => new StakeSave { Open = stake.Open, Rod = stake.Rod?.Id }).ToList(),
                        Landed = fishing.Landed?.Id,
                        Log = new Dictionary<string, FishRecord>(fishing.Log),
                    };
                    break;
            }

            return save;
        }

        // 칸 · 열린 것 → 업그레이드 → 배치(진열대 용량이 업그레이드를 본다) → 사물 상태 → 점원 · 후보
        private static void ApplyArea(AreaSave save, WombatArea area, TableSet tables)
        {
            switch (area)
            {
                case BakeryArea bakery:
                    DigCells(bakery.Grid, save.Cells);

                    foreach (BreadTable bread in tables.GetAll<BreadTable>())
                    {
                        if (save.Unlocked.Contains(bread.Id) && !bakery.UnlockedBreads.Contains(bread))
                        {
                            bakery.UnlockBread(bread);
                        }
                    }

                    bakery.Evaluation.Cooldown = save.EvaluationCooldown;
                    break;
                case FarmArea farm:
                    if (save.Open && farm.Upper != null)
                    {
                        farm.RestoreOpen();
                    }

                    DigCells(farm.Grid, save.Cells);

                    // 연 작물은 층 공용 목록이라 1층에서만
                    foreach (CropTable crop in tables.GetAll<CropTable>())
                    {
                        if (farm.Upper == null && save.Unlocked.Contains(crop.Id) && !farm.UnlockedCrops.Contains(crop))
                        {
                            farm.UnlockCrop(crop);
                        }
                    }

                    foreach (PlotSave p in save.Plots)
                    {
                        PlotInteractable plot = farm.Plots.FirstOrDefault(each => each.Cell.Equals(new Cell(p.Col, p.Row)));
                        plot?.Restore(p.Tilled, Find<CropTable>(tables, p.Crop), p.Remaining, p.Fertilized);
                    }

                    break;
                case PlazaArea plaza:
                    plaza.Merchant.Restore(save.MerchantUntil);
                    break;
                case RestaurantArea restaurant:
                    if (save.Open)
                    {
                        restaurant.RestoreOpen();
                    }

                    DigCells(restaurant.Grid, save.Cells);
                    break;
                case FishingArea fishing when save.Fishing != null:
                    FishingSave f = save.Fishing;
                    fishing.Restore(f.Stage, f.Summons, f.Upgrades, f.Dug,
                        f.Stakes.Select(s => (s.Open, Find<RodTable>(tables, s.Rod), s.Grade)),
                        f.Log.Where(pair => Find<FishTable>(tables, pair.Key) != null).ToDictionary(pair => pair.Key, pair => pair.Value),
                        Find<BossTable>(tables, f.Landed));
                    break;
            }

            area.RestoreUpgrades(save.Upgrades);
            List<IPlaced> placed = area.ShopKinds.Count > 0
                ? area.RestorePlacement(save.Things.Select(t => new KeyValuePair<string, Vector2>(t.Kind, new Vector2(t.X, t.Y))), save.Stored)
                : new List<IPlaced>();

            for (int i = 0; i < placed.Count; i++)
            {
                ThingSave t = save.Things[i];

                switch (placed[i])
                {
                    case ShelfInteractable shelf when Find<BreadTable>(tables, t.Bread) is BreadTable bread:
                        shelf.Put(bread, t.Stock);
                        break;
                    case OvenInteractable oven:
                        oven.Restore(Find<BreadTable>(tables, t.Bread), Find<BreadTable>(tables, t.LastBread), t.Remaining, t.Ready);
                        break;
                    case TankInteractable tank when t.Fish != null:
                        foreach (KeyValuePair<string, int> pair in t.Fish)
                        {
                            if (Find<DishTable>(tables, pair.Key) is DishTable dish)
                            {
                                tank.Restore(dish, pair.Value);
                            }
                        }

                        break;
                }
            }

            foreach (ClerkSave c in save.Clerks)
            {
                Interactable thing = c.Thing >= 0 ? (c.Thing < placed.Count ? placed[c.Thing] as Interactable : null) : area.FixedClerkSlot;
                VisitorTable look = Find<VisitorTable>(tables, c.Look);

                if (thing != null && look != null && area.CanStaff(thing) && area.ClerkOf(thing) == null)
                {
                    area.RestoreClerk(new Candidate(c.Name, c.Skill, look), thing, c.Wage, c.Product);
                }
            }

            area.RestoreCandidates(save.Candidates
                .Where(c => Find<VisitorTable>(tables, c.Look) != null)
                .Select(c => new Candidate(c.Name, c.Skill, Find<VisitorTable>(tables, c.Look))));
        }

        private static List<int[]> CaptureCells(BurrowGrid grid)
        {
            return grid.Cells.Select(cell => new[] { cell.Col, cell.Row }).ToList();
        }

        private static void DigCells(BurrowGrid grid, List<int[]> cells)
        {
            foreach (int[] c in cells)
            {
                Cell cell = new Cell(c[0], c[1]);

                if (!grid.Contains(cell))
                {
                    grid.Dig(cell);
                }
            }
        }

        private static T Find<T>(TableSet tables, string id) where T : Table<string>
        {
            return id == null ? null : tables.GetAll<T>().FirstOrDefault(row => row.Id == id);
        }
    }
}

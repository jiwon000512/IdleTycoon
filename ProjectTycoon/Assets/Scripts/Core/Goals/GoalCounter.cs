using System;
using System.Linq;
using GameKit.Events;

namespace ZooTycoon.Core
{
    // 설계 54: 할 일 세기(퀘스트 · 오늘의 일이 같이 쓴다). 「무엇을 셀 수 있나」는 표의 kind, 여기는 「어떻게 세나」만.
    // 사건 종류는 사건이 나면 (종류, 대상, 개수)로 알리고(그 걸음이 시작된 뒤부터 센다), 상태 종류는 지금 게임 상태를 읽는다(이미 했으면 바로 다 됨)
    public static class GoalCounter
    {
        // 사건 종류
        public const string k_Bake = "bake";
        public const string k_Stock = "stock";
        public const string k_Sell = "sell";
        public const string k_Till = "till";
        public const string k_Plant = "plant";
        public const string k_Harvest = "harvest";
        public const string k_Dig = "dig";
        public const string k_Clean = "clean";
        public const string k_Wake = "wake";
        public const string k_Bless = "bless";
        public const string k_Draw = "draw";
        public const string k_Evaluate = "evaluate";
        public const string k_Catch = "catch";
        public const string k_Serve = "serve";
        // 상태 종류
        public const string k_Visit = "visit";
        public const string k_Place = "place";
        public const string k_Hire = "hire";
        public const string k_Unlock = "unlock";
        public const string k_Star = "star";
        public const string k_Open = "open";
        public const string k_Fill = "fill";
        public const string k_Seat = "seat";

        private static readonly string[] s_events = { k_Bake, k_Stock, k_Sell, k_Till, k_Plant, k_Harvest, k_Dig, k_Clean, k_Wake, k_Bless, k_Draw, k_Evaluate, k_Catch, k_Serve };
        private static readonly string[] s_states = { k_Visit, k_Place, k_Hire, k_Unlock, k_Star, k_Open, k_Fill, k_Seat };

        public static bool IsKnown(string kind)
        {
            return s_events.Contains(kind) || s_states.Contains(kind);
        }

        public static bool IsState(string kind)
        {
            return s_states.Contains(kind);
        }

        // 사건 종류를 세기 시작한다: 사건이 나면 counted(종류, 대상 id 또는 null, 개수)
        public static void Listen(EventBus bus, Action<string, string, int> counted)
        {
            bus.Subscribe<Events.BakeStarted>(e => counted(k_Bake, e.Bread.Id, 1));
            bus.Subscribe<Events.Stocked>(e => counted(k_Stock, e.Bread.Id, e.Count));
            bus.Subscribe<Events.BakeryVisitorPaid>(_ => counted(k_Sell, null, 1));
            bus.Subscribe<Events.Tilled>(_ => counted(k_Till, null, 1));
            bus.Subscribe<Events.Planted>(e => counted(k_Plant, e.Crop.Id, 1));
            bus.Subscribe<Events.Harvested>(e => counted(k_Harvest, e.Crop.Id, e.Count));
            bus.Subscribe<Events.Dug>(_ => counted(k_Dig, null, 1));
            bus.Subscribe<Events.PoopsCleaned>(e => counted(k_Clean, null, e.Count));
            bus.Subscribe<Events.ClerkWoke>(_ => counted(k_Wake, null, 1));
            bus.Subscribe<Events.BlessingChanged>(e =>
            {
                if (e.Prayed)
                {
                    counted(k_Bless, null, 1);
                }
            });
            bus.Subscribe<Events.RelicsChanged>(e =>
            {
                if (e.Change == RelicChange.Drawn)
                {
                    counted(k_Draw, null, 1);
                }
            });
            bus.Subscribe<Events.EvaluationStarted>(_ => counted(k_Evaluate, null, 1));
            bus.Subscribe<Events.FishLanded>(e => counted(k_Catch, e.Fish.Kind.Id, 1));
            bus.Subscribe<Events.DishServed>(_ => counted(k_Serve, null, 1));
        }

        // 상태 종류의 지금 값(사건 종류는 0)
        public static int Current(string kind, string param, Mall mall)
        {
            switch (kind)
            {
                case k_Visit:
                    return mall.Active.Id == param ? 1 : 0;
                case k_Place:
                    return mall.Areas.Sum(area => area.Things.Count(thing => thing.Table.Id == param));
                case k_Hire:
                    return mall.Areas.Where(area => param == null || area.Id == param).Sum(area => area.Clerks.Count);
                case k_Unlock:
                    return mall.Bakery.UnlockedBreads.Any(bread => bread.Id == param) || mall.Farm.UnlockedCrops.Any(crop => crop.Id == param) ? 1 : 0;
                case k_Star:
                    return mall.Bakery.Wallet.Stars.Count(param);
                case k_Open:
                    FishingStretchTable rock = Stretch(param, mall);
                    return (rock != null ? mall.Fishing.Opened.Contains(rock.Index) : IsAreaOpen(param, mall)) ? 1 : 0;
                case k_Fill:
                    FishingStretchTable dam = Stretch(param, mall);
                    return dam != null && mall.Fishing.Flooded.Contains(dam.Index) ? 1 : 0;
                case k_Seat:
                    return mall.Fishing.Seats.Count(seat => seat.Placed);
                default:
                    return 0;
            }
        }

        // 웜뱃이 그 곳에 들어갈 수 있나(농장 아래층 · 횟집은 열어야)
        public static bool IsAreaOpen(string area, Mall mall)
        {
            switch (mall.Areas.FirstOrDefault(a => a.Id == area))
            {
                case null:
                    return false;
                case FarmArea farm:
                    return farm.IsOpen;
                case RestaurantArea restaurant:
                    return restaurant.IsOpen;
                default:
                    return true;
            }
        }

        // 할 수 있는 일인가(오늘의 일로 내나 · 퀘스트 걸음을 다 된 것으로 넘기나): 유물을 다 모았으면 뽑기는 못 한다
        public static bool IsPossible(string kind, Mall mall)
        {
            return kind != k_Draw || !mall.Bakery.Wallet.Relics.Complete;
        }

        // 「가기」가 데려갈 사물로 알맞나: 갈기는 안 간 밭, 심기 · 작물 열기는 빈 밭(밭 시트), 거두기는 심은 밭,
        // 바위 · 댐은 그 걸음의 구간(param) 것(2026-10-09 리뷰: 「바위 뚫기」가 반대쪽 바위로 데려갔다)
        public static bool Suits(string kind, string param, Interactable thing)
        {
            switch (thing)
            {
                case RockInteractable rock:
                    return param == null || rock.Stretch.Id == param;
                case DamInteractable dam:
                    return param == null || dam.Stretch.Id == param;
                case PlotInteractable plot:
                    switch (kind)
                    {
                        case k_Till:
                            return !plot.IsTilled;
                        case k_Plant:
                        case k_Unlock:
                            return plot.IsTilled && plot.IsEmpty;
                        case k_Harvest:
                            return !plot.IsEmpty;
                        default:
                            return true;
                    }
                default:
                    return true;
            }
        }

        private static FishingStretchTable Stretch(string id, Mall mall)
        {
            return mall.Bakery.Tables.GetAll<FishingStretchTable>().FirstOrDefault(stretch => stretch.Id == id);
        }
    }
}

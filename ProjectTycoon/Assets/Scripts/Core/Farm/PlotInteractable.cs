using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 25 → 설계 27: 판 칸 하나의 밭. 흙 칸 → 갈기(버튼, 코인) → 빈 밭 → 심기(버튼) → 자라는 중 → 익음 → 거두기(밟으면) → 빈 밭.
    // 거리는 칸 사각형까지(안이면 0)라 칸에 서면 대상이 된다. 기준점(Position)은 칸 밑변 가운데(그림 발끝).
    // 설계 28: 심을 때 거름을 쓰면 거름 준 밭(빨리 자라고 덤이 잘 나온다), 거두면 꺼진다
    public sealed class PlotInteractable : Interactable
    {
        public const string k_Id = "plot";

        public FarmArea Farm { get; }
        public Cell Cell { get; }
        public Vector2 Position { get; }
        // 갈아 놓은 밭 칸인가(아니면 흙 칸: 심지 못한다)
        public bool IsTilled { get; private set; }
        // 심은 작물. 빈 밭이면 null
        public CropTable Crop { get; private set; }
        public double Remaining { get; private set; }
        public bool IsFertilized { get; private set; }
        public bool IsEmpty => Crop == null;
        public bool IsRipe => !IsEmpty && Remaining <= 0d;
        // 0~1
        public double Progress => IsEmpty ? 0d : 1d - Remaining / m_growSeconds;
        // 자라는 그림 번호: 자라는 동안 0 ~ stages − 2를 고르게, 익으면 마지막. 빈 밭은 −1
        public int Stage => IsEmpty ? -1 : IsRipe ? Crop.Stages - 1 : Math.Min(Crop.Stages - 2, (int)(Progress * (Crop.Stages - 1)));
        // 밟고 서는 사물: 곁의 흙 칸(파기)·통로에 대상을 양보한다(QA A)
        public override int TargetPriority => 1;

        // 이번에 심은 작물이 익는 데 걸리는 시간(거름 배율이 든 값)
        private double m_growSeconds;

        public PlotInteractable(InteractableTable table, Cell cell, FarmArea farm, bool tilled) : base(table, farm)
        {
            Farm = farm;
            Cell = cell;
            Position = farm.Layout.Cells.CellBase(cell);
            IsTilled = tilled;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Farm.Layout.Cells.DistanceToCell(Cell, p);
        }

        // 발이 밭 몸통(칸 둘레 여백 FarmConfigTable fieldInset 안쪽)에 있나. 거두기는 여기서만(여백은 걷는 길, 2026-09-30 안 A)
        public bool IsUnderfoot(Vector2 p)
        {
            return Farm.Layout.Cells.CellRect(Cell).Contains(p, -(float)Farm.Config.FieldInset);
        }

        // 갈기: 흙 칸이 밭 칸이 된다(값은 갈기 행동이 치른다)
        public void Till()
        {
            IsTilled = true;
            OnChanged();
            Area.Bus.Publish(new Events.Tilled(this));
        }

        // 설계 43: 저장한 갈림 · 작물(없으면 빈 밭) · 남은 초 · 거름
        internal void Restore(bool tilled, CropTable crop, double remaining, bool fertilized)
        {
            IsTilled = tilled;

            if (crop != null)
            {
                Plant(crop, fertilized);
                Remaining = Math.Min(remaining, m_growSeconds);
            }
        }

        public void Plant(CropTable crop, bool fertilized)
        {
            Crop = crop;
            IsFertilized = fertilized;
            m_growSeconds = crop.GrowSeconds * (fertilized ? Farm.Config.ManureGrowScale : 1d);
            Remaining = m_growSeconds;
            OnChanged();
        }

        // 익은 작물을 거둬 창고에 넣는다(빈 밭이 된다). 덤은 bonusChance(× 석상 반짝 축복, 거름 준 밭은 × manureBonusScale)로 하나.
        // 설계 31 이삭 바구니를 끼웠으면 그 확률로 작물 +1
        public void Harvest(ZooState wallet, IRandom random)
        {
            CropTable crop = Crop;
            FarmConfigTable config = Farm.Config;
            double chance = config.BonusChance * (1d + wallet.Blessing.Boost(BlessingTable.k_Bonus));
            bool bonus = random.NextDouble() < chance * (IsFertilized ? config.ManureBonusScale : 1d);
            double basket = wallet.Relics.Value(RelicTable.k_Basket);
            int count = crop.Yield + (basket > 0d && random.NextDouble() < basket ? 1 : 0);
            Crop = null;
            IsFertilized = false;
            wallet.AddItem(crop.Item, count);
            OnChanged();
            Area.Bus.Publish(new Events.Harvested(this, crop, count));

            if (bonus)
            {
                wallet.AddItem(config.BonusItem, 1);
                Area.Bus.Publish(new Events.BonusFound(this, config.BonusItem, 1));
            }
        }

        // 그림 번호가 바뀔 때만 알린다
        public override void Tick(double dt)
        {
            if (IsEmpty || IsRipe)
            {
                return;
            }

            int before = Stage;
            // 설계 30 · 31: 새싹 축복 · 물뿌리개
            Remaining = Math.Max(0d, Remaining - dt * Farm.Wombat.Worker.Wallet.Scale(BlessingTable.k_Grow));

            if (Stage != before)
            {
                OnChanged();
            }
        }
    }
}

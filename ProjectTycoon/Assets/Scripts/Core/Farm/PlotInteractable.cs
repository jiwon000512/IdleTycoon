using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 25 → 설계 27: 판 칸 하나의 밭. 흙 칸 → 갈기(버튼, 코인) → 빈 밭 → 심기(버튼) → 자라는 중 → 익음 → 거두기(밟으면) → 빈 밭.
    // 거리는 칸 사각형까지(안이면 0)라 칸에 서면 대상이 된다. 기준점(Position)은 칸 밑변 가운데(그림 발끝)
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
        public bool IsEmpty => Crop == null;
        public bool IsRipe => !IsEmpty && Remaining <= 0d;
        // 0~1
        public double Progress => IsEmpty ? 0d : 1d - Remaining / Crop.GrowSeconds;
        // 자라는 그림 번호: 자라는 동안 0 ~ stages − 2를 고르게, 익으면 마지막. 빈 밭은 −1
        public int Stage => IsEmpty ? -1 : IsRipe ? Crop.Stages - 1 : Math.Min(Crop.Stages - 2, (int)(Progress * (Crop.Stages - 1)));

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

        // 갈기: 흙 칸이 밭 칸이 된다(값은 갈기 행동이 치른다)
        public void Till()
        {
            IsTilled = true;
            OnChanged();
            Area.Bus.Publish(new Events.Tilled(this));
        }

        public void Plant(CropTable crop)
        {
            Crop = crop;
            Remaining = crop.GrowSeconds;
            OnChanged();
        }

        // 익은 작물을 거둬 창고에 넣는다(빈 밭이 된다)
        public void Harvest(ZooState wallet)
        {
            CropTable crop = Crop;
            Crop = null;
            wallet.AddItem(crop.Item, crop.Yield);
            OnChanged();
            Area.Bus.Publish(new Events.Harvested(this, crop.Item, crop.Yield));
        }

        // 그림 번호가 바뀔 때만 알린다
        public override void Tick(double dt)
        {
            if (IsEmpty || IsRipe)
            {
                return;
            }

            int before = Stage;
            Remaining = Math.Max(0d, Remaining - dt);

            if (Stage != before)
            {
                OnChanged();
            }
        }
    }
}

using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 25: 밭 한 칸(자유 배치, 밑변 가운데 좌표). 빈 밭 → 심기(버튼) → 자라는 중 → 익음 → 거두기(지나가면) → 빈 밭.
    // 거리는 밭 바닥 사각형까지라 긴 밭의 어느 쪽에 서도 닿는다
    public sealed class PlotInteractable : Interactable, IPlaced
    {
        public const string k_Id = "plot";

        public FarmArea Farm { get; }
        public Vector2 Position { get; private set; }
        // 심은 작물. 빈 밭이면 null
        public CropTable Crop { get; private set; }
        public double Remaining { get; private set; }
        public bool IsEmpty => Crop == null;
        public bool IsRipe => !IsEmpty && Remaining <= 0d;
        // 0~1
        public double Progress => IsEmpty ? 0d : 1d - Remaining / Crop.GrowSeconds;
        // 자라는 그림 번호: 자라는 동안 0 ~ stages − 2를 고르게, 익으면 마지막. 빈 밭은 −1
        public int Stage => IsEmpty ? -1 : IsRipe ? Crop.Stages - 1 : Math.Min(Crop.Stages - 2, (int)(Progress * (Crop.Stages - 1)));
        public IPlacedKind Kind => Table;

        public PlotInteractable(InteractableTable table, Vector2 position, FarmArea farm) : base(table, farm)
        {
            Farm = farm;
            Position = position;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Placement.Rect(this).DistanceTo(p);
        }

        public void MoveTo(Vector2 position)
        {
            Position = position;
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

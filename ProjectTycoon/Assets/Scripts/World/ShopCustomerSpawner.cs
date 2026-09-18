using System.Collections.Generic;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: ShopSim 손님 이벤트 → 손님 개체 생성·이동·퇴장. 걷는 시간은 Core가 센 값을 그대로 쓴다
    // 입구 → 옆으로 → 빵 칸 앞 / 빵 칸 → 가운데 / 줄 칸 / 계산대 → 오른쪽 통로 → 입구
    public sealed class ShopCustomerSpawner : MonoBehaviour
    {
        [SerializeField] private ShopCustomer m_prefab;

        private readonly Dictionary<int, ShopCustomer> m_units = new Dictionary<int, ShopCustomer>();
        private ShopSim m_shop;
        private ShopView m_view;
        private GameTables m_tables;
        private FrameCache m_frames;

        public void Initialize(ShopSim shop, ShopView view, GameTables tables, FrameCache frames)
        {
            m_shop = shop;
            m_view = view;
            m_tables = tables;
            m_frames = frames;

            m_shop.CustomerArrived += Shop_CustomerArrived;
            m_shop.CustomerPicked += Shop_CustomerPicked;
            m_shop.QueueChanged += Shop_QueueChanged;
            m_shop.CustomerPaid += Shop_CustomerPaid;
            m_shop.CustomerGaveUp += Shop_CustomerGaveUp;
            m_shop.LayoutChanged += Shop_QueueChanged;
        }

        private void OnDestroy()
        {
            if (m_shop != null)
            {
                m_shop.CustomerArrived -= Shop_CustomerArrived;
                m_shop.CustomerPicked -= Shop_CustomerPicked;
                m_shop.QueueChanged -= Shop_QueueChanged;
                m_shop.CustomerPaid -= Shop_CustomerPaid;
                m_shop.CustomerGaveUp -= Shop_CustomerGaveUp;
                m_shop.LayoutChanged -= Shop_QueueChanged;
            }
        }

        // 규칙 예외: 손님 외형 선택은 연출 난수(UnityEngine.Random, 프로그래밍-규약 5장)
        private void Shop_CustomerArrived(Customer customer)
        {
            IReadOnlyList<VisitorRecord> looks = m_tables.Visitors;
            VisitorRecord look = looks[Random.Range(0, looks.Count)];
            ShopCustomer unit = Instantiate(m_prefab, transform);
            unit.Initialize(look, m_frames, m_frames.Get(customer.Bread.Sprite)[0], m_view.Door);
            m_units[customer.Id] = unit;

            Vector2 spot = m_view.ShelfSpot(customer.Slot);
            unit.Walk(new[] { new Vector2(spot.x, m_view.Door.y), spot }, (float)customer.Timer);
        }

        private void Shop_CustomerPicked(Customer customer)
        {
            ShopCustomer unit = m_units[customer.Id];
            unit.HideBubble();
            unit.Walk(new[] { new Vector2(m_view.transform.position.x, unit.Logical.y) }, (float)m_tables.Config.Shop.ToQueueSeconds);
        }

        private void Shop_QueueChanged()
        {
            for (int i = 0; i < m_shop.Queue.Count; i++)
            {
                ShopCustomer unit = m_units[m_shop.Queue[i].Id];
                Vector2 spot = m_view.QueueSpot(i);

                if (unit.Destination != spot)
                {
                    unit.Walk(new[] { spot }, 0f);
                }
            }
        }

        private void Shop_CustomerPaid(Customer customer, double coins)
        {
            ShopCustomer unit = Take(customer);
            unit.PopCoin();
            float lane = m_view.ExitLaneX;
            Leave(unit, new[] { new Vector2(lane, unit.Logical.y), new Vector2(lane, m_view.Door.y), m_view.Door });
        }

        private void Shop_CustomerGaveUp(Customer customer)
        {
            ShopCustomer unit = Take(customer);
            unit.ShowAngry();
            Leave(unit, new[] { new Vector2(unit.Logical.x, m_view.Door.y), m_view.Door });
        }

        private ShopCustomer Take(Customer customer)
        {
            ShopCustomer unit = m_units[customer.Id];
            m_units.Remove(customer.Id);
            return unit;
        }

        private static void Leave(ShopCustomer unit, Vector2[] path)
        {
            unit.Walk(path, 0f, () => Destroy(unit.gameObject));
        }
    }
}

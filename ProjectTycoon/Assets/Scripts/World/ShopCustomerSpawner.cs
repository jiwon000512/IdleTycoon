using System.Collections.Generic;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: ShopSim 손님 이벤트 → 손님 개체 생성·이동·퇴장. 걷는 시간은 Core가 센 값을 그대로 쓴다
    // 굴 격자 설계 v0.5: 입구 → (칸 경로) → 빵 자리 / 빵 자리 → 가운데(같은 줄로 곧장, 안 되면 칸 경로) / 줄 칸 / 계산대 → 오른쪽 통로 → 입구
    public sealed class ShopCustomerSpawner : MonoBehaviour
    {
        private const string k_CoinKey = "coin_popup";
        // 연출 1차: 같은 진열대 대기자는 오른쪽으로 이만큼씩 옆에(최대 k_WaitMax명), 코인 팝업은 줄 오른쪽 옆에
        private const float k_WaitOffset = 0.45f;
        private const int k_WaitMax = 2;
        private const float k_CoinSideOffset = 0.7f;

        [SerializeField] private ShopCustomer m_prefab;

        private readonly Dictionary<int, ShopCustomer> m_units = new Dictionary<int, ShopCustomer>();
        // 손님별 대기 자리 번호(같은 진열대에서 가장 작은 빈 번호). 앞 사람이 떠나도 뒤 사람은 그대로
        private readonly Dictionary<int, int> m_waitIndex = new Dictionary<int, int>();
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
            }
        }

        // 규칙 예외: 손님 외형 선택은 연출 난수(UnityEngine.Random, 프로그래밍-규약 5장)
        private void Shop_CustomerArrived(Customer customer)
        {
            IReadOnlyList<VisitorRecord> looks = m_tables.Visitors;
            VisitorRecord look = looks[Random.Range(0, looks.Count)];
            ShopCustomer unit = Instantiate(m_prefab, transform);
            unit.Initialize(customer, (float)m_tables.Config.Shop.PatienceWarnSeconds, look, m_frames, m_frames.Get(customer.Bread.Sprite)[0], m_view.Door);
            m_units[customer.Id] = unit;

            Vector2 spot = m_view.ShelfSpot(customer.Cell);
            bool[] used = new bool[k_WaitMax + 1];

            foreach (Customer other in m_shop.Customers)
            {
                if (other != customer && other.Cell.Equals(customer.Cell) && (other.Phase == CustomerPhase.ToShelf || other.Phase == CustomerPhase.AtShelf)
                    && m_waitIndex.TryGetValue(other.Id, out int index) && index < used.Length)
                {
                    used[index] = true;
                }
            }

            int wait = 0;

            while (wait < k_WaitMax && used[wait])
            {
                wait++;
            }

            m_waitIndex[customer.Id] = wait;
            spot.x += k_WaitOffset * wait;
            // 입구 줄을 옆으로 걸어 첫 칸 열에 맞춘 뒤 경로를 따른다
            List<Vector2> route = m_view.Route(m_shop.Grid.EntranceNear(customer.Cell), customer.Cell, spot);
            route.Insert(0, new Vector2(route[0].x, m_view.Door.y));
            unit.Walk(route, (float)customer.Timer);
        }

        private void Shop_CustomerPicked(Customer customer)
        {
            ShopCustomer unit = m_units[customer.Id];
            unit.HideBubble();
            Vector2 center = new Vector2(m_view.transform.position.x, unit.Logical.y);
            List<Vector2> route;

            if (m_view.StraightToCenter(customer.Cell))
            {
                route = new List<Vector2> { center };
            }
            else
            {
                route = m_view.Route(customer.Cell, m_shop.Grid.CounterNear(customer.Cell), center);
                route[route.Count - 1] = new Vector2(center.x, route[route.Count - 2].y);
            }

            unit.Walk(route, (float)customer.Timer);
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
            unit.PopCoin(m_tables.Strings.Format(k_CoinKey, coins.ToString("0", System.Globalization.CultureInfo.InvariantCulture)), k_CoinSideOffset);
            float lane = m_view.ExitLaneX;
            Leave(unit, new[] { new Vector2(lane, unit.Logical.y), new Vector2(lane, m_view.Door.y), m_view.Door });
        }

        // 포기: 온 길을 되짚어 입구로
        private void Shop_CustomerGaveUp(Customer customer)
        {
            ShopCustomer unit = Take(customer);
            unit.ShowAngry();
            Leave(unit, m_view.Route(customer.Cell, m_shop.Grid.EntranceNear(customer.Cell), m_view.Door));
        }

        private ShopCustomer Take(Customer customer)
        {
            ShopCustomer unit = m_units[customer.Id];
            m_units.Remove(customer.Id);
            m_waitIndex.Remove(customer.Id);
            return unit;
        }

        private static void Leave(ShopCustomer unit, IReadOnlyList<Vector2> path)
        {
            unit.Walk(path, 0f, () => Destroy(unit.gameObject));
        }
    }
}

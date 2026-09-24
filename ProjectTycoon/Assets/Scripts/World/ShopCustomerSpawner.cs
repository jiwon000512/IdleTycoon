using System.Collections.Generic;
using UnityEngine;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 08 v0.5 · 손님 동선 설계 v0.2: BakeryArea 손님 사건 → 손님 개체 생성·연출·삭제. 걷기와 판단은 Core가 하고 개체는 그 위치를 그린다
    public sealed class ShopCustomerSpawner : MonoBehaviour
    {
        private const string k_CoinKey = "coin_popup";
        private const string k_HappyKey = "emote_happy";
        // 진열대 그림에서 빵이 놓인 높이(유닛, 밑변 기준)
        private const float k_ShelfBreadHeight = 0.62f;

        [SerializeField] private ShopCustomer m_prefab;

        private readonly Dictionary<Customer, ShopCustomer> m_units = new Dictionary<Customer, ShopCustomer>();
        private BakeryArea m_shop;
        private ShopView m_view;
        private TableSet m_tables;
        private FrameCache m_frames;

        public void Initialize(BakeryArea shop, ShopView view, TableSet tables, FrameCache frames)
        {
            m_shop = shop;
            m_view = view;
            m_tables = tables;
            m_frames = frames;

            m_shop.CustomerArrived += Shop_CustomerArrived;
            m_shop.CustomerPicked += Shop_CustomerPicked;
            m_shop.CustomerPaid += Shop_CustomerPaid;
            m_shop.CustomerGaveUp += Shop_CustomerGaveUp;
            m_shop.CustomerExited += Shop_CustomerExited;
        }

        private void OnDestroy()
        {
            if (m_shop != null)
            {
                m_shop.CustomerArrived -= Shop_CustomerArrived;
                m_shop.CustomerPicked -= Shop_CustomerPicked;
                m_shop.CustomerPaid -= Shop_CustomerPaid;
                m_shop.CustomerGaveUp -= Shop_CustomerGaveUp;
                m_shop.CustomerExited -= Shop_CustomerExited;
            }
        }

        // 설계 11: 외형은 광장에서 정해져 온다(customer.Look)
        private void Shop_CustomerArrived(Customer customer)
        {
            ShopCustomer unit = Instantiate(m_prefab, transform);
            unit.Initialize(customer, m_frames, m_view.transform);
            m_units[customer] = unit;
        }

        private void Shop_CustomerPicked(Customer customer)
        {
            Vector3 shelf = m_view.ToWorld(m_shop.Layout.ShelfBase(customer.Cell)) + Vector3.up * k_ShelfBreadHeight;
            m_units[customer].Pick(m_frames.Get(customer.Bread.Sprite)[0], shelf);
        }

        private void Shop_CustomerPaid(Customer customer, double coins)
        {
            string amount = m_tables.Format(k_CoinKey, coins.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
            m_units[customer].Pay(amount, m_tables.Text(k_HappyKey));
        }

        private void Shop_CustomerGaveUp(Customer customer)
        {
            m_units[customer].GiveUp();
        }

        private void Shop_CustomerExited(Customer customer)
        {
            Destroy(m_units[customer].gameObject);
            m_units.Remove(customer);
        }
    }
}

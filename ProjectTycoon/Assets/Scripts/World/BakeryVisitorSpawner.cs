using System.Collections.Generic;
using UnityEngine;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 08 v0.5 · 손님 동선 설계 v0.2: BakeryArea 손님 사건 → 손님 개체 생성·연출·삭제. 걷기와 판단은 Core가 하고 개체는 그 위치를 그린다
    public sealed class BakeryVisitorSpawner : MonoBehaviour
    {
        private const string k_CoinKey = "coin_popup";
        private const string k_HappyKey = "emote_happy";

        [SerializeField] private VisitorView m_prefab;

        private readonly Dictionary<BakeryVisitor, VisitorView> m_units = new Dictionary<BakeryVisitor, VisitorView>();
        private BakeryArea m_shop;
        private BakeryView m_view;
        private TableSet m_tables;
        private FrameCache m_frames;

        public void Initialize(BakeryArea shop, BakeryView view, TableSet tables, FrameCache frames)
        {
            m_shop = shop;
            m_view = view;
            m_tables = tables;
            m_frames = frames;

            m_shop.VisitorArrived += Shop_VisitorArrived;
            m_shop.VisitorPicked += Shop_VisitorPicked;
            m_shop.VisitorPaid += Shop_VisitorPaid;
            m_shop.VisitorGaveUp += Shop_VisitorGaveUp;
            m_shop.VisitorExited += Shop_VisitorExited;
        }

        private void OnDestroy()
        {
            if (m_shop != null)
            {
                m_shop.VisitorArrived -= Shop_VisitorArrived;
                m_shop.VisitorPicked -= Shop_VisitorPicked;
                m_shop.VisitorPaid -= Shop_VisitorPaid;
                m_shop.VisitorGaveUp -= Shop_VisitorGaveUp;
                m_shop.VisitorExited -= Shop_VisitorExited;
            }
        }

        // 설계 11: 외형은 광장에서 정해져 온다(visitor.Look)
        private void Shop_VisitorArrived(BakeryVisitor visitor)
        {
            VisitorView unit = Instantiate(m_prefab, transform);
            unit.Initialize(visitor, m_frames, m_view.transform);
            m_units[visitor] = unit;
        }

        // 빵 그림이 진열대의 빵 자리에서 손으로 날아온다
        private void Shop_VisitorPicked(BakeryVisitor visitor)
        {
            m_units[visitor].Pick(m_frames.Get(visitor.Bread.Sprite)[0], m_view.ShelfIconPosition(m_shop.Shelves[visitor.Cell]));
        }

        private void Shop_VisitorPaid(BakeryVisitor visitor, double coins)
        {
            string amount = m_tables.Format(k_CoinKey, coins.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
            m_units[visitor].Pay(amount, m_tables.Text(k_HappyKey));
        }

        private void Shop_VisitorGaveUp(BakeryVisitor visitor)
        {
            m_units[visitor].GiveUp();
        }

        private void Shop_VisitorExited(BakeryVisitor visitor)
        {
            Destroy(m_units[visitor].gameObject);
            m_units.Remove(visitor);
        }
    }
}

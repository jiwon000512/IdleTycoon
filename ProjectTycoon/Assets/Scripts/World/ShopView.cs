using System;
using System.Collections.Generic;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: 가게 = 구역을 위에서 아래로 쌓은 것(입구 → 진열 층 × n → 계산대 → 오븐 줄 × m).
    // 층 수는 ShopSim이 정하고, 늘면 한 층 끼우고 아래 구역을 내린다(굴 확장). 재고·오븐 표시도 여기서 ShopSim 이벤트로 갱신한다
    public sealed class ShopView : MonoBehaviour
    {
        [SerializeField] private ShopSection m_entrancePrefab;
        [SerializeField] private ShelfRowView m_shelfRowPrefab;
        [SerializeField] private CounterView m_counterPrefab;
        [SerializeField] private OvenRowView m_ovenRowPrefab;
        [Tooltip("가게 폭(유닛). 화면 폭과 같게 둔다")]
        [SerializeField] private float m_width = 6.75f;
        [Tooltip("위 HUD(상단 바·지상으로)에 가리는 몫(유닛). 스크롤 범위에 더한다")]
        [SerializeField] private float m_topPadding = 2f;
        [Tooltip("아래 HUD(업그레이드 버튼)에 가리는 몫(유닛)")]
        [SerializeField] private float m_bottomPadding = 1.2f;
        [Tooltip("줄 칸 간격(유닛). 계산대에서 위로")]
        [SerializeField] private float m_queueSpacing = 0.55f;
        [Tooltip("입구에서 손님이 나타나는 깊이(유닛, 입구 윗변 기준)")]
        [SerializeField] private float m_doorDepth = 1.5f;
        [Tooltip("계산을 마친 손님이 올라가는 통로 x(유닛)")]
        [SerializeField] private float m_exitLaneX = 0.7f;
        [Tooltip("웜뱃이 굽기 심부름 때 서는 자리: 오븐 위로 이만큼(유닛)")]
        [SerializeField] private float m_ovenStandOffset = 1.35f;
        [Tooltip("새 층을 팠을 때 흙빛에서 밝아지는 시간(초)")]
        [SerializeField] private float m_digSeconds = 0.6f;

        private readonly List<ShelfRowView> m_shelfRows = new List<ShelfRowView>();
        private readonly List<OvenRowView> m_ovenRows = new List<OvenRowView>();
        private ShopSection m_entrance;
        private CounterView m_counter;
        private ShopSim m_shop;
        private FrameCache m_frames;
        private GameTables m_tables;
        private float m_height;

        // 새로 판 층의 가운데(카메라가 보여 줄 곳)
        public event Action<Vector2> Expanded;

        public Rect Bounds => new Rect(transform.position.x - m_width * 0.5f, transform.position.y - m_height - m_bottomPadding,
            m_width, m_height + m_topPadding + m_bottomPadding);
        public Vector2 Top => new Vector2(transform.position.x, transform.position.y + m_topPadding);
        public Vector2 Door => (Vector2)transform.position + new Vector2(0f, -m_doorDepth);
        public float ExitLaneX => transform.position.x + m_exitLaneX;

        public void Bind(ShopSim shop, FrameCache frames, GameTables tables)
        {
            m_shop = shop;
            m_frames = frames;
            m_tables = tables;
            m_entrance = Instantiate(m_entrancePrefab, transform);
            m_counter = Instantiate(m_counterPrefab, transform);
            Build();

            m_shop.StockChanged += Shop_StockChanged;
            m_shop.OvenChanged += Shop_OvenChanged;
            m_shop.LayoutChanged += Shop_LayoutChanged;
            m_shop.UpgradesChanged += Shop_UpgradesChanged;
            m_shop.QueueChanged += Shop_QueueChanged;
            m_shop.WombatLeft += Shop_WombatLeft;
            m_shop.WombatAtOven += Shop_WombatAtOven;
        }

        public Vector2 ShelfSpot(int slot)
        {
            return m_shelfRows[slot / 2].Get(slot % 2).StandPoint;
        }

        public Vector2 QueueSpot(int index)
        {
            return m_counter.QueueHead + Vector2.up * (m_queueSpacing * index);
        }

        // 사물 터치 기획: 탭 위치의 사물. 잠긴 진열대·오븐도 맞힌다
        public ShopTarget? HitTest(Vector3 world)
        {
            for (int slot = 0; slot < m_shelfRows.Count * 2; slot++)
            {
                if (m_shelfRows[slot / 2].Get(slot % 2).Contains(world))
                {
                    return new ShopTarget(slot < m_shop.UnlockedBreads.Count ? ShopTargetKind.Shelf : ShopTargetKind.LockedShelf, slot);
                }
            }

            for (int i = 0; i < m_ovenRows.Count * 2; i++)
            {
                if (Oven(i).Contains(world))
                {
                    return new ShopTarget(i < m_shop.Ovens.Count ? ShopTargetKind.Oven : ShopTargetKind.LockedOven, i);
                }
            }

            if (m_counter.Contains(world))
            {
                return new ShopTarget(ShopTargetKind.Counter, 0);
            }

            return null;
        }

        public void Bounce(ShopTarget target)
        {
            switch (target.Kind)
            {
                case ShopTargetKind.Shelf:
                case ShopTargetKind.LockedShelf:
                    m_shelfRows[target.Index / 2].Get(target.Index % 2).Bounce();
                    break;
                case ShopTargetKind.Oven:
                case ShopTargetKind.LockedOven:
                    Oven(target.Index).Bounce();
                    break;
                default:
                    m_counter.Bounce();
                    break;
            }
        }

        private void OnDestroy()
        {
            if (m_shop != null)
            {
                m_shop.StockChanged -= Shop_StockChanged;
                m_shop.OvenChanged -= Shop_OvenChanged;
                m_shop.LayoutChanged -= Shop_LayoutChanged;
                m_shop.UpgradesChanged -= Shop_UpgradesChanged;
                m_shop.QueueChanged -= Shop_QueueChanged;
                m_shop.WombatLeft -= Shop_WombatLeft;
                m_shop.WombatAtOven -= Shop_WombatAtOven;
            }
        }

        private void Shop_StockChanged(string breadId)
        {
            RefreshShelves();
        }

        private void Shop_OvenChanged(int index)
        {
            RefreshOven(index);
        }

        private void Shop_UpgradesChanged()
        {
            RefreshShelves();

            for (int i = 0; i < m_ovenRows.Count * 2; i++)
            {
                RefreshOven(i);
            }
        }

        private void Shop_QueueChanged()
        {
            m_counter.SetServing(m_shop.Queue.Count > 0);
        }

        // v0.6: 오븐까지 걸어가고, 도착하면(굽기 시작) 같은 시간 걸려 계산대로 돌아온다
        private void Shop_WombatLeft(int ovenIndex, double seconds)
        {
            Vector3 stand = Oven(ovenIndex).transform.position + Vector3.up * m_ovenStandOffset;
            m_counter.Wombat.WalkTo(stand, (float)seconds);
        }

        private void Shop_WombatAtOven(int ovenIndex, double seconds)
        {
            m_counter.Wombat.WalkHome((float)seconds);
        }

        private void Shop_LayoutChanged()
        {
            int shelfRows = m_shelfRows.Count;
            int ovenRows = m_ovenRows.Count;
            Build();

            if (m_shelfRows.Count > shelfRows)
            {
                Dig(m_shelfRows[m_shelfRows.Count - 1]);
            }
            else if (m_ovenRows.Count > ovenRows)
            {
                Dig(m_ovenRows[m_ovenRows.Count - 1]);
            }
        }

        // 층 프리팹을 모자란 만큼 만들고 위에서부터 높이를 더해 놓는다
        private void Build()
        {
            while (m_shelfRows.Count < m_shop.ShelfRows)
            {
                m_shelfRows.Add(Instantiate(m_shelfRowPrefab, transform));
            }

            while (m_ovenRows.Count < m_shop.OvenRows)
            {
                m_ovenRows.Add(Instantiate(m_ovenRowPrefab, transform));
            }

            float y = 0f;
            Place(m_entrance, ref y);

            foreach (ShelfRowView row in m_shelfRows)
            {
                Place(row, ref y);
            }

            Place(m_counter, ref y);

            foreach (OvenRowView row in m_ovenRows)
            {
                Place(row, ref y);
            }

            m_height = y;
            m_counter.SetServing(m_shop.Queue.Count > 0);
            RefreshShelves();

            for (int i = 0; i < m_ovenRows.Count * 2; i++)
            {
                RefreshOven(i);
            }
        }

        private static void Place(ShopSection section, ref float y)
        {
            section.transform.localPosition = new Vector3(0f, -y, 0f);
            y += section.Height;
        }

        private void Dig(ShopSection section)
        {
            StartCoroutine(DigRoutine(section.GetComponentsInChildren<SpriteRenderer>()));
            Expanded?.Invoke((Vector2)section.transform.position + Vector2.down * (section.Height * 0.5f));
        }

        // 굴 파기 연출: 새 층이 흙빛에서 밝아진다(아트는 나중)
        private System.Collections.IEnumerator DigRoutine(SpriteRenderer[] renderers)
        {
            Color dirt = new Color(0.45f, 0.3f, 0.18f);

            for (float t = 0f; t <= m_digSeconds; t += Time.deltaTime)
            {
                Color tint = Color.Lerp(dirt, Color.white, t / m_digSeconds);

                foreach (SpriteRenderer r in renderers)
                {
                    r.color = new Color(tint.r, tint.g, tint.b, r.color.a);
                }

                yield return null;
            }

            foreach (SpriteRenderer r in renderers)
            {
                r.color = new Color(1f, 1f, 1f, r.color.a);
            }

            Build();
        }

        private void RefreshShelves()
        {
            for (int slot = 0; slot < m_shelfRows.Count * 2; slot++)
            {
                ShelfView shelf = m_shelfRows[slot / 2].Get(slot % 2);

                if (slot >= m_shop.UnlockedBreads.Count)
                {
                    // 다음 빵 자리(첫 잠긴 칸)에만 굴 넓히기 태그
                    BreadRecord next = m_shop.NextBread;
                    bool nextSlot = slot == m_shop.UnlockedBreads.Count && next != null;
                    shelf.ShowLocked(nextSlot ? m_tables.Strings.Format("tag_unlock", Cost(next.UnlockCost)) : null);
                    continue;
                }

                BreadRecord bread = m_shop.UnlockedBreads[slot];
                shelf.Show(Icon(bread), m_shop.Stock(bread.Id), m_shop.ShelfCapacity);
            }
        }

        private void RefreshOven(int index)
        {
            OvenView view = Oven(index);
            view.SetLook(m_shop.OvenLookUpgraded);

            if (index >= m_shop.Ovens.Count)
            {
                bool nextOven = index == m_shop.Ovens.Count && !m_shop.IsMaxed(ShopSim.k_OvenCount);
                view.ShowLocked(nextOven ? m_tables.Strings.Format("tag_oven", Cost(m_shop.UpgradeCost(ShopSim.k_OvenCount))) : null);
                return;
            }

            Oven oven = m_shop.Ovens[index];

            if (oven.IsEmpty)
            {
                view.ShowEmpty();
                return;
            }

            float progress = 1f - (float)(oven.Remaining / oven.Bread.BakeSeconds);
            view.ShowBaking(Icon(oven.Bread), progress, oven.Ready);
        }

        private OvenView Oven(int index)
        {
            return m_ovenRows[index / 2].Get(index % 2);
        }

        private static string Cost(double cost)
        {
            return cost.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        }

        private Sprite Icon(BreadRecord bread)
        {
            return m_frames.Get(bread.Sprite)[0];
        }
    }
}

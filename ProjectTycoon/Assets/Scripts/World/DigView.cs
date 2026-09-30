using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 27: 굴 파기 화면 조각(빵집·농장 공용). 팔 수 있는 칸의 값 표식(편집 중이면 전부, 아니면 대상 칸만), 판 칸이 흙빛에서 밝아지는 연출과 흙덩이.
    // 코루틴·표식은 곳 화면(host) 밑에서 돈다
    public sealed class DigView
    {
        private const int k_DirtOrder = -1990;
        private static readonly Color k_Dirt = new Color(0.45f, 0.3f, 0.18f);
        // 굴을 판 순간 새 칸에서 튀는 흙덩이(두 빛깔 × k_ClodCount개, 모든 그림 위)
        private static readonly Color k_DirtDark = new Color32(52, 32, 32, 255);
        private const int k_ClodCells = 12;
        private const int k_ClodCount = 12;
        private const float k_ClodSpread = 2.4f;
        private const float k_ClodLift = 4f;
        private const float k_ClodGravity = 12f;
        private const float k_ClodSeconds = 0.6f;
        private const int k_ClodOrder = 1000;

        private readonly MonoBehaviour m_host;
        private readonly MarkerView m_tagPrefab;
        private readonly TableSet m_tables;
        private readonly BurrowGrid m_grid;
        private readonly Func<Cell, Vector3> m_cellCenter;
        private readonly Vector2 m_cellSize;
        private readonly float m_digSeconds;
        private readonly Dictionary<Cell, MarkerView> m_tags = new Dictionary<Cell, MarkerView>();
        private Sprite m_white;

        public DigView(MonoBehaviour host, MarkerView tagPrefab, TableSet tables, BurrowGrid grid, Func<Cell, Vector3> cellCenter, Vector2 cellSize, float digSeconds)
        {
            m_host = host;
            m_tagPrefab = tagPrefab;
            m_tables = tables;
            m_grid = grid;
            m_cellCenter = cellCenter;
            m_cellSize = cellSize;
            m_digSeconds = digSeconds;
        }

        // 편집 중이면 팔 수 있는 칸 전부, 아니면 대상인 칸만
        public void Refresh(bool editing, Interactable target)
        {
            foreach (MarkerView tag in m_tags.Values)
            {
                tag.gameObject.SetActive(false);
            }

            if (editing)
            {
                foreach (Cell cell in m_grid.Frontier())
                {
                    Show(cell);
                }
            }
            else if (target is DigInteractable dig && dig.Grid == m_grid)
            {
                Show(dig.Cell);
            }
        }

        public void Bounce(Cell cell)
        {
            if (m_tags.TryGetValue(cell, out MarkerView tag))
            {
                tag.Bounce();
            }
        }

        // 굴을 팠다: 새 칸 자리에 흙빛 사각형을 얹어 밝아지게 하고, 흙덩이가 튄다
        public void Play(Cell cell)
        {
            Vector3 center = m_cellCenter(cell);
            m_host.StartCoroutine(Fade(center));
            Clods(center);
        }

        // 흙덩이 두 빛깔이 튄다(파기 · 갈기 공용)
        public void Clods(Vector3 center)
        {
            m_host.StartCoroutine(Fx.Burst(m_host.transform, White, center, k_ClodCount, k_ClodSpread, k_ClodLift, k_ClodGravity, k_ClodSeconds, k_ClodCells, k_Dirt, k_ClodOrder));
            m_host.StartCoroutine(Fx.Burst(m_host.transform, White, center, k_ClodCount, k_ClodSpread, k_ClodLift, k_ClodGravity, k_ClodSeconds, k_ClodCells, k_DirtDark, k_ClodOrder));
        }

        private void Show(Cell cell)
        {
            if (!m_tags.TryGetValue(cell, out MarkerView tag))
            {
                tag = UnityEngine.Object.Instantiate(m_tagPrefab, m_host.transform);
                tag.transform.position = m_cellCenter(cell);
                m_tags[cell] = tag;
            }

            tag.Show(m_tables.Format("tag_dig", m_grid.DigCost.ToString("0", System.Globalization.CultureInfo.InvariantCulture)));
        }

        private IEnumerator Fade(Vector3 center)
        {
            GameObject go = new GameObject("Dirt");
            go.transform.SetParent(m_host.transform, false);
            go.transform.position = center;
            go.transform.localScale = new Vector3(m_cellSize.x, m_cellSize.y, 1f);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = White;
            r.sortingOrder = k_DirtOrder;

            for (float t = 0f; t < m_digSeconds; t += Time.deltaTime)
            {
                r.color = new Color(k_Dirt.r, k_Dirt.g, k_Dirt.b, 1f - t / m_digSeconds);
                yield return null;
            }

            UnityEngine.Object.Destroy(go);
        }

        private Sprite White
        {
            get
            {
                if (m_white == null)
                {
                    m_white = Fx.NewSquare();
                }

                return m_white;
            }
        }
    }
}

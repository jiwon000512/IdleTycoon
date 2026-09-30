using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 27: 칸 크기(ConfigTable cellWidth · cellHeight · entranceHeight)로 칸 ↔ 좌표(빵집·농장 공용). 좌표는 굴 원점(입구 줄 윗변 가운데) 기준 유닛, y 위
    public sealed class CellMetrics
    {
        private readonly double m_cellWidth;
        private readonly double m_cellHeight;
        private readonly double m_entranceHeight;

        public float CellWidth => (float)m_cellWidth;
        public float CellHeight => (float)m_cellHeight;

        public CellMetrics(TableSet tables)
        {
            m_cellWidth = tables.Get<ConfigTable>(ConfigTable.k_CellWidth).Value;
            m_cellHeight = tables.Get<ConfigTable>(ConfigTable.k_CellHeight).Value;
            m_entranceHeight = tables.Get<ConfigTable>(ConfigTable.k_EntranceHeight).Value;
        }

        // 줄 윗변(원점에서 아래로 양수). 입구 줄은 0
        public float RowTop(int row)
        {
            return row <= 0 ? 0f : (float)(m_entranceHeight + (row - 1) * m_cellHeight);
        }

        public Vector2 CellCenter(Cell cell)
        {
            float height = (float)(cell.Row == 0 ? m_entranceHeight : m_cellHeight);
            return new Vector2((float)((cell.Col + 0.5) * m_cellWidth), -(RowTop(cell.Row) + height * 0.5f));
        }

        // 칸 밑변 가운데(밭 그림의 발끝)
        public Vector2 CellBase(Cell cell)
        {
            return new Vector2((float)((cell.Col + 0.5) * m_cellWidth), -RowTop(cell.Row + 1));
        }

        public NavRect CellRect(Cell cell)
        {
            float left = (float)(cell.Col * m_cellWidth);
            return new NavRect(left, -RowTop(cell.Row + 1), left + (float)m_cellWidth, -RowTop(cell.Row));
        }

        // 칸 사각형까지 거리(안이면 0). 파기·밭 대상 고르기
        public float DistanceToCell(Cell cell, Vector2 p)
        {
            return CellRect(cell).DistanceTo(p);
        }

        // 판 칸으로 굴 마스크(한 칸 = 1/40유닛)
        public BurrowShape.Result Build(IReadOnlyCollection<Cell> cells)
        {
            int unit = (int)BurrowShape.k_PixelsPerUnit;
            return BurrowShape.Build(cells, (int)Math.Round(m_cellWidth * unit), (int)Math.Round(m_cellHeight * unit), (int)Math.Round(m_entranceHeight * unit));
        }
    }
}

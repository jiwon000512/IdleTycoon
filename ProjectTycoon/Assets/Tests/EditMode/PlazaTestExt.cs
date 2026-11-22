using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 64: 광장은 작은 마당에서 시작한다. 광장 모양과 상관없는 테스트는 굴길 · 주머니를 모두 파 둔다(값 · 문 조건 없이 Grid.Dig)
    internal static class PlazaTestExt
    {
        public static void DigAll(this PlazaArea plaza)
        {
            PlazaConfigTable config = plaza.Tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
            int first = -config.ShaftCols / 2;

            for (int row = 1; row < config.Rows; row++)
            {
                for (int col = first; col < first + config.ShaftCols; col++)
                {
                    Dig(plaza, new Cell(col, row));
                }
            }

            foreach (PlazaPocketData pocket in config.Pockets)
            {
                Dig(plaza, pocket.Cell);
            }
        }

        private static void Dig(PlazaArea plaza, Cell cell)
        {
            if (!plaza.Grid.Contains(cell))
            {
                plaza.Grid.Dig(cell);
            }
        }
    }
}

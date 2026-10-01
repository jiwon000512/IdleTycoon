using GameKit.Tables;

namespace ZooTycoon.Core
{
    public enum MerchantPhase
    {
        // 다음에 올 때까지 세는 중(좌판 자리는 빈 표지판)
        Away,
        // 계단에서 좌판까지 걷는 중
        Coming,
        // 좌판을 펼쳤다(수레 버튼 → 유물 팝업)
        Open,
        // 좌판을 걷고 계단으로 돌아가는 중
        Leaving,
    }

    // 설계 31: 광장 떠돌이 행상. 처음에는 merchantFirst초, 그 뒤로는 올 때마다 merchantEvery초 뒤에 계단으로 내려와 좌판(유물 수레 자리) 뒤에 서면
    // merchantStay초 동안 좌판을 연다. 시간이 다 되면 계단으로 돌아간다. 단계가 바뀔 때마다 MerchantChanged
    public sealed class RelicMerchant
    {
        private readonly PlazaArea m_plaza;
        private readonly PlazaConfigTable m_config;
        private readonly VisitorTable m_look;
        private PlazaVisitor m_figure;
        // 다음에 올 때까지 남은 초(오는 때부터 센다)
        private double m_untilNext;

        public MerchantPhase Phase { get; private set; }
        // 좌판이 열려 있는 남은 초
        public double OpenLeft { get; private set; }
        public double UntilNext => m_untilNext;
        public bool IsOpen => Phase == MerchantPhase.Open;
        public PlazaVisitor Figure => m_figure;

        internal RelicMerchant(PlazaArea plaza, TableSet tables)
        {
            m_plaza = plaza;
            m_config = tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
            m_look = tables.Get<VisitorTable>(m_config.MerchantLook);
            m_untilNext = m_config.MerchantFirst;
        }

        internal void Tick(double dt)
        {
            m_untilNext -= dt;

            switch (Phase)
            {
                case MerchantPhase.Away:
                    if (m_untilNext <= 0d)
                    {
                        m_untilNext = m_config.MerchantEvery;
                        m_figure = m_plaza.SpawnMerchant(m_look);
                        Set(MerchantPhase.Coming);
                    }

                    break;
                case MerchantPhase.Coming:
                    if (m_figure.AtStall)
                    {
                        OpenLeft = m_config.MerchantStay;
                        Set(MerchantPhase.Open);
                    }

                    break;
                case MerchantPhase.Open:
                    OpenLeft -= dt;

                    if (OpenLeft <= 0d)
                    {
                        OpenLeft = 0d;
                        m_figure.Dismiss();
                        Set(MerchantPhase.Leaving);
                    }

                    break;
                default:
                    if (!Contains(m_figure))
                    {
                        m_figure = null;
                        Set(MerchantPhase.Away);
                    }

                    break;
            }
        }

        private bool Contains(PlazaVisitor figure)
        {
            foreach (PlazaVisitor visitor in m_plaza.Visitors)
            {
                if (visitor == figure)
                {
                    return true;
                }
            }

            return false;
        }

        private void Set(MerchantPhase phase)
        {
            Phase = phase;
            m_plaza.Bus.Publish(new Events.MerchantChanged(this));
        }
    }
}

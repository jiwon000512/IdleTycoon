using GameKit.Tables;

namespace ZooTycoon.Core
{
    public enum MerchantPhase
    {
        // 다음에 올 때까지 세는 중
        Away,
        // 계단에서 좌판 자리까지 걷는 중
        Coming,
        // 좌판 자리에 섰다(말 걸면 뽑기 팝업)
        Open,
        // 계단으로 돌아가는 중
        Leaving,
    }

    // 설계 31 · 34: 광장 떠돌이 행상(너구리). 처음에는 merchantFirst초, 그 뒤로는 올 때마다 merchantEvery초 뒤에 계단으로 내려와
    // 좌판 자리(merchantX · Y)에 서서 merchantStay초 동안 말을 받는다. 시간이 다 되면 계단으로 돌아간다. 수레는 없다. 단계가 바뀔 때마다 MerchantChanged
    public sealed class RelicMerchant
    {
        private readonly PlazaArea m_plaza;
        private readonly PlazaConfigTable m_config;
        private readonly VisitorTable m_look;
        private PlazaVisitor m_figure;
        // 다음에 올 때까지 남은 초(오는 때부터 센다)
        private double m_untilNext;

        public MerchantPhase Phase { get; private set; }
        // 좌판 자리에 서 있을 남은 초(오는 중에는 머무는 초 그대로)
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

        // 설계 43: 저장한 「다음에 올 때까지」. 머물던 행상은 떠난 것으로 본다
        internal void Restore(double untilNext)
        {
            m_untilNext = untilNext;
        }

        // 다음 틱에 오게 한다(없을 때만, 에디터 치트)
        public void Summon()
        {
            if (Phase == MerchantPhase.Away)
            {
                m_untilNext = 0d;
            }
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
                        OpenLeft = m_config.MerchantStay;
                        m_figure = m_plaza.SpawnMerchant(m_look);
                        Set(MerchantPhase.Coming);
                    }

                    break;
                case MerchantPhase.Coming:
                    if (m_figure.AtStall)
                    {
                        Set(MerchantPhase.Open);
                    }

                    break;
                case MerchantPhase.Open:
                    OpenLeft -= dt;

                    if (OpenLeft <= 0d)
                    {
                        OpenLeft = 0d;
                        Set(MerchantPhase.Leaving);
                        m_figure.Dismiss();
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

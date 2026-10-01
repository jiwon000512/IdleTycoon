using System;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    public enum MerchantPhase
    {
        // 다음에 올 때까지 세는 중(좌판 자리는 빈 바닥)
        Away,
        // 접힌 수레를 끌고 계단에서 손잡이 자리까지 걷는 중(설계 33: 수레가 좌판 자리에 멈춘다)
        Coming,
        // 수레를 좌판 자리에 세우고 펼치는 중(행상은 앞을 돌아 좌판 자리로, 다 펴고 행상이 서면 엶)
        Unpacking,
        // 좌판을 펼쳤다(수레 버튼 → 좌판 팝업)
        Open,
        // 좌판을 접는 중
        Packing,
        // 접은 수레의 손잡이 쪽으로 걸어가는 중(수레는 아직 좌판 자리)
        Hitching,
        // 접힌 수레를 밀고 계단으로 돌아가는 중
        Leaving,
    }

    // 설계 31 · 32 · 33: 광장 떠돌이 행상. 처음에는 merchantFirst초, 그 뒤로는 올 때마다 merchantEvery초 뒤에 접힌 수레를 끌고 계단으로 내려와
    // 손잡이 자리에 서면 수레를 세워 merchantSetupSeconds초 동안 펼치고(그사이 행상은 좌판 자리로), merchantStay초 동안 좌판을 연다. 시간이 다 되면 접고,
    // 손잡이 쪽으로 가 수레를 잡은 뒤(설계 33) 밀고 계단으로 돌아간다. 수레는 세운 동안(펼침 · 엶 · 접음 · 잡으러 감)만 길을 막는다. 단계가 바뀔 때마다 MerchantChanged
    public sealed class RelicMerchant
    {
        private readonly PlazaArea m_plaza;
        private readonly PlazaConfigTable m_config;
        private readonly VisitorTable m_look;
        private PlazaVisitor m_figure;
        // 다음에 올 때까지 남은 초(오는 때부터 센다)
        private double m_untilNext;

        public MerchantPhase Phase { get; private set; }
        // 좌판이 열려 있는 남은 초(오는 중 · 펼치는 중에는 머무는 초 그대로)
        public double OpenLeft { get; private set; }
        // 펼침 · 접음의 남은 초
        public double SetupLeft { get; private set; }
        public double SetupSeconds => m_config.MerchantSetupSeconds;
        public double UntilNext => m_untilNext;
        public bool IsOpen => Phase == MerchantPhase.Open;
        public bool CartParked => Phase == MerchantPhase.Unpacking || Phase == MerchantPhase.Open || Phase == MerchantPhase.Packing || Phase == MerchantPhase.Hitching;
        public PlazaVisitor Figure => m_figure;

        internal RelicMerchant(PlazaArea plaza, TableSet tables)
        {
            m_plaza = plaza;
            m_config = tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
            m_look = tables.Get<VisitorTable>(m_config.MerchantLook);
            m_untilNext = m_config.MerchantFirst;
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
                    if (m_figure.AtHandle)
                    {
                        SetupLeft = SetupSeconds;
                        Set(MerchantPhase.Unpacking);
                        m_figure.GoToStall();
                    }

                    break;
                case MerchantPhase.Unpacking:
                    SetupLeft = Math.Max(0d, SetupLeft - dt);

                    if (SetupLeft <= 0d && m_figure.AtStall)
                    {
                        Set(MerchantPhase.Open);
                    }

                    break;
                case MerchantPhase.Open:
                    OpenLeft -= dt;

                    if (OpenLeft <= 0d)
                    {
                        OpenLeft = 0d;
                        SetupLeft = SetupSeconds;
                        Set(MerchantPhase.Packing);
                    }

                    break;
                case MerchantPhase.Packing:
                    SetupLeft -= dt;

                    if (SetupLeft <= 0d)
                    {
                        SetupLeft = 0d;
                        Set(MerchantPhase.Hitching);
                        m_figure.GoToHandle();
                    }

                    break;
                case MerchantPhase.Hitching:
                    if (m_figure.AtHandle)
                    {
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

        // 수레를 세우거나 걷으면 광장 길을 다시 깐 뒤 알린다
        private void Set(MerchantPhase phase)
        {
            bool parked = CartParked;
            Phase = phase;

            if (parked != CartParked)
            {
                m_plaza.CartMoved();
            }

            m_plaza.Bus.Publish(new Events.MerchantChanged(this));
        }
    }
}

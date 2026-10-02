using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 40: 평가단장(맛 평가단을 앞장서는 한 마리). 평가가 시작되면 빵집 구멍에서 나와 평가판 옆 자리(StarConfigTable judgeX · judgeY)에서 지켜본다.
    // 조건을 채우면 ♥, 실망한 평가단이 나가면 !!(BakeryArea가 띄운다). 평가가 끝나면 결과 한마디(화면이 EvaluationEnded로 말풍선)를 하고 잠시 뒤 구멍으로 나간다
    public sealed class Judge : Visitor
    {
        // 끝난 뒤 한마디하며 서 있는 시간(초)
        public const double k_LingerSeconds = 2.5;

        private enum Step
        {
            In,
            ToSpot,
            Watch,
            Linger,
            ToHole,
            Out,
        }

        private readonly BakeryArea m_bakery;
        private readonly Vector2 m_spot;
        private Step m_step;
        private double m_linger;

        protected override WombatArea Area => m_bakery;

        internal Judge(int id, VisitorTable look, BakeryArea bakery, Vector2 spot) : base(id, look, bakery.Layout.HoleInside, bakery.Tables)
        {
            m_bakery = bakery;
            m_spot = spot;
            StartHop(VisitorPhase.Entering, bakery.Layout.HoleInside, bakery.Layout.HoleFloor);
        }

        // 평가가 끝났다: 한마디하고 서 있다가 나간다
        internal void Dismiss()
        {
            if (m_step <= Step.Watch)
            {
                m_step = Step.Linger;
                m_linger = k_LingerSeconds;
            }
        }

        protected override bool Act(double dt)
        {
            switch (m_step)
            {
                case Step.In:
                    if (TickHop(dt))
                    {
                        Phase = VisitorPhase.Walking;
                        Mover.WalkTo(m_bakery.Layout.Nav, m_bakery.Layout.Nav.Snap(m_spot), Facing.Down, true);
                        m_step = Step.ToSpot;
                    }

                    return true;
                case Step.ToSpot:
                    if (!Moving)
                    {
                        Phase = VisitorPhase.Looking;
                        Mover.Facing = Facing.Down;
                        m_step = Step.Watch;
                    }

                    return true;
                case Step.Watch:
                    return true;
                case Step.Linger:
                    m_linger -= dt;

                    if (m_linger <= 0d)
                    {
                        Phase = VisitorPhase.Leaving;
                        Mover.WalkTo(m_bakery.Layout.Nav, m_bakery.Layout.HoleFloor, Facing.Up, true);
                        m_step = Step.ToHole;
                    }

                    return true;
                case Step.ToHole:
                    if (!Moving)
                    {
                        StartHop(VisitorPhase.Exiting, m_bakery.Layout.HoleFloor, m_bakery.Layout.HoleInside);
                        m_step = Step.Out;
                    }

                    return true;
                default:
                    return !TickHop(dt);
            }
        }
    }
}

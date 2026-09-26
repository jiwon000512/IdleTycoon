using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 11 · 리뷰 R2: 광장 손님 한 명. 지상 계단(또는 빵집 문)에서 톡 나와 들를 곳 몇 곳을 들르고 빵집 문(자리가 있을 때) 또는 계단으로 간다.
    // 빵집이 꽉 찼으면 한 곳 더 들르고, 그때도 꽉 찼거나 들를 곳이 없으면 떠난다.
    // 설계 22 외출: 점원의 광장 그림(Clerk != null)은 들를 곳을 계속 돌다(없으면 그 자리에서 기다림) ReturnToDoor로 문에 들어가 ClerkCameBack, 해고되면 Dismiss로 계단으로
    public sealed class PlazaVisitor : Visitor
    {
        private enum Goal
        {
            Spot,
            Door,
            Stairs,
        }

        private int m_visitsLeft;
        private bool m_wantsShop;
        // 빵집이 꽉 차서 한 곳 더 들렀다(다음에도 꽉 차면 떠난다)
        private bool m_waited;
        private Goal m_heading;
        // 들를 곳 번호(PlazaLayout.Spots). 없으면 −1
        private int m_spot = -1;
        // 점원 그림: 문으로 돌아가라 · 계단으로 나가라(톡 뛰는 중에 받으면 Next가 처리)
        private bool m_returnHome;
        private bool m_dismissed;

        public PlazaArea Plaza { get; }
        // 외출한 점원의 광장 그림이면 그 점원(말풍선을 같이 쓴다)
        public Clerk Clerk { get; }

        // inside에서 floor로 톡 나온다(계단·빵집 문)
        internal PlazaVisitor(int id, VisitorTable look, PlazaArea plaza, Vector2 inside, Vector2 floor, int visits, bool wantsShop, Clerk clerk = null) : base(id, look, inside, plaza.Tables)
        {
            Plaza = plaza;
            Clerk = clerk;
            m_visitsLeft = visits;
            m_wantsShop = wantsShop;

            if (clerk != null)
            {
                ShareBubble(clerk.Bubble);
            }

            StartHop(VisitorPhase.Entering, inside, floor);
        }

        // 점원 그림: 문으로 가서 들어간다(돌아옴)
        internal void ReturnToDoor()
        {
            m_returnHome = true;
            ReleaseSpot();
            Timer = 0d;

            if (!Hopping)
            {
                Next();
            }
        }

        // 점원 그림: 해고됐다. 계단으로 나간다
        internal void Dismiss()
        {
            m_dismissed = true;
            ReleaseSpot();
            Timer = 0d;

            if (!Hopping)
            {
                Next();
            }
        }

        // false면 광장을 떠났다(빵집 문·계단으로 들어감)
        protected override bool Act(double dt)
        {
            if (Hopping)
            {
                if (!TickHop(dt))
                {
                    return true;
                }

                if (Phase == VisitorPhase.Exiting)
                {
                    // ponytail: 문 앞에서 자리를 확인하고 톡 뛰는 사이에 다른 손님이 먼저 들어가면 한 명 넘칠 수 있다
                    if (m_heading == Goal.Door)
                    {
                        if (Clerk != null)
                        {
                            Plaza.Bus.Publish(new Events.ClerkCameBack(Clerk));
                        }
                        else
                        {
                            Plaza.Bakery.Admit(Look);
                        }
                    }

                    ReleaseSpot();
                    return false;
                }

                Phase = VisitorPhase.Walking;
                Next();
                return true;
            }

            if (Moving)
            {
                return true;
            }

            // 들를 곳에서 머무는 중
            if (Timer > 0d)
            {
                Timer -= dt;

                if (Timer <= 0d)
                {
                    ReleaseSpot();

                    if (Clerk == null)
                    {
                        m_visitsLeft--;
                    }

                    Next();
                }

                return true;
            }

            Arrive();
            return true;
        }

        // 다음 갈 곳: 들를 곳이 남았으면 빈 들를 곳, 아니면 빵집(자리가 있을 때) 또는 계단
        private void Next()
        {
            if (m_dismissed)
            {
                m_heading = Goal.Stairs;
                Mover.WalkTo(Plaza.Layout.Nav, Plaza.Layout.StairsFloor, Facing.Up);
                return;
            }

            if (m_returnHome)
            {
                m_heading = Goal.Door;
                Mover.WalkTo(Plaza.Layout.Nav, Plaza.Layout.DoorFloor, Facing.Up);
                return;
            }

            if (m_visitsLeft > 0 && TryVisitSpot())
            {
                return;
            }

            // 점원 그림은 들를 곳이 없으면 그 자리에서 잠시 기다렸다 다시 찾는다
            if (Clerk != null)
            {
                m_heading = Goal.Spot;
                Timer = Plaza.VisitSeconds();
                return;
            }

            if (m_wantsShop && Plaza.Bakery.CanAdmit)
            {
                m_heading = Goal.Door;
                Mover.WalkTo(Plaza.Layout.Nav, Plaza.Layout.DoorFloor, Facing.Up);
                return;
            }

            if (m_wantsShop && !m_waited && TryVisitSpot())
            {
                m_waited = true;
                m_visitsLeft = 1;
                return;
            }

            m_wantsShop = false;
            m_heading = Goal.Stairs;
            Mover.WalkTo(Plaza.Layout.Nav, Plaza.Layout.StairsFloor, Facing.Up);
        }

        private void Arrive()
        {
            switch (m_heading)
            {
                case Goal.Spot:
                    Timer = Plaza.VisitSeconds();

                    if (Plaza.RollEmote())
                    {
                        Bubble.Show(BubbleTable.k_Heart);
                    }

                    break;
                case Goal.Door:
                    if (Clerk != null || Plaza.Bakery.CanAdmit)
                    {
                        StartHop(VisitorPhase.Exiting, Plaza.Layout.DoorFloor, Plaza.Layout.DoorInside);
                    }
                    else
                    {
                        Next();
                    }

                    break;
                default:
                    StartHop(VisitorPhase.Exiting, Plaza.Layout.StairsFloor, Plaza.Layout.StairsInside);
                    break;
            }
        }

        // 설계 18: 장식이 바뀌어 들를 곳 번호가 다시 매겨졌다. 들르던 곳은 놓고 다음으로, 걷던 길은 새 땅에서 다시
        internal void Relayout()
        {
            if (m_spot >= 0)
            {
                m_spot = -1;
                Timer = 0d;
                Next();
                return;
            }

            if (Moving && !Hopping)
            {
                Mover.WalkTo(Plaza.Layout.Nav, Mover.Destination, Mover.ArriveFacing);
            }
        }

        private bool TryVisitSpot()
        {
            if (!Plaza.TryTakeSpot(out int index))
            {
                return false;
            }

            m_spot = index;
            m_heading = Goal.Spot;
            PlazaSpot spot = Plaza.Layout.Spots[index];
            Mover.WalkTo(Plaza.Layout.Nav, spot.Position, spot.Facing);
            return true;
        }

        private void ReleaseSpot()
        {
            if (m_spot >= 0)
            {
                Plaza.ReleaseSpot(m_spot);
                m_spot = -1;
            }
        }
    }
}

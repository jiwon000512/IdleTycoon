using System;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 손님 동선 설계 v0.2: 화면이 그림을 고르는 상태
    public enum VisitorPhase
    {
        Entering,
        Walking,
        Looking,
        Picking,
        ToQueue,
        Queued,
        Leaving,
        Exiting,
    }

    // 설계 11·13 · 리뷰 R2: 곳을 찾아온 손님 한 명의 공통 뼈대(빵집 BakeryVisitor·광장 PlazaVisitor). 외형(Look)은 광장에서 정해져 빵집 안까지 그대로 간다.
    // 위치·보는 방향은 Mover, 구멍·문에서 톡 뛰기(hop)는 여기, 무엇을 할지는 자식이 Act로 정한다
    public abstract class Visitor
    {
        private readonly double m_walkSpeed;
        private readonly double m_hopSeconds;
        private Vector2 m_hopFrom;
        private Vector2 m_hopTo;

        public int Id { get; }
        public VisitorTable Look { get; }
        public VisitorPhase Phase { get; protected set; }
        // 나오기·나가기 톡 뛰기 진행(0~1)
        public double HopProgress { get; private set; }
        public Vector2 Position => Mover.Position;
        // 비켜 걷기: 가까운 손님과 겹치지 않게 옆으로 비킨 만큼. 길·자리는 그대로이고 화면만 Position에 더한다(광장은 0)
        public Vector2 Sidestep { get; internal set; }
        public Facing Facing => Mover.Facing;
        public bool Moving => Mover.Moving;
        public bool Hopping => Phase == VisitorPhase.Entering || Phase == VisitorPhase.Exiting;

        internal Mover Mover { get; }
        // 설계 22: 머리 위 이모지 말풍선. 외출한 점원의 광장 그림은 점원의 것을 같이 쓴다(ShareBubble, 시간은 주인만 센다)
        public BubbleState Bubble { get; private set; }
        private bool m_ownsBubble = true;

        internal void ShareBubble(BubbleState bubble)
        {
            Bubble = bubble;
            m_ownsBubble = false;
        }
        // 집기·두리번·머물기·톡 뛰기의 남은 초
        protected double Timer { get; set; }

        protected Visitor(int id, VisitorTable look, Vector2 position, TableSet tables)
        {
            Id = id;
            Look = look;
            Mover = new Mover(position, Facing.Down);
            Bubble = new BubbleState(tables);
            m_walkSpeed = tables.Get<ConfigTable>(ConfigTable.k_WalkSpeed).Value;
            m_hopSeconds = tables.Get<ConfigTable>(ConfigTable.k_HopSeconds).Value;
        }

        // 매 프레임: 길을 걷고 할 일을 한다. false면 곳을 떠났다
        public bool Tick(double dt)
        {
            Mover.Advance(m_walkSpeed * dt);

            if (m_ownsBubble)
            {
                Bubble.Tick(dt);
            }

            return Act(dt);
        }

        // 곳에서 할 일. false면 떠난다
        protected abstract bool Act(double dt);

        // 톡 뛰기 시작: 나올 때(Entering)는 아래를, 들어갈 때(Exiting)는 위를 본다
        protected void StartHop(VisitorPhase phase, Vector2 from, Vector2 to)
        {
            Phase = phase;
            Timer = m_hopSeconds;
            HopProgress = 0d;
            m_hopFrom = from;
            m_hopTo = to;
            Mover.Place(from);
            Mover.Facing = phase == VisitorPhase.Exiting ? Facing.Up : Facing.Down;
        }

        // 톡 뛰기 진행. 다 뛰었으면 true
        protected bool TickHop(double dt)
        {
            Timer -= dt;
            double t = Math.Min(1d, 1d - Timer / m_hopSeconds);
            HopProgress = t;
            Mover.Place(Vector2.Lerp(m_hopFrom, m_hopTo, (float)t));
            return Timer <= 0d;
        }
    }
}

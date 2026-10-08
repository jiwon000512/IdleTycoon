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
        // 웜뱃 비켜 가기: 웜뱃 둘레 이 거리 안으로 걸어 들어가지 않는다(발밑 원 둘). 앞길은 k_YieldLook까지만 본다
        internal const float k_YieldRadius = 0.8f;
        private const float k_YieldLook = 1f;
        // 웜뱃이 설 자리(일하는 자리·진열대 앞)에 서 있으면 비킬 때까지 기다린다. 길만 막혔거나 드나드는 곳(구멍·문·계단)이면 돌아갈 길이 없을 때 이 시간(초)만 기다리고 지나간다(가게가 멈추지 않게)
        private const double k_YieldSeconds = 1.5;
        private const double k_DetourGap = 0.3;

        private readonly double m_walkSpeed;
        private readonly double m_hopSeconds;
        private Vector2 m_hopFrom;
        private Vector2 m_hopTo;
        private double m_yielded;
        private double m_detourWait;
        private bool m_passing;
        private double m_hold;

        // seconds 동안 제자리에 선다(걷던 길은 이어 간다)
        internal void Hold(double seconds)
        {
            m_hold = seconds;
        }

        public int Id { get; }
        public VisitorTable Look { get; }
        public VisitorPhase Phase { get; protected set; }
        // 나오기·나가기 톡 뛰기 진행(0~1)
        public double HopProgress { get; private set; }
        public Vector2 Position => Mover.Position;
        // 비켜 걷기: 가까운 손님과 겹치지 않게 옆으로 비킨 만큼. 길·자리는 그대로이고 화면만 Position에 더한다(광장은 0)
        public Vector2 Sidestep { get; internal set; }
        // 화면만 발끝 위로 올린 높이(좌대에 앉음 · 오르내리는 깡충). 길 · 자리는 발끝 그대로
        public float Lift { get; protected set; }
        public Facing Facing => Mover.Facing;
        public bool Moving => Mover.Moving;
        public bool Hopping => Phase == VisitorPhase.Entering || Phase == VisitorPhase.Exiting;
        // 웜뱃이 비키기를 기다리며 서 있다(길은 남아 있다)
        public bool Yielding { get; private set; }
        // 잠깐 멈춤(딴짓 점원에 「?」가 뜬 순간). 길은 남아 있다
        public bool Held => m_hold > 0d;
        // 길이 남았지만 이번 프레임은 걷지 않는다(그림은 서 있는 모습)
        public bool Paused => Yielding || Held;
        // 지금 있는 곳(웜뱃이 어디 있는지 안다)
        protected abstract WombatArea Area { get; }

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
            m_hold -= dt;
            Yielding = Yield(dt);

            if (!Paused)
            {
                Mover.Advance(m_walkSpeed * dt);
            }

            if (m_ownsBubble)
            {
                Bubble.Tick(dt);
            }

            return Act(dt);
        }

        // 앞길이 웜뱃에 막혔으면 돌아가는 길로 바꾸고, 없으면 기다린다. true면 이번 프레임은 걷지 않는다
        private bool Yield(double dt)
        {
            m_detourWait -= dt;

            if (!Moving || Hopping || !Area.WombatPresent)
            {
                m_yielded = 0d;
                m_passing = false;
                return false;
            }

            // 기다리다 지나가기로 했으면 웜뱃 둘레를 벗어날 때까지 다시 서지 않는다
            if (m_passing)
            {
                m_passing = Blocked() || Vector2.Distance(Position, Area.Wombat.Mover.Position) < k_YieldRadius;
                return false;
            }

            if (!Blocked())
            {
                m_yielded = 0d;
                return false;
            }

            // 갈 곳이 웜뱃 둘레 안이면 돌아가도 소용없다: 통로면 기다렸다 지나가고, 아니면 웜뱃이 비킬 때까지 선다
            bool cornered = Vector2.Distance(Mover.Destination, Area.Wombat.Mover.Position) < k_YieldRadius;

            if (cornered && !Area.IsPassage(Mover.Destination))
            {
                return true;
            }

            if (!cornered && m_detourWait <= 0d)
            {
                m_detourWait = k_DetourGap;

                if (Mover.Detour(Area.Wombat.Mover.Position, DetourReach()) && !Blocked())
                {
                    m_yielded = 0d;
                    return false;
                }
            }

            m_yielded += dt;

            if (m_yielded < k_YieldSeconds)
            {
                return true;
            }

            m_yielded = 0d;
            m_passing = true;
            return false;
        }

        // 이미 웜뱃 가까이 있으면(웜뱃이 다가왔다) 지금보다 더 가까워지지만 않으면 된다
        private float Reach()
        {
            return Math.Min(k_YieldRadius, Vector2.Distance(Position, Area.Wombat.Mover.Position) - 0.01f);
        }

        // 돌아가는 길은 반 칸 넓게 찾는다. 길 찾기는 격자 점만 재서, 점은 둘레 밖인데 점 사이 선분이 둘레를 스치면 Blocked가 참으로 남아
        // 길을 바꿨다 되돌리며 제자리에서 떨었다(2026-10-02)
        private float DetourReach()
        {
            return Math.Min(k_YieldRadius + BurrowNav.k_Step * 0.5f, Vector2.Distance(Position, Area.Wombat.Mover.Position) - 0.01f);
        }

        // 지금 걷는 선분의 앞 k_YieldLook 안에서 웜뱃 둘레로 들어가는가
        private bool Blocked()
        {
            Vector2 wombat = Area.Wombat.Mover.Position;
            Vector2 ahead = Mover.NextNode - Position;
            float length = ahead.Length();

            if (length < 1e-4f)
            {
                return false;
            }

            Vector2 dir = ahead / length;
            float along = Math.Clamp(Vector2.Dot(wombat - Position, dir), 0f, Math.Min(length, k_YieldLook));
            return along > 0f && Vector2.Distance(Position + dir * along, wombat) < Reach();
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

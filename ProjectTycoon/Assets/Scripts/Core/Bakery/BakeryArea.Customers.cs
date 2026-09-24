using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 손님 동선 설계 v0.2 5·6·7장: 손님 행동 트리와 잎 행동, 줄·서는 자리
    public sealed partial class BakeryArea
    {
        // 비켜 걷기: 이 거리 안의 손님을 피한다(손님 그림 폭). 비키는 최대 거리는 걷는 땅 여유(k_Clearance 0.3) 안
        private const float k_PersonalSpace = 0.6f;
        private const float k_MaxSidestep = 0.25f;
        private const float k_SidestepRate = 8f;
        // 앞사람이 이만큼도 옆에 있지 않으면 한 줄로 마주 온 것: 오른쪽으로 비킨다
        private const float k_InLine = 0.05f;

        private readonly List<Customer> m_customers = new List<Customer>();
        private readonly List<Customer> m_queue = new List<Customer>();
        private int m_nextCustomerId;

        public IReadOnlyList<Customer> Customers => m_customers;
        // 설계 11: 손님은 광장 빵집 문으로 들어온다(도착 타이머는 PlazaArea)
        public bool CanAdmit => m_customers.Count < m_config.MaxCustomers;
        // 빵을 집은 순서. 걸어오는 중인 손님도 들어 있다
        public IReadOnlyList<Customer> Queue => m_queue;

        public event Action<Customer> CustomerArrived;
        public event Action<Customer> CustomerPicked;
        public event Action QueueChanged;
        public event Action<Customer, double> CustomerPaid;
        public event Action<Customer> CustomerGaveUp;
        public event Action<Customer> CustomerExited;

        // 5장 트리. 마디가 진행 상태를 가지므로 손님마다 새로 만든다
        private BtNode<Customer> BuildBrain()
        {
            BtNode<Customer> pick = new BtAction<Customer>(StartPick, TickPick);
            BtNode<Customer> findOnce = new BtSequence<Customer>(
                new BtAction<Customer>(null, ChooseBread),
                new BtAction<Customer>(StartWalkToShelf, TickWalk),
                new BtSelector<Customer>(
                    pick,
                    new BtSequence<Customer>(
                        new BtAction<Customer>(StartLook, TickLook),
                        new BtAction<Customer>(StartPick, TickPick))));

            return new BtSelector<Customer>(
                new BtSequence<Customer>(
                    new BtAction<Customer>(StartEnter, TickHop),
                    new BtRepeat<Customer>(c => c.Patience > 0d, findOnce),
                    new BtAction<Customer>(StartToQueue, TickToQueue),
                    new BtAction<Customer>(null, TickWaitCheckout),
                    new BtAction<Customer>(StartLeave, TickWalk),
                    new BtAction<Customer>(StartExit, TickHop)),
                new BtSequence<Customer>(
                    new BtAction<Customer>(StartAngry, TickWalk),
                    new BtAction<Customer>(StartExit, TickHop)));
        }

        private void TickCustomers(double dt)
        {
            for (int i = 0; i < m_customers.Count; i++)
            {
                Customer customer = m_customers[i];
                customer.Mover.Advance(m_walkSpeed * dt);

                if (customer.Brain.Tick(customer, dt) != BtStatus.Running)
                {
                    customer.HasSpot = false;
                    m_customers.RemoveAt(i);
                    i--;
                    OnCustomerExited(customer);
                }
            }

            float blend = (float)Math.Min(1d, dt * k_SidestepRate);

            foreach (Customer customer in m_customers)
            {
                customer.Sidestep += (SidestepTarget(customer) - customer.Sidestep) * blend;
            }
        }

        // 걷는 손님은 앞에 있는 손님 반대쪽 옆으로(한 줄로 마주 오면 오른쪽으로), 선 손님은 가까운 손님 반대쪽으로 비킨다
        private Vector2 SidestepTarget(Customer customer)
        {
            if (customer.Hopping)
            {
                return Vector2.Zero;
            }

            Vector2 self = customer.Mover.Position;
            Vector2 ahead = customer.Mover.NextNode - self;
            bool walking = ahead.LengthSquared() > 1e-6f;
            Vector2 left = walking ? Vector2.Normalize(new Vector2(-ahead.Y, ahead.X)) : Vector2.Zero;
            Vector2 push = Vector2.Zero;

            foreach (Customer other in m_customers)
            {
                Vector2 toOther = other.Mover.Position - self;
                float distance = toOther.Length();

                if (other == customer || other.Hopping || distance >= k_PersonalSpace)
                {
                    continue;
                }

                float strength = 1f - distance / k_PersonalSpace;

                if (walking)
                {
                    if (Vector2.Dot(toOther, ahead) > 0f)
                    {
                        push += left * (Vector2.Dot(toOther, left) < -k_InLine ? strength : -strength);
                    }
                }
                else if (distance > 1e-4f)
                {
                    push -= toOther / distance * strength;
                }
            }

            float length = push.Length();
            return (length > 1f ? push / length : push) * k_MaxSidestep;
        }

        // 줄 머리가 머리 자리에 선 뒤에만 계산한다. 웜뱃이 계산대 자리를 비우면 멈춘다
        private void TickCheckout(double dt)
        {
            // 설계 09 v0.4: serve가 auto면 계산대 range 안에 있는 동안만 흐른다. manual이면 버튼(PayHead)으로만
            if (!HeadWaiting || !ServeAuto || !WombatAtCounter)
            {
                return;
            }

            Customer head = m_queue[0];
            head.Timer -= dt * m_counter.UpgradeValue(m_counter.UpgradeLevel);

            if (head.Timer > 0d)
            {
                return;
            }

            PayHead();
        }

        // 줄 머리가 머리 자리에 서서 계산을 기다린다
        internal bool HeadWaiting => m_queue.Count > 0 && m_queue[0].Phase == CustomerPhase.Queued && !m_queue[0].Moving;

        internal void PayHead()
        {
            Customer head = m_queue[0];
            m_queue.RemoveAt(0);
            head.Paid = true;
            head.CarriesBread = false;
            m_state.AddCoins(head.Bread.Price);
            OnCustomerPaid(head, head.Bread.Price);

            for (int i = 0; i < m_queue.Count; i++)
            {
                m_queue[i].Mover.WalkTo(Layout.Nav, Layout.QueueSlots[i], Layout.QueueFacing(i));
            }

            OnQueueChanged();
        }

        // 광장 빵집 문에서 톡 들어온 손님이 구멍에서 나온다(자리 확인은 CanAdmit으로 부르는 쪽이)
        public void Admit(VisitorTable look)
        {
            Customer customer = new Customer(++m_nextCustomerId, look, Layout.HoleInside, m_config.PatienceSeconds);
            customer.Brain = BuildBrain();
            m_customers.Add(customer);
            OnCustomerArrived(customer);
        }

        // 배치가 바뀌면: 줄에 선(서러 가는) 손님은 새 줄 자리로, 걷는 중인 손님은 같은 목적지로 새 길을 찾는다
        private void RepathCustomers()
        {
            foreach (Customer customer in m_customers)
            {
                int index = m_queue.IndexOf(customer);

                if (index >= 0)
                {
                    customer.Mover.WalkTo(Layout.Nav, Layout.QueueSlots[index], Layout.QueueFacing(index));
                }
                else if (customer.Moving)
                {
                    customer.Mover.WalkTo(Layout.Nav, customer.Mover.Destination, customer.Mover.ArriveFacing);
                }
            }
        }

        // ---------- 잎 행동 ----------

        // 구멍 안에서 톡 뛰어내려 구멍 아래 바닥에 선다
        private bool StartEnter(Customer customer)
        {
            customer.Phase = CustomerPhase.Entering;
            customer.Timer = m_hopSeconds;
            customer.HopProgress = 0d;
            customer.Mover.Place(Layout.HoleInside);
            customer.Mover.Facing = Facing.Down;
            return true;
        }

        // 나가기: 구멍 아래 바닥에서 톡 뛰어 구멍 안으로
        private bool StartExit(Customer customer)
        {
            customer.Phase = CustomerPhase.Exiting;
            customer.Timer = m_hopSeconds;
            customer.HopProgress = 0d;
            customer.Mover.Facing = Facing.Up;
            return true;
        }

        private BtStatus TickHop(Customer customer, double dt)
        {
            customer.Timer -= dt;
            double t = Math.Min(1d, 1d - customer.Timer / m_hopSeconds);
            customer.HopProgress = t;
            bool entering = customer.Phase == CustomerPhase.Entering;
            Vector2 from = entering ? Layout.HoleInside : Layout.HoleFloor;
            Vector2 to = entering ? Layout.HoleFloor : Layout.HoleInside;
            customer.Mover.Place(Vector2.Lerp(from, to, (float)t));
            return customer.Timer > 0d ? BtStatus.Running : BtStatus.Success;
        }

        // 안 가 본 빵 중 가중치 난수. 재고는 보지 않는다(가 봐야 안다). 다 가 봤으면 지금 빵 그대로
        private BtStatus ChooseBread(Customer customer, double dt)
        {
            int weightSum = 0;

            foreach (BreadTable bread in m_unlocked)
            {
                weightSum += customer.Tried.Contains(bread.Id) ? 0 : bread.Weight;
            }

            if (weightSum == 0)
            {
                return customer.Bread != null ? BtStatus.Success : BtStatus.Failure;
            }

            double roll = m_random.NextDouble() * weightSum;
            BreadTable chosen = null;

            foreach (BreadTable bread in m_unlocked)
            {
                if (customer.Tried.Contains(bread.Id))
                {
                    continue;
                }

                chosen = bread;
                roll -= bread.Weight;

                if (roll < 0d)
                {
                    break;
                }
            }

            customer.Bread = chosen;
            customer.Cell = ShelfOf(chosen.Id).Cell;
            customer.Tried.Add(chosen.Id);
            return BtStatus.Success;
        }

        // 그 진열대의 빈 서는 자리를 예약하고 걷는다. 이미 그 진열대 자리에 서 있으면 그대로
        private bool StartWalkToShelf(Customer customer)
        {
            customer.Phase = CustomerPhase.Walking;

            if (customer.HasSpot && IsShelfSpot(customer.Cell, customer.Spot))
            {
                return true;
            }

            customer.HasSpot = false;
            Vector2 spot = FreeSpot(customer.Cell);
            customer.Spot = spot;
            customer.HasSpot = true;
            customer.Mover.WalkTo(Layout.Nav, spot, Layout.ShelfFacing(customer.Cell, spot));
            return true;
        }

        private BtStatus TickWalk(Customer customer, double dt)
        {
            return customer.Moving ? BtStatus.Running : BtStatus.Success;
        }

        // 재고가 있으면 하나 집는다(pickSeconds 동안 빵이 머리 위로)
        private bool StartPick(Customer customer)
        {
            if (!m_shelves[customer.Cell].TryPick())
            {
                return false;
            }

            customer.Phase = CustomerPhase.Picking;
            customer.Timer = m_config.PickSeconds;
            OnCustomerPicked(customer);
            return true;
        }

        private BtStatus TickPick(Customer customer, double dt)
        {
            customer.Timer -= dt;

            if (customer.Timer > 0d)
            {
                return BtStatus.Running;
            }

            customer.CarriesBread = true;
            return BtStatus.Success;
        }

        // 빈 진열대 앞에서 최대 lookSeconds(남은 인내까지) 두리번. 그사이 재고가 생기면 바로 성공
        private bool StartLook(Customer customer)
        {
            if (customer.Patience <= 0d)
            {
                return false;
            }

            customer.Phase = CustomerPhase.Looking;
            customer.Timer = Math.Min(m_config.LookSeconds, customer.Patience);
            return true;
        }

        private BtStatus TickLook(Customer customer, double dt)
        {
            if (m_shelves[customer.Cell].Stock > 0)
            {
                return BtStatus.Success;
            }

            customer.Timer -= dt;
            customer.Patience -= dt;
            return customer.Timer > 0d ? BtStatus.Running : BtStatus.Failure;
        }

        // 집은 순서대로 줄 번호를 받고 그 자리로 걷는다
        private bool StartToQueue(Customer customer)
        {
            customer.HasSpot = false;
            customer.Phase = CustomerPhase.ToQueue;
            customer.Timer = m_config.CheckoutSeconds;
            m_queue.Add(customer);
            int index = m_queue.Count - 1;
            customer.Mover.WalkTo(Layout.Nav, Layout.QueueSlots[index], Layout.QueueFacing(index));
            OnQueueChanged();
            return true;
        }

        private BtStatus TickToQueue(Customer customer, double dt)
        {
            if (customer.Moving)
            {
                return BtStatus.Running;
            }

            customer.Phase = CustomerPhase.Queued;
            return BtStatus.Success;
        }

        // 앞사람이 빠지면 한 칸씩 앞으로 걷는다(TickCheckout). 계산이 끝나면 성공
        private BtStatus TickWaitCheckout(Customer customer, double dt)
        {
            return customer.Paid ? BtStatus.Success : BtStatus.Running;
        }

        private bool StartLeave(Customer customer)
        {
            customer.Phase = CustomerPhase.Leaving;
            customer.Mover.WalkTo(Layout.Nav, Layout.HoleFloor, Facing.Up);
            return true;
        }

        // 빵을 못 찾았다: 「!!」 뒤 구멍으로
        private bool StartAngry(Customer customer)
        {
            customer.Angry = true;
            customer.HasSpot = false;
            customer.Phase = CustomerPhase.Leaving;
            customer.Mover.WalkTo(Layout.Nav, Layout.HoleFloor, Facing.Up);
            OnCustomerGaveUp(customer);
            return true;
        }

        // ---------- 서는 자리 ----------

        private Vector2 FreeSpot(Cell cell)
        {
            foreach (Vector2 spot in Layout.ShelfSpots(cell))
            {
                if (!SpotTaken(spot))
                {
                    return spot;
                }
            }

            return Layout.OverflowSpot(cell, SpotTaken);
        }

        private bool SpotTaken(Vector2 p)
        {
            foreach (Customer other in m_customers)
            {
                if (other.HasSpot && Vector2.DistanceSquared(other.Spot, p) < 0.01f)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsShelfSpot(Cell cell, Vector2 p)
        {
            foreach (Vector2 spot in Layout.ShelfSpots(cell))
            {
                if (Vector2.DistanceSquared(spot, p) < 0.01f)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnCustomerArrived(Customer customer)
        {
            CustomerArrived?.Invoke(customer);
        }

        private void OnCustomerPicked(Customer customer)
        {
            CustomerPicked?.Invoke(customer);
        }

        private void OnQueueChanged()
        {
            QueueChanged?.Invoke();
        }

        private void OnCustomerPaid(Customer customer, double coins)
        {
            CustomerPaid?.Invoke(customer, coins);
        }

        private void OnCustomerGaveUp(Customer customer)
        {
            CustomerGaveUp?.Invoke(customer);
        }

        private void OnCustomerExited(Customer customer)
        {
            CustomerExited?.Invoke(customer);
        }
    }
}

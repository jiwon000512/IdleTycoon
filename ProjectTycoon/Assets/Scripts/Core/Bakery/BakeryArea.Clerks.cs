using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 21: 빵집 점원 명부. 자리(오븐·계산대)마다 점원 하나, 가게마다 대기 후보 N명, 월급은 주기마다 지갑에서(모자라면 그 점원만 해고).
    // 후보는 처음 볼 때 채운다(생성자에서 난수를 쓰지 않는다). 점원 한 명의 할 일은 Clerk
    public sealed partial class BakeryArea
    {
        private readonly List<Clerk> m_clerks = new List<Clerk>();
        // 그만두고 구멍으로 가는 중(자리는 이미 비었다)
        private readonly List<Clerk> m_leavingClerks = new List<Clerk>();
        private readonly List<Candidate> m_candidates = new List<Candidate>();
        private readonly List<VisitorTable> m_clerkLooks = new List<VisitorTable>();
        // 설계 22: 딴짓 중인 점원을 감싼 사물(웜뱃이 깨운다). 딴짓 시작·끝에 사물 목록을 다시 맞춘다
        private readonly Dictionary<Clerk, ClerkInteractable> m_clerkThings = new Dictionary<Clerk, ClerkInteractable>();
        private ClerkConfigTable m_clerkConfig;
        private int m_nextClerkId;

        public ClerkConfigTable ClerkConfig => m_clerkConfig;
        public IReadOnlyList<Clerk> Clerks => m_clerks;
        // 설계 22: 지금 도는 대화(없으면 null). 새 대화가 앞 것을 대신한다
        public Dialogue Dialogue { get; private set; }

        // 나 말고 가게 안에서 딴짓 중인 점원(수다 상대). 없으면 null
        public Clerk IdlingClerkOther(Clerk me)
        {
            foreach (Clerk clerk in m_clerks)
            {
                if (clerk != me && clerk.Idling && !clerk.Away && clerk.Idle != IdleKind.Outing)
                {
                    return clerk;
                }
            }

            return null;
        }

        public void StartDialogue(string dialogueId, Clerk clerk)
        {
            Dialogue = new Dialogue(Tables.Get<DialogueTable>(dialogueId), Random, Bus, clerk, Wombat);
        }

        public IReadOnlyList<Candidate> Candidates
        {
            get
            {
                FillCandidates();
                return m_candidates;
            }
        }

        // 이 사물에 붙은 점원. 없으면 null
        public Clerk ClerkOf(Interactable thing)
        {
            foreach (Clerk clerk in m_clerks)
            {
                if (clerk.Thing == thing)
                {
                    return clerk;
                }
            }

            return null;
        }

        // 점원 역할이 있는 사물인가(ClerkTable 행 = 사물 id)
        public bool CanStaff(Interactable thing)
        {
            foreach (ClerkTable role in Tables.GetAll<ClerkTable>())
            {
                if (role.Id == thing.Table.Id)
                {
                    return true;
                }
            }

            return false;
        }

        // 기본 월급 = 역할 baseWage × (1 + 일머리/100 × wagePerSkill), 정수
        public int WageFor(Candidate candidate, Interactable thing)
        {
            ClerkTable role = Tables.Get<ClerkTable>(thing.Table.Id);
            return Math.Max(1, (int)Math.Round(role.BaseWage * (1d + candidate.Skill / 100d * m_clerkConfig.WagePerSkill)));
        }

        public Negotiation Negotiate(Candidate candidate, Interactable thing)
        {
            return new Negotiation(m_clerkConfig, Random, candidate.Skill, WageFor(candidate, thing));
        }

        // 고용: 첫 월급을 내고 구멍에서 나온다. 자리가 찼거나 코인이 모자라면 실패
        public bool TryHire(Candidate candidate, Interactable thing, int wage)
        {
            if (!CanStaff(thing) || ClerkOf(thing) != null || !m_state.TrySpendCoins(wage))
            {
                return false;
            }

            Clerk clerk = new Clerk(++m_nextClerkId, candidate, thing, wage, this);
            m_clerks.Add(clerk);
            m_candidates.Remove(candidate);
            FillCandidates();
            Bus.Publish(new Events.ClerkHired(clerk));
            Bus.Publish(new Events.CandidatesChanged(this));
            return true;
        }

        // 자리는 바로 비고, 점원은 구멍으로 걸어 나간 뒤 사라진다(ClerkLeft)
        public void Fire(Clerk clerk, FireReason reason)
        {
            if (!m_clerks.Remove(clerk))
            {
                return;
            }

            clerk.Leave();
            m_leavingClerks.Add(clerk);
            Bus.Publish(new Events.ClerkFired(clerk, reason));
        }

        public bool TryRefreshCandidates()
        {
            if (!m_state.TrySpendCoins(m_clerkConfig.RefreshCost))
            {
                return false;
            }

            m_candidates.Clear();
            FillCandidates();
            Bus.Publish(new Events.CandidatesChanged(this));
            return true;
        }

        private void InitClerks()
        {
            m_clerkConfig = Tables.Get<ClerkConfigTable>(ClerkConfigTable.k_Main);
            Bus.Subscribe<Events.ClerkCameBack>(e =>
            {
                if (e.Clerk.Bakery == this)
                {
                    e.Clerk.ArriveBack();
                }
            });

            foreach (VisitorTable look in Tables.GetAll<VisitorTable>())
            {
                if (look.Role == VisitorRole.Clerk)
                {
                    m_clerkLooks.Add(look);
                }
            }
        }

        // 월급(점원마다 주기) → 점원 틱 → 나간 점원 정리
        private void TickClerks(double dt)
        {
            for (int i = m_clerks.Count - 1; i >= 0; i--)
            {
                Clerk clerk = m_clerks[i];
                clerk.UntilPay -= dt;

                if (clerk.UntilPay > 0d)
                {
                    continue;
                }

                clerk.UntilPay += m_clerkConfig.WagePeriodSeconds;

                if (!m_state.TrySpendCoins(clerk.Wage))
                {
                    Fire(clerk, FireReason.Unpaid);
                }
            }

            foreach (Clerk clerk in m_clerks)
            {
                clerk.Tick(dt);
            }

            SyncClerkThings();
            Dialogue?.Tick(dt);

            if (Dialogue != null && Dialogue.Done)
            {
                Dialogue = null;
            }

            for (int i = m_leavingClerks.Count - 1; i >= 0; i--)
            {
                Clerk clerk = m_leavingClerks[i];

                if (!clerk.Tick(dt))
                {
                    m_leavingClerks.RemoveAt(i);
                    Bus.Publish(new Events.ClerkLeft(clerk));
                }
            }
        }

        // 딴짓 중인 점원마다 사물 하나. 바뀌었을 때만 목록을 다시 맞춘다(대상은 다음 틱에 고른다)
        private void SyncClerkThings()
        {
            bool changed = false;

            foreach (Clerk clerk in new List<Clerk>(m_clerkThings.Keys))
            {
                if (!clerk.Idling || clerk.Away || !m_clerks.Contains(clerk))
                {
                    m_clerkThings.Remove(clerk);
                    changed = true;
                }
            }

            foreach (Clerk clerk in m_clerks)
            {
                if (clerk.Idling && !clerk.Away && !m_clerkThings.ContainsKey(clerk))
                {
                    m_clerkThings[clerk] = new ClerkInteractable(Tables.Get<InteractableTable>(ClerkInteractable.k_Id), clerk, this, () => clerk.Position);
                    changed = true;
                }
            }

            if (changed)
            {
                SyncThings();
            }
        }

        private void RepathClerks()
        {
            foreach (Clerk clerk in m_clerks)
            {
                clerk.Repath();
            }

            foreach (Clerk clerk in m_leavingClerks)
            {
                clerk.Repath();
            }
        }

        private void FillCandidates()
        {
            while (m_candidates.Count < m_clerkConfig.CandidateCount)
            {
                m_candidates.Add(NewCandidate());
            }
        }

        // 이름은 대기 후보와 겹치지 않게, 일머리는 1 + ⌊99 × r^skew⌋, 외형은 clerk 행 중 균등
        private Candidate NewCandidate()
        {
            List<string> names = new List<string>();

            foreach (string name in m_clerkConfig.Names)
            {
                bool used = false;

                foreach (Candidate candidate in m_candidates)
                {
                    used |= candidate.Name == name;
                }

                if (!used)
                {
                    names.Add(name);
                }
            }

            string picked = names[Math.Min(names.Count - 1, (int)(Random.NextDouble() * names.Count))];
            int skill = 1 + (int)Math.Floor(99d * Math.Pow(Random.NextDouble(), m_clerkConfig.SkillSkew));
            VisitorTable look = m_clerkLooks[Math.Min(m_clerkLooks.Count - 1, (int)(Random.NextDouble() * m_clerkLooks.Count))];
            return new Candidate(picked, skill, look);
        }
    }
}

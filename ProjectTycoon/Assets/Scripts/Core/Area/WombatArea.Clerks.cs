using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 21 → 설계 38: 곳의 점원 명부(빵집 · 농장 공용, 똥과 같은 방식). 자리(ClerkSlots: 오븐 · 계산대 · 농장 작업대)마다 점원 하나, 곳마다 대기 후보 N명.
    // 후보는 처음 볼 때 채운다(생성자에서 난수를 쓰지 않는다). 월급날은 모든 곳 공통이라 Payroll(Mall)이, 점원 한 명의 할 일은 Clerk
    public abstract partial class WombatArea
    {
        private readonly List<Clerk> m_clerks = new List<Clerk>();
        // 그만두고 구멍으로 가는 중(자리는 이미 비었다)
        private readonly List<Clerk> m_leavingClerks = new List<Clerk>();
        private readonly List<Candidate> m_candidates = new List<Candidate>();
        private readonly List<VisitorTable> m_clerkLooks = new List<VisitorTable>();
        // 설계 22: 딴짓 중인 점원을 감싼 사물(웜뱃이 깨운다). 딴짓 시작·끝에 사물 목록을 다시 맞춘다
        private readonly Dictionary<Clerk, ClerkInteractable> m_clerkThings = new Dictionary<Clerk, ClerkInteractable>();
        // 설계 22: 지금 도는 대화들(깨우기 · 수다가 동시에 돈다). 새 대화는 같은 점원이 낀 앞 대화를 대신한다
        private readonly List<Dialogue> m_dialogues = new List<Dialogue>();
        private ClerkConfigTable m_clerkConfig;
        private int m_nextClerkId;

        public ClerkConfigTable ClerkConfig => m_clerkConfig;
        public IReadOnlyList<Clerk> Clerks => m_clerks;

        // 점원을 둘 수 있는 사물(점원 팝업의 자리 줄 순서). 점원이 없는 곳은 비었다
        public virtual IEnumerable<Interactable> ClerkSlots
        {
            get { yield break; }
        }

        // 점원이 드나드는 굴 구멍: 바닥은 입구, 안은 구멍 그림 속(빵집 · 농장 구멍은 같은 자리)
        internal Vector2 HoleFloor => Entrance;
        internal Vector2 HoleInside => new Vector2(Entrance.X, -(BurrowShape.k_EntranceFloorTop - 1) / BurrowShape.k_PixelsPerUnit);
        // 점원이 걷는 땅(웜뱃과 같다)
        internal BurrowNav ClerkNav => WombatNav;

        // 딴짓 중인 점원을 감싼 사물(곳의 사물 목록 맨 앞에 넣는다)
        protected IEnumerable<ClerkInteractable> ClerkThings => m_clerkThings.Values;

        public IReadOnlyList<Candidate> Candidates
        {
            get
            {
                FillCandidates();
                return m_candidates;
            }
        }

        // 이 점원이 낀 대화. 없으면 null
        public Dialogue DialogueOf(Clerk clerk)
        {
            return m_dialogues.Find(d => d.Involves(clerk));
        }

        // 수다 상대: 나 말고 곳 안에 서 있는 가장 가까운 점원(일하는 중이어도 된다. 외출·퇴장·들어오는 중·이미 수다 중은 뺀다). 없으면 null
        internal Clerk ChatPartnerFor(Clerk me)
        {
            Clerk best = null;

            foreach (Clerk clerk in m_clerks)
            {
                if (clerk != me && clerk.CanChat && (best == null || Vector2.Distance(clerk.Position, me.Position) < Vector2.Distance(best.Position, me.Position)))
                {
                    best = clerk;
                }
            }

            return best;
        }

        public Dialogue StartDialogue(string dialogueId, Clerk clerk, Clerk partner = null)
        {
            StopDialogue(clerk);

            if (partner != null)
            {
                StopDialogue(partner);
            }

            Dialogue dialogue = new Dialogue(Tables.Get<DialogueTable>(dialogueId), Random, Bus, clerk, partner);
            m_dialogues.Add(dialogue);
            return dialogue;
        }

        internal void StopDialogue(Clerk clerk)
        {
            m_dialogues.RemoveAll(d => d.Involves(clerk));
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

        // 고용: 첫 월급을 내고 구멍에서 나온다. 다음부터는 공통 월급날(Payroll). 자리가 찼거나 코인이 모자라면 실패
        public bool TryHire(Candidate candidate, Interactable thing, int wage)
        {
            if (!CanStaff(thing) || ClerkOf(thing) != null || !Wombat.Worker.Wallet.TrySpendCoins(wage))
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

        // 설계 43: 저장할 대기 후보(아직 안 채웠으면 비었다)
        internal IReadOnlyList<Candidate> WaitingCandidates => m_candidates;

        // 저장한 점원을 값 없이 들인다(구멍에서 나와 자리로 간다. ClerkHired를 내지 않아 줌인 · 소리 · 이름 말하기가 없다)
        internal void RestoreClerk(Candidate candidate, Interactable thing, int wage, string product)
        {
            Clerk clerk = new Clerk(++m_nextClerkId, candidate, thing, wage, this);

            if (product != null)
            {
                clerk.TrySetProduct(product);
            }

            m_clerks.Add(clerk);
        }

        internal void RestoreCandidates(IEnumerable<Candidate> candidates)
        {
            m_candidates.Clear();
            m_candidates.AddRange(candidates);
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
            if (!Wombat.Worker.Wallet.TrySpendCoins(m_clerkConfig.RefreshCost))
            {
                return false;
            }

            m_candidates.Clear();
            FillCandidates();
            Bus.Publish(new Events.CandidatesChanged(this));
            return true;
        }

        // 딴짓 점원 사물이 바뀌었다: 곳이 사물 목록을 다시 맞춘다
        protected virtual void OnClerkThingsChanged()
        {
        }

        private void InitClerks()
        {
            m_clerkConfig = Tables.Get<ClerkConfigTable>(ClerkConfigTable.k_Main);
            Bus.Subscribe<Events.ClerkCameBack>(e =>
            {
                if (e.Clerk.Home == this)
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

        // 점원 틱 → 딴짓 사물 → 대화 → 나간 점원 정리
        private void TickClerks(double dt)
        {
            foreach (Clerk clerk in m_clerks)
            {
                clerk.Tick(dt);
            }

            SyncClerkThings();

            foreach (Dialogue dialogue in m_dialogues.ToArray())
            {
                dialogue.Tick(dt);
            }

            m_dialogues.RemoveAll(d => d.Done);

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
                OnClerkThingsChanged();
            }
        }

        protected void RepathClerks()
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

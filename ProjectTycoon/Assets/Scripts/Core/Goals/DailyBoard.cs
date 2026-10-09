using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 54: 오늘의 일. 하루(기기 시각 missionResetHour부터 다음 날 그 시각 전까지)마다 MissionTable에서 할 수 있는 행만 weight 비중으로 missionsPerDay개,
    // 같은 곳은 missionSameAreaMax개까지 뽑는다(그날의 씨앗이라 같은 날 다시 뽑아도 같다). 다 하고 받으면 점수, 하루 점수가 상자(MissionChestTable)의 points에 닿으면 연다.
    // 시계는 주입받는다(게임은 기기 시각, 테스트는 가짜 시계). 상자 값은 퀘스트 장(chapter)마다
    public sealed class DailyBoard
    {
        public sealed class Mission
        {
            public MissionTable Row { get; }
            public int Progress { get; internal set; }
            public bool Claimed { get; internal set; }
            public bool IsDone => Progress >= Row.Count;

            internal Mission(MissionTable row)
            {
                Row = row;
            }
        }

        private readonly Mall m_mall;
        private readonly EventBus m_bus;
        private readonly Func<DateTime> m_clock;
        private readonly Func<int> m_chapter;
        private readonly IReadOnlyList<MissionTable> m_rows;
        private readonly IReadOnlyList<MissionChestTable> m_chests;
        private readonly List<Mission> m_missions = new List<Mission>();
        private readonly HashSet<int> m_opened = new HashSet<int>();
        private readonly int m_perDay;
        private readonly int m_resetHour;
        private readonly int m_sameAreaMax;

        // 지금 하루(그날 새벽 resetHour의 날짜, yyyy-MM-dd). 처음 Tick 전에는 null
        public string Day { get; private set; }
        public IReadOnlyList<Mission> Missions => m_missions;
        public int Points { get; private set; }
        // 연 상자(chest 번호)
        public IReadOnlyCollection<int> Opened => m_opened;
        // 지금 장의 상자들(chest 순서). 표에 그 장이 없으면 그 아래 가장 가까운 장
        public IReadOnlyList<MissionChestTable> Chests
        {
            get
            {
                int chapter = m_chapter();
                int found = m_chests.Where(c => c.Chapter <= chapter).Select(c => c.Chapter).DefaultIfEmpty(m_chests.Min(c => c.Chapter)).Max();
                return m_chests.Where(c => c.Chapter == found).OrderBy(c => c.Chest).ToList();
            }
        }
        // 새 하루까지 남은 초
        public double SecondsLeft => (StartOf(m_clock()).AddDays(1) - m_clock()).TotalSeconds;
        // 받을 것이 있나(메뉴 버튼 점)
        public bool HasReward => m_missions.Any(m => m.IsDone && !m.Claimed) || Chests.Any(c => Points >= c.Points && !m_opened.Contains(c.Chest));

        public DailyBoard(Mall mall, TableSet tables, EventBus bus, Func<DateTime> clock, Func<int> chapter)
        {
            m_mall = mall;
            m_bus = bus;
            m_clock = clock;
            m_chapter = chapter;
            m_rows = tables.GetAll<MissionTable>();
            m_chests = tables.GetAll<MissionChestTable>();
            m_perDay = (int)tables.Get<ConfigTable>(ConfigTable.k_MissionsPerDay).Value;
            m_resetHour = (int)tables.Get<ConfigTable>(ConfigTable.k_MissionResetHour).Value;
            m_sameAreaMax = (int)tables.Get<ConfigTable>(ConfigTable.k_MissionSameAreaMax).Value;
            GoalCounter.Listen(bus, Counted);
        }

        // 매 프레임: 하루가 바뀌었으면(처음 · 플레이 중 · 꺼 둔 사이) 새로 뽑는다
        public void Tick()
        {
            string day = DayOf(m_clock());

            if (day != Day)
            {
                Roll(day);
            }
        }

        // 받기: 다 한 미션의 점수를 받는다
        public bool TryClaim(int index)
        {
            if (index < 0 || index >= m_missions.Count || !m_missions[index].IsDone || m_missions[index].Claimed)
            {
                return false;
            }

            m_missions[index].Claimed = true;
            Points += m_missions[index].Row.Points;
            Changed();
            return true;
        }

        // 상자 열기: 점수가 닿은 상자의 보상(분어치 × 1분 수입 + 재료)
        public bool TryOpen(int chest)
        {
            MissionChestTable row = Chests.FirstOrDefault(c => c.Chest == chest);

            if (row == null || Points < row.Points || !m_opened.Add(chest))
            {
                return false;
            }

            Dictionary<string, int> items = new Dictionary<string, int>();

            if (row.Item != null && row.ItemCount > 0)
            {
                items[row.Item] = row.ItemCount;
            }

            Rewards.Grant(m_mall.Bakery.Wallet, m_bus, Math.Round(row.Coins), items);
            Changed();
            return true;
        }

        // 치트: 오늘의 일을 모두 다 한 것으로(받기는 직접)
        public void CompleteAll()
        {
            foreach (Mission mission in m_missions)
            {
                mission.Progress = mission.Row.Count;
            }

            Changed();
        }

        // 설계 43: 저장한 하루(같은 날이면 이어 가고, 날이 바뀌었으면 다음 Tick이 새로 뽑는다). 표에서 사라진 미션은 뺀다
        internal void Restore(string day, IEnumerable<(string Id, int Progress, bool Claimed)> missions, int points, IEnumerable<int> opened)
        {
            Day = day;
            m_missions.Clear();

            foreach ((string id, int progress, bool claimed) in missions)
            {
                MissionTable row = m_rows.FirstOrDefault(r => r.Id == id);

                if (row != null)
                {
                    m_missions.Add(new Mission(row) { Progress = Math.Min(row.Count, progress), Claimed = claimed });
                }
            }

            Points = points;
            m_opened.Clear();
            m_opened.UnionWith(opened);
            Changed();
        }

        private void Counted(string kind, string param, int count)
        {
            bool changed = false;

            foreach (Mission mission in m_missions)
            {
                MissionTable row = mission.Row;

                if (row.Kind == kind && (row.Param == null || row.Param == param) && !mission.IsDone)
                {
                    mission.Progress = Math.Min(row.Count, mission.Progress + count);
                    changed = true;
                }
            }

            if (changed)
            {
                Changed();
            }
        }

        private void Roll(string day)
        {
            Day = day;
            Points = 0;
            m_opened.Clear();
            m_missions.Clear();
            IRandom random = new SeededRandom(Seed(day));
            List<MissionTable> pool = m_rows.Where(CanPick).ToList();

            while (m_missions.Count < m_perDay && pool.Count > 0)
            {
                double roll = random.NextDouble() * pool.Sum(row => row.Weight);
                MissionTable chosen = pool[pool.Count - 1];

                foreach (MissionTable row in pool)
                {
                    if (roll < row.Weight)
                    {
                        chosen = row;
                        break;
                    }

                    roll -= row.Weight;
                }

                m_missions.Add(new Mission(chosen));
                pool.Remove(chosen);

                if (chosen.Area != null && m_missions.Count(m => m.Row.Area == chosen.Area) >= m_sameAreaMax)
                {
                    pool.RemoveAll(row => row.Area == chosen.Area);
                }
            }

            Changed();
        }

        private bool CanPick(MissionTable row)
        {
            if (row.Weight <= 0d || !GoalCounter.IsPossible(row.Kind, m_mall))
            {
                return false;
            }

            if (row.Area != null && !GoalCounter.IsAreaOpen(row.Area, m_mall))
            {
                return false;
            }

            if (row.Needs == null)
            {
                return true;
            }

            string[] needs = row.Needs.Split(':');
            return GoalCounter.Current(needs[0], needs.Length > 1 && needs[1].Length > 0 ? needs[1] : null, m_mall) >= 1;
        }

        private void Changed()
        {
            m_bus.Publish(new Events.MissionsChanged(this));
        }

        private DateTime StartOf(DateTime now)
        {
            return now.AddHours(-m_resetHour).Date.AddHours(m_resetHour);
        }

        private string DayOf(DateTime now)
        {
            return now.AddHours(-m_resetHour).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        // 그날의 씨앗(문자열 해시는 실행마다 달라 쓰지 않는다)
        private static int Seed(string day)
        {
            int seed = 17;

            foreach (char c in day)
            {
                seed = unchecked(seed * 31 + c);
            }

            return seed;
        }
    }
}

using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    public sealed class GameTables
    {
        private readonly Dictionary<string, AnimalRecord> m_animalsById =
            new Dictionary<string, AnimalRecord>(StringComparer.Ordinal);

        private readonly Dictionary<string, GradeRecord> m_gradesById =
            new Dictionary<string, GradeRecord>(StringComparer.Ordinal);

        public IReadOnlyList<AnimalRecord> Animals { get; }
        public IReadOnlyList<GradeRecord> Grades { get; }
        public IReadOnlyList<ZooLevelRecord> ZooLevels { get; }
        public IReadOnlyList<VisitorRecord> Visitors { get; }
        public IReadOnlyList<FacilityRecord> Facilities { get; }
        public IReadOnlyList<StringRecord> StringRows { get; }
        public StringTable Strings { get; }
        public GameConfig Config { get; }

        // 규칙 예외: 테이블 집합체라 생성자 매개변수가 7개다(코드-규칙 3장 상한). 나눌 축이 없다.
        public GameTables(
            IReadOnlyList<AnimalRecord> animals,
            IReadOnlyList<GradeRecord> grades,
            IReadOnlyList<ZooLevelRecord> zooLevels,
            IReadOnlyList<VisitorRecord> visitors,
            IReadOnlyList<FacilityRecord> facilities,
            IReadOnlyList<StringRecord> strings,
            GameConfig config)
        {
            Animals = animals;
            Grades = grades;
            ZooLevels = zooLevels;
            Visitors = visitors;
            Facilities = facilities;
            StringRows = strings;
            Strings = new StringTable(strings);
            Config = config;

            for (int i = 0; i < animals.Count; i++)
            {
                m_animalsById[animals[i].Id] = animals[i];
            }

            for (int i = 0; i < grades.Count; i++)
            {
                m_gradesById[grades[i].Id] = grades[i];
            }
        }

        public AnimalRecord GetAnimal(string id)
        {
            if (!m_animalsById.TryGetValue(id, out AnimalRecord animal))
            {
                throw new KeyNotFoundException($"동물 ID '{id}'가 animals.json에 없다.");
            }

            return animal;
        }

        public GradeRecord GetGrade(string id)
        {
            if (!m_gradesById.TryGetValue(id, out GradeRecord grade))
            {
                throw new KeyNotFoundException($"등급 ID '{id}'가 grades.json에 없다.");
            }

            return grade;
        }

        public bool HasGrade(string id)
        {
            return id != null && m_gradesById.ContainsKey(id);
        }
    }
}

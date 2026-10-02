namespace ZooTycoon.Core
{
    // 설계 21: 고용 후보 한 명. 이름·일머리(1~100, 높을수록 드묾)·외형. 월급은 붙일 사물의 역할이 정한다(BakeryArea.WageFor)
    public readonly struct Candidate
    {
        public readonly string Name;
        public readonly int Skill;
        public readonly VisitorTable Look;

        public Candidate(string name, int skill, VisitorTable look)
        {
            Name = name;
            Skill = skill;
            Look = look;
        }
    }
}

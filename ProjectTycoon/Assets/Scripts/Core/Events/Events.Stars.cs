namespace ZooTycoon.Core
{
    // 설계 40: 별 · 평가 사건
    public static partial class Events
    {
        // 가게 별이 늘었다(간판 · 상한 · 효과 배수)
        public readonly struct StarsChanged
        {
            public readonly string Shop;
            public readonly int Count;

            public StarsChanged(string shop, int count)
            {
                Shop = shop;
                Count = count;
            }
        }

        // 평가판(칠판) 앞 버튼: 평가 팝업을 연다
        public readonly struct EvaluationBoardOpened
        {
            public readonly Evaluation Evaluation;

            public EvaluationBoardOpened(Evaluation evaluation)
            {
                Evaluation = evaluation;
            }
        }

        // 평가가 시작됐다(상단 평가 알약 · 평가단장 · 소리)
        public readonly struct EvaluationStarted
        {
            public readonly Evaluation Evaluation;

            public EvaluationStarted(Evaluation evaluation)
            {
                Evaluation = evaluation;
            }
        }

        // 평가가 끝났다. 통과면 별이 이미 올랐다(소식지 · 평가단장 한마디)
        public readonly struct EvaluationEnded
        {
            public readonly Evaluation Evaluation;
            public readonly bool Passed;

            public EvaluationEnded(Evaluation evaluation, bool passed)
            {
                Evaluation = evaluation;
                Passed = passed;
            }
        }

        // 평가 조건 하나를 채웠다 · 평가단이 실망해 나갔다(평가단장 ♥ · !!)
        public readonly struct EvaluationProgressed
        {
            public readonly Evaluation Evaluation;
            public readonly bool Good;

            public EvaluationProgressed(Evaluation evaluation, bool good)
            {
                Evaluation = evaluation;
                Good = good;
            }
        }

        // 평가단장이 빵집 구멍에서 나왔다 · 나갔다(그림)
        public readonly struct JudgeArrived
        {
            public readonly Judge Judge;

            public JudgeArrived(Judge judge)
            {
                Judge = judge;
            }
        }

        public readonly struct JudgeLeft
        {
            public readonly Judge Judge;

            public JudgeLeft(Judge judge)
            {
                Judge = judge;
            }
        }

        // 계산 때 팁이 나왔다(그 손님 머리 위 「팁 +N」 · 소리)
        public readonly struct Tipped
        {
            public readonly BakeryVisitor Visitor;
            public readonly double Coins;

            public Tipped(BakeryVisitor visitor, double coins)
            {
                Visitor = visitor;
                Coins = coins;
            }
        }
    }
}

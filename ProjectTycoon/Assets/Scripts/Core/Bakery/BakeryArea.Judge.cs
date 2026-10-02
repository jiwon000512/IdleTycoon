using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 40: 빵집 평가단장. 평가가 시작되면 구멍에서 나오고(JudgeArrived), 진행에 따라 ♥ · !!, 끝나면 한마디 뒤 나간다(JudgeLeft)
    public sealed partial class BakeryArea
    {
        // 지금 빵집에 있는 평가단장(없으면 null)
        public Judge Judge { get; private set; }

        private void InitJudge()
        {
            Bus.Subscribe<Events.EvaluationStarted>(Bus_EvaluationStarted);
            Bus.Subscribe<Events.EvaluationProgressed>(Bus_EvaluationProgressed);
            Bus.Subscribe<Events.EvaluationEnded>(Bus_EvaluationEnded);
        }

        private void TickJudge(double dt)
        {
            if (Judge == null || Judge.Tick(dt))
            {
                return;
            }

            Judge left = Judge;
            Judge = null;
            Bus.Publish(new Events.JudgeLeft(left));
        }

        // 앞 평가의 평가단장이 아직 나가는 중이면 그대로 두고 새로 부르지 않는다
        private void Bus_EvaluationStarted(Events.EvaluationStarted e)
        {
            if (e.Evaluation != Evaluation || Judge != null)
            {
                return;
            }

            StarConfigTable config = Evaluation.Config;
            Judge = new Judge(++m_nextVisitorId, Tables.Get<VisitorTable>(config.JudgeLook), this, new Vector2((float)config.JudgeX, (float)config.JudgeY));
            Bus.Publish(new Events.JudgeArrived(Judge));
        }

        private void Bus_EvaluationProgressed(Events.EvaluationProgressed e)
        {
            if (e.Evaluation == Evaluation && Judge != null)
            {
                Judge.Bubble.Show(e.Good ? BubbleTable.k_Heart : BubbleTable.k_Angry);
            }
        }

        private void Bus_EvaluationEnded(Events.EvaluationEnded e)
        {
            if (e.Evaluation == Evaluation && Judge != null)
            {
                Judge.Bubble.Clear();
                Judge.Dismiss();
            }
        }
    }
}

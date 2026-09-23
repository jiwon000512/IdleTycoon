using System.Collections.Generic;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 손님 동선 설계 v0.2 검증 2: 행동 트리 마디의 성공·실패·진행 전파와 되감기
    public sealed class BtTests
    {
        private sealed class Log
        {
            public readonly List<string> Calls = new List<string>();
        }

        // n번째 틱에 결과를 내는 행동. 그 전에는 진행 중
        private static BtAction<Log> Leaf(string name, BtStatus result, int ticks = 1)
        {
            int count = 0;
            return new BtAction<Log>(log =>
            {
                count = 0;
                log.Calls.Add(name + ":start");
                return true;
            }, (log, dt) =>
            {
                count++;
                return count < ticks ? BtStatus.Running : result;
            });
        }

        [Test]
        public void Sequence_RunsChildrenInOrder_AndSucceedsWhenAllDo()
        {
            Log log = new Log();
            BtSequence<Log> sequence = new BtSequence<Log>(Leaf("a", BtStatus.Success, 2), Leaf("b", BtStatus.Success));

            Assert.That(sequence.Tick(log, 0.1), Is.EqualTo(BtStatus.Running));
            Assert.That(sequence.Tick(log, 0.1), Is.EqualTo(BtStatus.Success));
            Assert.That(log.Calls, Is.EqualTo(new[] { "a:start", "b:start" }));
        }

        [Test]
        public void Sequence_FailsOnFirstFailure_AndRestartsFromTheTop()
        {
            Log log = new Log();
            BtSequence<Log> sequence = new BtSequence<Log>(Leaf("a", BtStatus.Success), Leaf("b", BtStatus.Failure), Leaf("c", BtStatus.Success));

            Assert.That(sequence.Tick(log, 0.1), Is.EqualTo(BtStatus.Failure));
            Assert.That(sequence.Tick(log, 0.1), Is.EqualTo(BtStatus.Failure));
            Assert.That(log.Calls, Is.EqualTo(new[] { "a:start", "b:start", "a:start", "b:start" }));
        }

        [Test]
        public void Selector_StopsAtFirstSuccess_AndFailsWhenAllFail()
        {
            Log log = new Log();
            BtSelector<Log> pick = new BtSelector<Log>(Leaf("a", BtStatus.Failure), Leaf("b", BtStatus.Success), Leaf("c", BtStatus.Success));
            BtSelector<Log> none = new BtSelector<Log>(Leaf("x", BtStatus.Failure), Leaf("y", BtStatus.Failure));

            Assert.That(pick.Tick(log, 0.1), Is.EqualTo(BtStatus.Success));
            Assert.That(none.Tick(log, 0.1), Is.EqualTo(BtStatus.Failure));
            Assert.That(log.Calls, Is.EqualTo(new[] { "a:start", "b:start", "x:start", "y:start" }));
        }

        [Test]
        public void Repeat_RetriesWhileConditionHolds_ThenFails()
        {
            Log log = new Log();
            int tries = 0;
            BtRepeat<Log> repeat = new BtRepeat<Log>(l => ++tries < 3, Leaf("a", BtStatus.Failure));

            Assert.That(repeat.Tick(log, 0.1), Is.EqualTo(BtStatus.Running));
            Assert.That(repeat.Tick(log, 0.1), Is.EqualTo(BtStatus.Running));
            Assert.That(repeat.Tick(log, 0.1), Is.EqualTo(BtStatus.Failure));
            Assert.That(log.Calls.Count, Is.EqualTo(3));
        }

        [Test]
        public void Action_WhoseStartFails_FailsWithoutTicking()
        {
            Log log = new Log();
            bool ticked = false;
            BtAction<Log> action = new BtAction<Log>(l => false, (l, dt) =>
            {
                ticked = true;
                return BtStatus.Success;
            });

            Assert.That(action.Tick(log, 0.1), Is.EqualTo(BtStatus.Failure));
            Assert.That(ticked, Is.False);
        }
    }
}

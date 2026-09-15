namespace ZooTycoon.Core
{
    public sealed class PullResult
    {
        public AnimalRecord Animal { get; }
        public PullOutcome Outcome { get; }
        public int Count { get; }
        public double Cost { get; }

        public PullResult(AnimalRecord animal, PullOutcome outcome, int count, double cost)
        {
            Animal = animal;
            Outcome = outcome;
            Count = count;
            Cost = cost;
        }
    }
}

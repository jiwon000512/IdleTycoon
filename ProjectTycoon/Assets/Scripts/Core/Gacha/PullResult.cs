namespace ZooTycoon.Core
{
    public sealed class PullResult
    {
        public AnimalRecord Animal { get; }
        public PullOutcome Outcome { get; }
        public int Level { get; }
        public double Cost { get; }

        public PullResult(AnimalRecord animal, PullOutcome outcome, int level, double cost)
        {
            Animal = animal;
            Outcome = outcome;
            Level = level;
            Cost = cost;
        }
    }
}

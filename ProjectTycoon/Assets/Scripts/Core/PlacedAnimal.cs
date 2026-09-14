namespace ZooTycoon.Core
{
    public sealed class PlacedAnimal
    {
        public string AnimalId { get; }
        public int Level { get; }

        public PlacedAnimal(string animalId, int level)
        {
            AnimalId = animalId;
            Level = level;
        }
    }
}

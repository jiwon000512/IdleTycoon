namespace ZooTycoon.Core
{
    public sealed class OwnedAnimal
    {
        public string AnimalId { get; }
        public int Level { get; internal set; }

        public OwnedAnimal(string animalId, int level)
        {
            AnimalId = animalId;
            Level = level;
        }
    }
}

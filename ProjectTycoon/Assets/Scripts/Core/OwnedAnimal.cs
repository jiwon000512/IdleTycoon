namespace ZooTycoon.Core
{
    public sealed class OwnedAnimal
    {
        public string AnimalId { get; }
        public int Level { get; private set; }

        public OwnedAnimal(string animalId, int level)
        {
            AnimalId = animalId;
            Level = level;
        }

        internal void LevelUp()
        {
            Level++;
        }
    }
}

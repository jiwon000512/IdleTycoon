namespace ZooTycoon.Core
{
    public sealed class OwnedAnimal
    {
        public string AnimalId { get; }
        // 기획서 6.2: 마리 수. 중복 뽑기 1회당 +1
        public int Count { get; internal set; }

        public OwnedAnimal(string animalId, int count)
        {
            AnimalId = animalId;
            Count = count;
        }
    }
}

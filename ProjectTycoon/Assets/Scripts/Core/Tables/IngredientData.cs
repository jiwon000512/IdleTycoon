namespace ZooTycoon.Core
{
    // 설계 25: 레시피 한 칸(재료 id · 개수). BreadTable.ingredients
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class IngredientData
    {
        public string Item { get; set; }
        public int Count { get; set; }
    }
}

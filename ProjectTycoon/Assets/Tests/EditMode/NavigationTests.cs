using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 08 v0.2: 지상 ↔ 가게 전환
    public sealed class NavigationTests
    {
        [Test]
        public void New_StartsOnOverworld()
        {
            Assert.That(new Navigation().Current, Is.EqualTo(GameScreen.Overworld));
        }

        [Test]
        public void EnterThenExit_ChangesScreenAndNotifiesEachTime()
        {
            Navigation navigation = new Navigation();
            int changed = 0;
            navigation.Changed += () => changed++;

            navigation.EnterShop();
            Assert.That(navigation.Current, Is.EqualTo(GameScreen.Shop));

            navigation.ExitShop();
            Assert.That(navigation.Current, Is.EqualTo(GameScreen.Overworld));
            Assert.That(changed, Is.EqualTo(2));
        }

        [Test]
        public void EnterShop_WhenAlreadyInShop_DoesNotNotify()
        {
            Navigation navigation = new Navigation();
            navigation.EnterShop();
            int changed = 0;
            navigation.Changed += () => changed++;

            navigation.EnterShop();

            Assert.That(changed, Is.EqualTo(0));
        }
    }
}

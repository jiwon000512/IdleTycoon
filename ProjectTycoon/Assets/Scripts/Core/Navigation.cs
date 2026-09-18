using System;

namespace ZooTycoon.Core
{
    public enum GameScreen
    {
        Overworld,
        Shop,
    }

    // 설계 08 v0.2: 지금 보고 있는 화면. 규칙이 아니라 "어디를 보나"지만 UI와 World가 서로 모른 채 같이 따라야 해서 Core에 둔다
    public sealed class Navigation
    {
        public GameScreen Current { get; private set; } = GameScreen.Overworld;

        public event Action Changed;

        // 08은 굴이 빵집 하나라 인자가 없다. 굴이 늘면(12) 어느 굴인지 받는다
        public void EnterShop()
        {
            Go(GameScreen.Shop);
        }

        public void ExitShop()
        {
            Go(GameScreen.Overworld);
        }

        private void Go(GameScreen screen)
        {
            if (Current == screen)
            {
                return;
            }

            Current = screen;
            OnChanged();
        }

        private void OnChanged()
        {
            Changed?.Invoke();
        }
    }
}

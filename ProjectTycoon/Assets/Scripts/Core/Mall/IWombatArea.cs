using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 11: 웜뱃이 있을 수 있는 곳(빵집·광장). HUD와 웜뱃 그림은 이 면만 본다
    public interface IWombatArea
    {
        bool WombatPresent { get; }
        Vector2 WombatPosition { get; }
        Facing WombatFacing { get; }
        bool WombatMoving { get; }
        Interactable? Target { get; }
        // 지금 할 수 있는 첫 manual 행동(버튼). 없으면 null
        ActionRecord TargetAction { get; }

        event Action TargetChanged;

        void SetWombatInput(Vector2 input);
        // 버튼. 시트를 여는 행동(open·dig)은 화면 몫이라 false
        bool TryInteract();
    }
}

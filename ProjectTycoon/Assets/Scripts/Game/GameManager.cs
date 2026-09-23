using System;
using System.Collections.Generic;
using UnityEngine;
using GameKit.Singleton;
using GameKit.Tables;
using ZooTycoon.Core;
using ZooTycoon.Data;

namespace ZooTycoon.Game
{
    // 규칙 예외: 게임 상태·서비스를 씬 사이에서 유지하는 싱글턴(설계 04 D2). 게임 규칙은 Core 서비스에 있고 여기서는 생성·보관·틱 호출만 한다
    public sealed class GameManager : MonoSingleton<GameManager>
    {
        public GameTables Tables { get; private set; }
        public ZooState State { get; private set; }
        public ShopSim Shop { get; private set; }

        // 씬을 다시 열어도 상태는 한 번만 만든다(싱글턴이 씬 사이에서 살아남는 이유)
        public void Init()
        {
            if (State != null)
            {
                return;
            }

            Tables = LoadTables();
            IReadOnlyList<string> errors = TableValidator.Validate(Tables);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException($"테이블 검증 실패:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
            }

            State = ZooState.CreateNew(Tables.Config);
            Shop = new ShopSim(State, Tables, new SystemRandom());
        }

        // 손님 동선 설계 v0.2: 가게 시뮬은 매 프레임(손님 행동 트리·조이스틱 웜뱃이 멈칫하지 않게)
        private void Update()
        {
            Shop?.Tick(Time.deltaTime);
        }

        private static GameTables LoadTables()
        {
            TableManager tableManager = TableManager.Instance;

            return new GameTables(
                tableManager.Load<VisitorRecord>("visitors"),
                tableManager.Load<BreadRecord>("breads"),
                tableManager.Load<ShopUpgradeRecord>("shop_upgrades"),
                tableManager.Load<ActionRecord>("actions"),
                tableManager.Load<InteractableRecord>("interactables"),
                tableManager.Load<StringRecord>("strings"),
                tableManager.LoadConfig<GameConfig>("game_config"));
        }
    }
}

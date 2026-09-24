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
        public TableSet Tables { get; private set; }
        public ZooState State { get; private set; }
        public BakeryArea Bakery { get; private set; }
        // 설계 11: 빵집 + 굴 밖 광장, 웜뱃이 오가는 곳
        public Mall Mall { get; private set; }

        // 씬을 다시 열어도 상태는 한 번만 만든다(싱글턴이 씬 사이에서 살아남는 이유)
        public void Init()
        {
            if (State != null)
            {
                return;
            }

            Tables = TableManager.Instance.Tables;
            IReadOnlyList<string> errors = TableValidator.Validate(Tables);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException($"테이블 검증 실패:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
            }

            State = ZooState.CreateNew(Tables);
            SystemRandom random = new SystemRandom();
            Wombat wombat = new Wombat(Tables);
            Bakery = new BakeryArea(State, Tables, random, wombat);
            Mall = new Mall(Bakery, new PlazaArea(Tables, Bakery, random, wombat));
        }

        // 손님 동선 설계 v0.2: 가게 시뮬은 매 프레임(손님 행동 트리·조이스틱 웜뱃이 멈칫하지 않게). 설계 11: 빵집과 광장을 함께
        private void Update()
        {
            Mall?.Tick(Time.deltaTime);
        }
    }
}

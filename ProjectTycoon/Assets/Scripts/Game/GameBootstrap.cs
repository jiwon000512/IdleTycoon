using System;
using System.Collections.Generic;
using UnityEngine;
using GameKit.Tables;
using GameKit.UI;
using ZooTycoon.Core;
using ZooTycoon.Data;
using ZooTycoon.UI;

namespace ZooTycoon.Game
{
    // 컴포지션 루트. 서비스·프레젠터를 new 하는 유일한 곳(코드-규칙 3장).
    public sealed class GameBootstrap : MonoBehaviour
    {
        private TopBarPresenter m_topBarPresenter;

        private void Awake()
        {
            GameTables tables = LoadTables();
            IReadOnlyList<string> errors = TableValidator.Validate(tables);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException($"테이블 검증 실패:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
            }

            ZooState state = ZooState.CreateNew(tables.Config);
            ZooLevelService zooLevelService = new ZooLevelService(tables, state);

            TopBarView topBarView = UIManager.Instance.Open<TopBarView>();
            m_topBarPresenter = new TopBarPresenter(topBarView, state, zooLevelService, tables);
        }

        private void OnDestroy()
        {
            m_topBarPresenter?.Dispose();
        }

        private static GameTables LoadTables()
        {
            TableManager tableManager = TableManager.Instance;

            return new GameTables(
                tableManager.Load<AnimalRecord>("animals"),
                tableManager.Load<GradeRecord>("grades"),
                tableManager.Load<ZooLevelRecord>("zoo_levels"),
                tableManager.Load<StringRecord>("strings"),
                tableManager.LoadConfig<GameConfig>("game_config"));
        }
    }
}

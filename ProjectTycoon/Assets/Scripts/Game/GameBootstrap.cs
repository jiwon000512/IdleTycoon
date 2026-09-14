using System;
using System.Collections.Generic;
using UnityEngine;
using GameKit.Tables;
using GameKit.UI;
using ZooTycoon.Core;
using ZooTycoon.Data;
using ZooTycoon.UI;
using ZooTycoon.World;

namespace ZooTycoon.Game
{
    // 컴포지션 루트. 서비스·프레젠터를 new 하는 유일한 곳(코드-규칙 3장).
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private WorldView m_worldView;

        private TopBarPresenter m_topBarPresenter;
        private WorldPresenter m_worldPresenter;
        private GachaButtonPresenter m_gachaButtonPresenter;
        private GachaResultPresenter m_gachaResultPresenter;

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
            GachaService gachaService = new GachaService(tables, state, new SystemRandom());

            TopBarView topBarView = UIManager.Instance.Open<TopBarView>();
            GachaButtonView gachaButtonView = UIManager.Instance.Open<GachaButtonView>();

            m_topBarPresenter = new TopBarPresenter(topBarView, state, zooLevelService, tables);
            m_worldPresenter = new WorldPresenter(m_worldView, state, tables);
            m_gachaButtonPresenter = new GachaButtonPresenter(gachaButtonView, gachaService, state, tables);
            m_gachaResultPresenter = new GachaResultPresenter(gachaService, tables);
        }

        private void OnDestroy()
        {
            m_topBarPresenter?.Dispose();
            m_worldPresenter?.Dispose();
            m_gachaButtonPresenter?.Dispose();
            m_gachaResultPresenter?.Dispose();
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

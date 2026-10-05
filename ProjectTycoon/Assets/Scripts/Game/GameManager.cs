using System;
using System.Collections.Generic;
using UnityEngine;
using GameKit.Audio;
using GameKit.Events;
using GameKit.Saving;
using GameKit.Singleton;
using GameKit.Tables;
using ZooTycoon.Core;
using ZooTycoon.Data;

namespace ZooTycoon.Game
{
    // 규칙 예외: 게임 상태·서비스를 씬 사이에서 유지하는 싱글턴(설계 04 D2). 게임 규칙은 Core 서비스에 있고 여기서는 생성·보관·틱 호출만 한다.
    // 설계 43: 저장 파일 한 장(DataManager "save")을 불러와 덮고 자리를 비운 동안을 정산한다. 저장은 autosaveSeconds마다 · 앱이 뒤로 갈 때 · 닫을 때
    public sealed class GameManager : MonoSingleton<GameManager>
    {
        private const string k_SaveKey = "save";

        private double m_autosaveSeconds;
        private double m_offlineMinSeconds;
        private double m_sinceSave;
        private DateTime m_pausedAt;
        private OfflineReport m_pending;
        // 치트 「저장 지우기」 뒤에는 닫을 때 다시 쓰지 않는다
        private bool m_saveOff;

        public TableSet Tables { get; private set; }
        // 설계 16: 도메인 사건 버스. 게임 상태와 같이 만들고 Core에는 생성자로 넘긴다
        public EventBus Bus { get; private set; }
        public ZooState State { get; private set; }
        // 설계 11: 빵집 + 굴 밖 광장 + 농장(설계 25), 웜뱃이 오가는 곳
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

            foreach (SoundTable sound in Tables.GetAll<SoundTable>())
            {
                SoundManager.Instance.Register(sound.Id, Resources.Load<AudioClip>(sound.Clip), (float)sound.Volume,
                    (float)sound.MinGap, (float)sound.ComboSeconds, (float)sound.PitchStep, (float)sound.PitchMax);
            }

            m_autosaveSeconds = Tables.Get<ConfigTable>(ConfigTable.k_AutosaveSeconds).Value;
            m_offlineMinSeconds = Tables.Get<ConfigTable>(ConfigTable.k_OfflineMinSeconds).Value;
            NewGame();
            Load();
        }

        // 불러올 때 정산한 것을 화면에 알린다(팝업이 다 만들어진 뒤 MainScene이 부른다)
        public void PublishPendingOffline()
        {
            if (m_pending != null)
            {
                Bus.Publish(new Events.OfflineSettled(m_pending));
                m_pending = null;
            }
        }

        // seconds 동안 자리를 비운 것으로 정산하고 팝업을 띄운다(앱이 뒤에서 돌아왔을 때 · 치트)
        public OfflineReport SettleOffline(double seconds)
        {
            OfflineReport report = Offline.Settle(State, Mall, Tables, seconds);

            if (Worth(report))
            {
                Bus.Publish(new Events.OfflineSettled(report));
            }

            return report;
        }

        public void Save()
        {
            if (State == null || m_saveOff)
            {
                return;
            }

            DataManager.Instance.Save(k_SaveKey, GameSave.Capture(State, Mall), GameSave.k_Version);
            m_sinceSave = 0d;
        }

        // 치트: 저장 파일을 지운다. 이번 플레이가 끝날 때까지 다시 쓰지 않는다(다음 플레이가 새 게임)
        public void DeleteSave()
        {
            DataManager.Instance.Delete(k_SaveKey);
            m_saveOff = true;
        }

        // 손님 동선 설계 v0.2: 가게 시뮬은 매 프레임(손님 행동 트리·조이스틱 웜뱃이 멈칫하지 않게). 설계 11: 빵집과 광장을 함께
        private void Update()
        {
            if (Mall == null)
            {
                return;
            }

            Mall.Tick(Time.deltaTime);
            m_sinceSave += Time.unscaledDeltaTime;

            if (m_sinceSave >= m_autosaveSeconds)
            {
                Save();
            }
        }

        // 폰: 뒤로 갈 때 저장하고, 돌아오면 그동안을 정산한다(멈춰 있던 동안 Update가 돌지 않았다)
        private void OnApplicationPause(bool paused)
        {
            if (State == null)
            {
                return;
            }

            if (paused)
            {
                Save();
                m_pausedAt = DateTime.UtcNow;
            }
            else if (m_pausedAt != default)
            {
                SettleOffline((DateTime.UtcNow - m_pausedAt).TotalSeconds);
                m_pausedAt = default;
            }
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        private void NewGame()
        {
            Bus = new EventBus();
            State = ZooState.CreateNew(Tables, Bus);
            SystemRandom random = new SystemRandom();
            Wombat wombat = new Wombat(Tables, State);
            BakeryArea bakery = new BakeryArea(State, Tables, random, wombat, Bus);
            Mall = new Mall(bakery, new PlazaArea(Tables, bakery, random, wombat, Bus), new FarmArea(Tables, random, wombat, Bus), Bus, new FishingArea(Tables, random, wombat, Bus));
        }

        // 깨진 파일 · 덮다가 실패(표가 바뀌어 맞지 않음)면 새 게임으로 시작하고 파일은 .bad로 남긴다(30초 뒤 덮어써 잃지 않게)
        private void Load()
        {
            if (!DataManager.Instance.TryLoad(k_SaveKey, out SaveFile<SaveData> file))
            {
                return;
            }

            try
            {
                GameSave.Apply(file.Data, State, Mall, Tables);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"저장 파일을 덮지 못해 새 게임으로 시작한다(save.bad.json에 남김): {e.Message}");
                DataManager.Instance.Backup(k_SaveKey);
                NewGame();
                return;
            }

            OfflineReport report = Offline.Settle(State, Mall, Tables, (DateTime.UtcNow - file.SavedAtUtc).TotalSeconds);
            m_pending = Worth(report) ? report : null;
        }

        // 팝업을 띄울 만한가: offlineMinSeconds 넘게 비웠고 번 것이 있다
        private bool Worth(OfflineReport report)
        {
            return report.Seconds >= m_offlineMinSeconds && (report.Coins > 0d || report.Items.Count > 0);
        }
    }
}

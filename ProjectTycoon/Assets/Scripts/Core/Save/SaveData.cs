using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 43: 저장 파일 한 장의 모양(GameKit SaveFile의 Data). 공개 필드만(Newtonsoft가 그대로 쓴다), id는 표 id, 좌표는 곳 좌표.
    // 저장하지 않는 것: 손님 · 줄 · 똥 · 웜뱃이 든 빵 · 진행 중인 평가 · 머물던 행상 · 점원의 딴짓
    public sealed class SaveData
    {
        public double Coins;
        public Dictionary<string, int> Items = new Dictionary<string, int>();
        // 걸린 축복 id(없으면 null) · 남은 초 · 쉬는 초
        public string Blessing;
        public double BlessingLeft;
        public double BlessingCooldown;
        // 유물 id → 별, 끼운 칸(빈 칸은 null), 주판 셈
        public Dictionary<string, int> Relics = new Dictionary<string, int>();
        public List<string> RelicSlots = new List<string>();
        public int RelicSales;
        // 가게 id → 별
        public Dictionary<string, int> Stars = new Dictionary<string, int>();
        public double UntilPayday;
        // 빵집 · 광장 · 농장 층마다 하나(곳 id)
        public List<AreaSave> Areas = new List<AreaSave>();
    }

    // 곳 하나. 곳마다 쓰는 칸만 채운다(빵집: 칸 · 사물 · 빵 · 평가, 농장: 열림 · 칸 · 밭 · 작물, 광장: 사물 · 행상, 낚시터: Fishing, 횟집: 열림 · 칸 · 사물)
    public sealed class AreaSave
    {
        public string Id;
        // 판 칸 [열, 줄]
        public List<int[]> Cells = new List<int[]>();
        public List<ThingSave> Things = new List<ThingSave>();
        public Dictionary<string, int> Stored = new Dictionary<string, int>();
        public Dictionary<string, int> Upgrades = new Dictionary<string, int>();
        public List<ClerkSave> Clerks = new List<ClerkSave>();
        public List<CandidateSave> Candidates = new List<CandidateSave>();
        // 연 빵(빵집) · 연 작물(농장 1층, 층 공용)
        public List<string> Unlocked = new List<string>();
        public double EvaluationCooldown;
        public bool Open;
        public List<PlotSave> Plots = new List<PlotSave>();
        public double MerchantUntil;
        public FishingSave Fishing;
    }

    // 설계 44 · 46: 낚시터. 말뚝(열림 · 대 · 등급, 말뚝 순서) · 단계 · 산 대 수 · 미끼 노점 업그레이드 단계 · 어종 기록(예전 저장의 든 대 · 3택 1 칸은 읽지 않는다).
    // 떠 있는 물고기 · 붙잡은 월척 · 물때 진행은 저장하지 않는다(다시 열면 물때 1부터)
    public sealed class FishingSave
    {
        public int Stage;
        public int Summons;
        public Dictionary<string, int> Upgrades = new Dictionary<string, int>();
        // 설계 45: 물길 판 횟수(없으면 0 = 처음 길이)
        public int Dug;
        public List<StakeSave> Stakes = new List<StakeSave>();
        public Dictionary<string, FishRecord> Log = new Dictionary<string, FishRecord>();
    }

    public sealed class StakeSave
    {
        public bool Open;
        public string Rod;
        public int Grade;
    }

    // 놓인 사물 하나(놓인 순서). 진열대는 빵 · 재고, 오븐은 빵 · 남은 초 · 다 구운 수 · 마지막 빵, 설계 47 수조는 회 id → 마리 수(없으면 null)
    public sealed class ThingSave
    {
        public string Kind;
        public float X;
        public float Y;
        public string Bread;
        public int Stock;
        public string LastBread;
        public double Remaining;
        public int Ready;
        public Dictionary<string, int> Fish;
    }

    // 점원 하나. Thing = 그 곳 놓인 사물 순서 번호(농장 작업대는 −1)
    public sealed class ClerkSave
    {
        public int Thing;
        public string Name;
        public int Skill;
        public int Wage;
        public string Look;
        public string Product;
    }

    public sealed class CandidateSave
    {
        public string Name;
        public int Skill;
        public string Look;
    }

    public sealed class PlotSave
    {
        public int Col;
        public int Row;
        public bool Tilled;
        public string Crop;
        public double Remaining;
        public bool Fertilized;
    }
}

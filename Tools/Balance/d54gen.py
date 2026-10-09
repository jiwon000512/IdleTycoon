# -*- coding: utf-8 -*-
# 설계 54 값 시안: python d54gen.py <출력 폴더> <d54.json(보상 없는 판 Z)> → <폴더>/<시안>.json(Program quest 명령이 읽는 꼴) + values.json(페이지 표)
# 퀘스트 걸음 = 그 장 1분 순수입 × N분(1~3장 튜토리얼은 Ntut분, 아래 floor), 큰 벽(1,000 이상) 바로 앞 걸음 = 벽 값 × front.
# 큰 벽 앞 걸음들의 합은 벽 값 × cap을 넘지 않는다(작은 목표는 보상으로 바로 사도 괜찮다). 상자 = 장 1분 순수입 × 분
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
DATA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "ProjectTycoon", "Assets", "Resources", "Data")

# 장마다 접속 1분 순수입: 보상 없는 퀘스트 손 판(d54.json의 Z)에서 읽어 다듬는다(10 단위, 장이 오를수록 줄지 않게, 못 잰 장은 다음 장 값)
RATE = {}
# 상자 7장: 사슬이 끝난 뒤(둘째 날~ 접속 1분 700~950)에도 이 값이라 올리되, 첫날 횟집 벽(10,000) 앞 셋째 상자(12분)가 벽의 40%를 넘지 않게
CHEST7 = 300
# 튜토리얼(1~3장, 처음 1분에 열 걸음)은 셋 다 0.25분어치(20): 처음 몇 분에 코인이 많으면 점원을 일찍 여럿 들여 ★1 평가가 늦고
# 첫 행상(1~4분)을 놓친다(100판: 걸음 40이면 행상 잡음 62%, 20이면 76%, 2026-10-09)
OPTIONS = {
    "A": {"name": "작게", "Ntut": 0.25, "N": 1, "front": 0.1, "cap": 0.3, "floor": 20, "chest": [2, 4, 8]},
    "B": {"name": "보통", "Ntut": 0.25, "N": 2, "front": 0.2, "cap": 0.4, "floor": 20, "chest": [3, 6, 12]},
    "C": {"name": "크게", "Ntut": 0.25, "N": 3, "front": 0.3, "cap": 0.5, "floor": 20, "chest": [5, 10, 20]},
}
# 걸음마다 덤 재료(모든 시안): ★1에 반짝돌 2(평가 통과 3과 합쳐 5 = 행상이 떠나기 전에 뽑기), 횟집 열기에 붕어 5(첫 회를 바로)
ITEMS = {"q11": ("gem", 2), "q24": ("fish_crucian", 5)}
# 오늘의 일 목표 수: 둘째 날부터 10분 접속 하위 20%(d54.py 미션)를 넘지 않게, 첫날 60분 접속으로도 닿게. 손으로 골라 하는 것(딸기 · 낚기 · 회)은 2분 안팎
MISSIONS = {"m_sell": 70, "m_bake": 30, "m_evaluate": 1, "m_wheat": 200, "m_strawberry": 10, "m_plant": 50, "m_bless": 1, "m_draw": 1,
            "m_catch": 5, "m_serve": 5, "m_clean": 2, "m_wake": 5}


def rows(name):
    return json.load(open(os.path.join(DATA, name + ".json"), encoding="utf-8"))["rows"]


def cost_of(step):
    """걸음이 코인을 쓰면 그 값(표에서), 아니면 0"""
    kind, param = step["kind"], step["param"]
    if kind == "dig":
        return rows("FarmFloorTable")[0]["digBaseCost"]
    if kind == "till":
        return rows("FarmFloorTable")[0]["tillCost"]
    if kind == "place":
        price = next(r for r in rows("InteractableTable") if r["id"] == param)["price"]
        return price["baseCost"] * price["growth"] ** (step["count"] - price["start"] - 1)
    if kind == "unlock":
        for table in ("BreadTable", "CropTable"):
            for r in rows(table):
                if r["id"] == param:
                    return r["unlockCost"]
    if kind == "seat":
        return rows("FishingStretchTable")[0]["seats"][0]["cost"]
    if kind == "open":
        for r in rows("FishingStretchTable"):
            if r["id"] == param:
                return r["rockCost"]
        return next(r for r in rows("RestaurantConfigTable") if r["id"] == param)["openCost"]
    return 0


def nice(x):
    for step, below in ((5, 50), (10, 200), (50, 1000), (100, 10000)):
        if x < below:
            return int(round(x / step) * step)
    return int(round(x / 500) * 500)


def build(opt):
    chain = rows("QuestTable")
    coins = {}
    for st in chain:
        n = opt["Ntut"] if st["chapter"] <= 3 else opt["N"]
        coins[st["id"]] = max(opt["floor"], nice(RATE[st["chapter"]] * n))
    # 큰 벽: 바로 앞 걸음을 벽 값 × front로 키우고, 앞 코인 걸음(그 보상은 산 뒤에 받는다)부터 벽 앞까지의 합이 cap을 넘으면 앞 걸음 덤부터 줄인다
    last = 0
    for i, st in enumerate(chain):
        c = cost_of(st)
        if c <= 0:
            continue
        if c >= 1000 and i > 0:
            seg = [s["id"] for s in chain[last:i]]
            front = seg[-1]
            coins[front] = max(coins[front], nice(c * opt["front"]))
            over = sum(coins[s] for s in seg) - c * opt["cap"]
            if over > 0:
                coins[front] = max(opt["floor"], nice(coins[front] - over))
        last = i
    return chain, coins


def measured(path):
    got = json.load(open(path, encoding="utf-8"))["Z"]["rates"]
    rate, last = {}, 0
    for ch in range(1, 8):
        v = next(got[str(c)]["perMin"] for c in range(ch, 8) if str(c) in got)
        last = max(last, int(round(v / 10.0) * 10))
        rate[ch] = last
    return rate


if __name__ == "__main__":
    out = sys.argv[1]
    RATE.update(measured(sys.argv[2]))
    CHEST_RATE = {**RATE, 7: CHEST7}
    os.makedirs(out, exist_ok=True)
    table = {"rate": RATE, "chestRate": CHEST_RATE, "options": OPTIONS, "missions": MISSIONS, "items": ITEMS, "steps": [], "coins": {}}
    for key, opt in OPTIONS.items():
        chain, coins = build(opt)
        p = {"quests": {sid: {"coins": v} for sid, v in coins.items()},
             "chestMinutes": opt["chest"], "perMinute": {str(k): v for k, v in CHEST_RATE.items()},
             "missions": {mid: {"count": n} for mid, n in MISSIONS.items()}}
        for sid, (item, n) in ITEMS.items():
            p["quests"][sid].update({"item": item, "itemCount": n})
        json.dump(p, open(os.path.join(out, key + ".json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        table["coins"][key] = coins
        print(key, opt["name"], "합", sum(coins.values()), [coins[s["id"]] for s in chain],
              "상자(장 1 · 4 · 7):", [[m * CHEST_RATE[ch] for m in opt["chest"]] for ch in (1, 4, 7)])
    table["steps"] = [{"id": s["id"], "chapter": s["chapter"], "kind": s["kind"], "param": s["param"], "count": s["count"], "cost": cost_of(s)} for s in chain]
    json.dump(table, open(os.path.join(out, "values.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)

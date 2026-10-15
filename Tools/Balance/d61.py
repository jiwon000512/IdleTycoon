# -*- coding: utf-8 -*-
# 설계 61 낚시터 섬 숫자 시안 비교(2026-10-14 밸런스방): quest<이름> 한 주 결과 셋(A · B · C)과 fish · seats 측정을 보고서 그래프용 숫자로 줄인다.
# 사용: python Tools/Balance/d61.py <결과 폴더(qa · qb · qc · m1 · mb · mc · m2가 든 폴더)> <d61.json>
import json
import os
import statistics
import sys

sys.stdout.reconfigure(encoding="utf-8")
ROOT = sys.argv[1]
OUT = sys.argv[2]
EARN = ("bakery", "tips", "restaurant", "seats", "crate")
STEPS = ["q18", "q19", "q22", "q20", "q21", "q24", "q25"]
BUYS = ["좌대 reed_island", "다리 rock_island", "좌대 rock_island", "좌대 rock_island#2", "다리 drift_island", "좌대 drift_island", "좌대 drift_island#2"]


def load(folder, key):
    return json.load(open(os.path.join(ROOT, folder, "rates.json"), encoding="utf-8"))[key]


def med(xs):
    xs = [x for x in xs if x is not None]
    return round(statistics.median(xs), 1) if xs else None


def online_at(run, clock_min):
    # 벽시계 분(첫날 0시 기준) → 그때까지 쌓인 접속 분
    acc = 0.0
    for s in run["sessions"]:
        if clock_min < s["startMin"]:
            return acc
        if clock_min <= s["startMin"] + s["minutes"]:
            return acc + (clock_min - s["startMin"])
        acc += s["minutes"]
    return acc


def buys(run):
    # 같은 이름이 둘이면 둘째에 #2를 붙인다(좌대 둘)
    seen, out = {}, []
    for p in run["purchases"]:
        if p["cost"] <= 0:
            continue
        n = seen.get(p["name"], 0) + 1
        seen[p["name"]] = n
        name = p["name"] if n == 1 else f"{p['name']}#{n}"
        out.append(dict(p, key=name, online=round(online_at(run, p["min"]), 1), day=int(p["min"] // 1440) + 1, hour=round((p["min"] % 1440) / 60, 1)))
    return out


def digest(runs):
    d = {"steps": {}, "buys": {}, "days": [], "seatShare": {}}
    for sid in STEPS:
        rows = [next((s for s in r["steps"] if s["id"] == sid), None) for r in runs]
        rows = [s for s in rows if s]
        d["steps"][sid] = {
            "endOnline": med([s["endOnline"] for s in rows]),
            "minutes": med([s["onlineMinutes"] for s in rows]),
            "waitCoins": med([s["waits"].get("코인", 0) for s in rows]),
            "cost": med([s["cost"] for s in rows]),
            "reached": len(rows),
        }
    for key in BUYS:
        hits = [next((b for b in buys(r) if b["key"] == key), None) for r in runs]
        got = [h for h in hits if h]
        d["buys"][key] = {
            "online": med([h["online"] for h in got]),
            "clock": med([h["min"] for h in got]),
            "day": med([h["day"] for h in got]),
            "cost": got[0]["cost"] if got else None,
            "reached": len(got),
        }
    # 날마다(씨앗 평균): 접속 중 번 것의 갈래 · 오프라인 · 좌대 몫
    for day in range(1, 8):
        tot = {k: 0.0 for k in EARN}
        off, wages, n = 0.0, 0.0, 0
        for r in runs:
            for s in r["sessions"]:
                if s["day"] != day:
                    continue
                n += 1
                c = s["counts"]
                for k in EARN:
                    tot[k] += c.get(k, 0)
                off += s["offlineCoins"]
                wages += c.get("wages", 0)
        k = len(runs)
        row = {"day": day, **{key: round(v / k) for key, v in tot.items()}, "offline": round(off / k), "wages": round(wages / k)}
        earned = sum(tot.values()) / k
        row["seatShare"] = round(tot["seats"] / k / earned, 2) if earned else 0
        d["days"].append(row)
    # 첫 접속(60분) 코인 선: 씨앗 1(30초마다) + 다음 목표 값
    first = runs[0]
    d["firstHour"] = [{"min": round(c["min"] - 540, 1), "coins": c["coins"], "next": c["next"], "nextCost": c["nextCost"], "chapter": c["chapter"]} for c in first["coins"] if c["min"] < 600]
    d["firstBuys"] = [{"min": round(b["min"] - 540, 1), "name": b["name"], "cost": b["cost"]} for b in buys(first) if b["min"] < 600]
    d["firstSteps"] = [{"id": s["id"], "start": s["startOnline"], "end": s["endOnline"], "wait": s["waits"]} for s in first["steps"] if s["session"] == 0]
    return d


result = {}
for name, folder in [("A", "qa"), ("B", "qb"), ("C", "qc")]:
    runs = load(folder, "quest" + name)
    result[name] = digest(runs)
    result[name]["fish"] = load({"A": "m1", "B": "mb", "C": "mc"}[name], "fish")
    result[name]["seats"] = load({"A": "m1", "B": "mb", "C": "mc"}[name], "seats")
result["ref"] = {"solo": load("m2", "solo"), "clerks": load("m2", "clerks"), "restaurant": load("m2", "restaurant")}
json.dump(result, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)

for name in "ABC":
    d = result[name]
    print(f"=== {name}")
    for sid, s in d["steps"].items():
        print(f"  {sid}: 끝 접속 {s['endOnline']}분 · 걸림 {s['minutes']}분 · 코인 기다림 {s['waitCoins']}분 · 값 {s['cost']} · {s['reached']}/5")
    for key, b in d["buys"].items():
        print(f"  {key}: 접속 {b['online']}분 · 벽시계 {b['clock']}분(day {b['day']}) · 값 {b['cost']} · {b['reached']}/5")
    for row in d["days"]:
        print(f"  day {row['day']}: 빵집 {row['bakery']} 팁 {row['tips']} 좌대 {row['seats']} 횟집 {row['restaurant']} 상자 {row['crate']} | 오프라인 {row['offline']} 월급 {row['wages']} | 좌대 몫 {row['seatShare']}")

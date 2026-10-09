# -*- coding: utf-8 -*-
# 설계 54 숫자 비교: python d54.py <rates.json> <변형 ...> → 같은 폴더 d54.json + 요약 출력
# 장마다 1분 순수입(사슬 중 · 사슬 뒤 날마다), 걸음마다 걸린 분 · 코인 기다림 · 넘김, 하루 수입과 보상 몫, 오늘의 일 후보를 10분 접속에 몇 번 했나
import json
import os
import statistics
import sys

sys.stdout.reconfigure(encoding="utf-8")
DATA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "ProjectTycoon", "Assets", "Resources", "Data")
EARN = ("bakery", "tips", "restaurant", "seats", "debris")


def q(xs, p):
    xs = sorted(x for x in xs if x is not None)
    if not xs:
        return None
    k = (len(xs) - 1) * p
    lo = int(k)
    hi = min(lo + 1, len(xs) - 1)
    return round(xs[lo] + (xs[hi] - xs[lo]) * (k - lo), 2)


def med(xs):
    return q(xs, 0.5)


def net(counts):
    return sum(counts.get(k, 0) for k in EARN) - counts.get("wages", 0)


def rates(runs):
    """장마다 접속 1분 순수입(30초 점 사이 순수입 ÷ 분, 판마다 합 ÷ 분의 가운데). 7장은 사슬 중 · 사슬 뒤 날마다 따로"""
    per = {}
    for run in runs:
        acc = {}
        pts = run["coins"]
        for a, b in zip(pts, pts[1:]):
            dt = b["min"] - a["min"]
            if dt > 0.6 or dt <= 0:
                continue
            key = str(b["chapter"]) if b["step"] < 25 else "after%d" % (int(b["min"] // 1440) + 1)
            e = acc.setdefault(key, [0.0, 0.0])
            e[0] += (b["earned"] - b["wages"]) - (a["earned"] - a["wages"])
            e[1] += dt
        for key, (coins, mins) in acc.items():
            if mins >= 1.0:
                per.setdefault(key, []).append((coins / mins, mins))
    return {k: {"perMin": med([r for r, _ in v]), "p25": q([r for r, _ in v], 0.25), "p75": q([r for r, _ in v], 0.75),
                "minutes": med([m for _, m in v]), "runs": len(v)} for k, v in sorted(per.items())}


def chain(runs):
    by, order = {}, []
    for run in runs:
        for st in run["steps"]:
            if st["id"] not in by:
                order.append(st["id"])
                by[st["id"]] = []
            by[st["id"]].append(st)
    out = []
    for sid in order:
        sts = by[sid]
        out.append({
            "id": sid, "chapter": sts[0]["chapter"], "kind": sts[0]["kind"], "param": sts[0]["param"], "reward": sts[0]["reward"],
            "reached": len(sts), "skips": sum(1 for s in sts if s["skipped"]),
            "start": med([s["startOnline"] for s in sts]), "end": med([s["endOnline"] for s in sts]),
            "endClock": med([s["endClock"] for s in sts]),
            "mins": med([s["onlineMinutes"] for s in sts]), "p80": q([s["onlineMinutes"] for s in sts], 0.8),
            "coinWait": med([s["waits"].get("코인", 0) for s in sts]), "coinWaitP80": q([s["waits"].get("코인", 0) for s in sts], 0.8),
            # 코인 걸음인데 기다림 없이 끝난 판(보상이 벽을 없앰)
            "free": sum(1 for s in sts if s["waits"].get("코인", 0) < 0.05),
        })
    return out


def days(runs):
    out = []
    for d in range(1, 8):
        rows = []
        for run in runs:
            ss = [s for s in run["sessions"] if s["day"] == d]
            if not ss:
                continue
            c = lambda k: sum(s["counts"].get(k, 0) for s in ss)
            rows.append({"online": sum(net(s["counts"]) for s in ss), "offline": sum(s["offlineCoins"] for s in ss),
                         "quest": c("questCoins"), "chest": c("chestCoins"), "points": ss[-1]["points"], "coinsEnd": ss[-1]["coinsEnd"]})
        out.append({"day": d, **{k: med([r[k] for r in rows]) for k in ("online", "offline", "quest", "chest", "points", "coinsEnd")}})
    return out


def missions(runs):
    """둘째 날부터 10분 접속마다 후보 행동 수. 할 수 있게 된 접속만(딸기 · 회 · 깨우기는 그 걸음을 끝낸 뒤)"""
    table = json.load(open(os.path.join(DATA, "MissionTable.json"), encoding="utf-8"))["rows"]
    gate = {"strawberry": "q16", "serve": "q24", "wake": "q10"}
    out = {}
    for m in table:
        key = "goal:" + m["kind"] + (":" + m["param"] if m["param"] else "")
        need = gate.get(m["param"]) or gate.get(m["kind"])
        xs = []
        for run in runs:
            ready = 0.0
            if need:
                ends = [s["endClock"] + 540 for s in run["steps"] if s["id"] == need]
                ready = ends[0] if ends else float("inf")
            for s in run["sessions"]:
                if s["day"] >= 2 and s["minutes"] == 10 and s["startMin"] >= ready:
                    xs.append(s["counts"].get(key, 0))
        out[m["id"]] = {"count": m["count"], "n": len(xs), "p20": q(xs, 0.2), "med": med(xs), "p80": q(xs, 0.8), "zero": sum(1 for x in xs if x == 0)}
    return out


def timeline(runs):
    """걸음 번호 → 끝난 접속 분 · 벽시계 분(판마다)"""
    return [[(st["endOnline"], st["endClock"]) for st in run["steps"]] for run in runs]


if __name__ == "__main__":
    path = sys.argv[1]
    data = json.load(open(path, encoding="utf-8"))
    out_path = os.path.join(os.path.dirname(path), "d54.json")
    result = json.load(open(out_path, encoding="utf-8")) if os.path.exists(out_path) else {}
    for v in sys.argv[2:]:
        runs = data[v]
        ends = [max(st["endClock"] for st in r["steps"]) if len(r["steps"]) == 25 else None for r in runs]
        r = {"n": len(runs), "ends": ends, "rates": rates(runs), "chain": chain(runs), "days": days(runs), "missions": missions(runs), "timeline": timeline(runs),
             "chests": [c for run in runs for c in run["chests"]]}
        result[v] = r
        done = sorted(e for e in ends if e is not None)
        print(f"== {v}: 끝낸 판 {len(done)}/{len(runs)} · 사슬 끝 가운데 {med(done)}분(9시부터) · 4분의 3 {q(done, 0.75)}")
        print("  장마다 1분:", {k: (x["perMin"], x["minutes"]) for k, x in r["rates"].items()})
        for st in r["chain"]:
            if st["mins"] >= 0.5 or st["coinWait"] > 0 or st["skips"]:
                print(f'  {st["id"]} 장{st["chapter"]} {st["kind"]:7} 보상 {st["reward"]:>6} 시작 {st["start"]:>6} 걸림 {st["mins"]}/{st["p80"]} 코인 기다림 {st["coinWait"]}/{st["coinWaitP80"]} 넘김 {st["skips"]} 공짜 {st["free"]}')
        for d in r["days"]:
            print(f'  {d["day"]}일 접속 {d["online"]} 오프라인 {d["offline"]} 퀘스트 {d["quest"]} 상자 {d["chest"]} 점수 {d["points"]} 끝 코인 {d["coinsEnd"]}')
        print("  미션(10분 접속 하위 20% · 가운데 · 0인 접속):", {k: (x["count"], x["p20"], x["med"], x["zero"], x["n"]) for k, x in r["missions"].items()})
    json.dump(result, open(out_path, "w", encoding="utf-8"), ensure_ascii=False)

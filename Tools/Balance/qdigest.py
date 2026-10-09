# -*- coding: utf-8 -*-
# 퀘스트 사슬 · 오늘의 일 판(Program quest*) 요약: python qdigest.py <rates.json> <키> [키 ...]
# 걸음마다 시작 접속 분 · 걸린 분 · 그때 1분 순수입(앞뒤 5분, 같은 접속 안) · 넘김, 장마다 1분 순수입, 하루 수입 · 미션 · 상자
import json
import statistics
import sys

sys.stdout.reconfigure(encoding="utf-8")


def med(xs):
    xs = [x for x in xs if x is not None]
    return round(statistics.median(xs), 2) if xs else None


def per_minute(run, online, half=2.5):
    """접속 분 online 앞뒤 half분(같은 접속 안)의 순수입(번 돈 - 월급) ÷ 분. 퀘스트 · 상자 보상은 빼고"""
    starts = []
    acc = 0.0
    for s in run["sessions"]:
        starts.append((acc, acc + s["minutes"], s["startMin"]))
        acc += s["minutes"]
    for i, (a, b, clock0) in enumerate(starts):
        if a - 1e-6 <= online <= b + 1e-6:
            lo, hi = max(a, online - half), min(b, online + half)
            if hi - lo < 2 * half:
                lo, hi = (a, min(b, a + 2 * half)) if online - a < half else (max(a, b - 2 * half), b)
            pts = [c for c in run["coins"] if clock0 <= c["min"] - 0.01 <= clock0 + (b - a)]
            def at(t):
                # 접속 안 누적값(30초 점), t = 접속 분
                best = None
                for c in pts:
                    if c["min"] - clock0 <= t + 0.26:
                        best = c
                return best
            p0, p1 = at(lo - a), at(hi - a)
            e0 = (p0["earned"] - p0["wages"]) if p0 and lo - a > 0.25 else 0.0
            if p1 is None:
                return None
            return round(((p1["earned"] - p1["wages"]) - e0) / max(0.5, hi - lo), 1)
    return None


def summarize(runs):
    out = {}
    # 걸음
    order = []
    by = {}
    for run in runs:
        for st in run["steps"]:
            if st["id"] not in by:
                order.append(st["id"])
                by[st["id"]] = []
            by[st["id"]].append((run, st))
    steps = []
    for sid in order:
        rows = by[sid]
        st0 = rows[0][1]
        steps.append({
            "id": sid, "chapter": st0["chapter"], "kind": st0["kind"], "param": st0["param"], "count": st0["count"],
            "reached": len(rows), "skips": sum(1 for _, st in rows if st["skipped"]),
            "startOnline": med([st["startOnline"] for _, st in rows]),
            "minutes": med([st["onlineMinutes"] for _, st in rows]),
            "maxMinutes": round(max(st["onlineMinutes"] for _, st in rows), 2),
            "endClock": statistics.median_low([st["endClock"] for _, st in rows]) if len(rows) == len(runs) else None,
            "perMin": med([per_minute(run, st["startOnline"]) for run, st in rows]),
            "waits": {k: med([st["waits"].get(k, 0) for _, st in rows]) for k in sorted({k for _, st in rows for k in st["waits"]})},
        })
    out["steps"] = steps
    out["chainEnd"] = [max((st["endClock"] for st in run["steps"]), default=None) if len(run["steps"]) == 25 else None for run in runs]
    # 접속마다: 장 · 1분 순수입 · 오프라인 · 보상
    sess = []
    for i in range(len(runs[0]["sessions"])):
        rows = [run["sessions"][i] for run in runs if i < len(run["sessions"])]
        def net(s):
            c = s["counts"]
            return (c.get("bakery", 0) + c.get("tips", 0) + c.get("restaurant", 0) + c.get("seats", 0) + c.get("debris", 0) - c.get("wages", 0)) / s["minutes"]
        sess.append({
            "i": i, "day": rows[0]["day"], "start": rows[0]["startMin"],
            "chapter": med([s["chapter"] for s in rows]),
            "netPerMin": med([net(s) for s in rows]),
            "offline": med([s["offlineCoins"] for s in rows]),
            "quest": med([s["counts"].get("questCoins", 0) for s in rows]),
            "chest": med([s["counts"].get("chestCoins", 0) for s in rows]),
            "points": med([s["points"] for s in rows]),
        })
    out["sessions"] = sess
    # 하루: 접속 순수입 + 오프라인, 미션(그날 마지막 접속), 상자
    days = []
    for d in sorted({s["day"] for s in runs[0]["sessions"]}):
        per = []
        for run in runs:
            ss = [s for s in run["sessions"] if s["day"] == d]
            c = lambda s, k: s["counts"].get(k, 0)
            online = sum(c(s, "bakery") + c(s, "tips") + c(s, "restaurant") + c(s, "seats") + c(s, "debris") - c(s, "wages") for s in ss)
            per.append({
                "online": online, "offline": sum(s["offlineCoins"] for s in ss),
                "quest": sum(c(s, "questCoins") for s in ss), "chest": sum(c(s, "chestCoins") for s in ss),
                "points": ss[-1]["points"], "chapterEnd": ss[-1]["chapter"],
                "missions": ss[-1]["missions"],
            })
        days.append({
            "day": d, "online": med([p["online"] for p in per]), "offline": med([p["offline"] for p in per]),
            "quest": med([p["quest"] for p in per]), "chest": med([p["chest"] for p in per]),
            "points": [p["points"] for p in per], "chapterEnd": [p["chapterEnd"] for p in per],
            "missions": [[(m["id"], m["Progress"], m["Count"]) for m in p["missions"]] for p in per],
        })
    out["days"] = days
    # 장마다 1분 순수입: 그 장에 있던 접속 분 가중(접속 안 30초 점 기준)
    chap = {}
    for run in runs:
        pts = run["coins"]
        for a, b in zip(pts, pts[1:]):
            if b["min"] - a["min"] > 0.6 or b["earned"] < a["earned"] - 1:
                continue
            chap.setdefault(b["chapter"], []).append(((b["earned"] - b["wages"]) - (a["earned"] - a["wages"])) / (b["min"] - a["min"]))
    out["chapters"] = {k: {"perMin": round(statistics.mean(v), 1), "median": med(v), "minutes": round(len(v) * 0.5 / len(runs), 1)} for k, v in sorted(chap.items())}
    return out


if __name__ == "__main__":
    data = json.load(open(sys.argv[1], encoding="utf-8"))
    result = {k: summarize(data[k]) for k in sys.argv[2:]}
    json.dump(result, open(sys.argv[1].replace("rates.json", "qdigest.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    for k, r in result.items():
        print("==", k, "사슬 끝(벽시계 분, 9시부터):", r["chainEnd"])
        for st in r["steps"]:
            print(f'  {st["id"]} {st["chapter"]} {st["kind"]:8} {str(st["param"]):10} x{st["count"]:<3} 도달 {st["reached"]} 넘김 {st["skips"]} 시작 {st["startOnline"]}분 걸림 {st["minutes"]} (최대 {st["maxMinutes"]}) 1분 {st["perMin"]} 기다림 {st["waits"]}')
        print("  장마다 1분 순수입:", r["chapters"])
        for d in r["days"]:
            print(f'  {d["day"]}일: 접속 {d["online"]} 오프라인 {d["offline"]} 퀘스트 {d["quest"]} 상자 {d["chest"]} 점수 {d["points"]} 장 {d["chapterEnd"]}')

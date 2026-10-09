# -*- coding: utf-8 -*-
# 퀘스트 판 변형 비교: python qcompare.py <rates.json> <변형 ...>
# 사슬을 다 끝낸 판 수 · 끝난 때(가운데 · 4분위) · 걸음마다 걸린 접속 분(가운데 · 80%) · 날마다 수입과 보상 몫
import json
import statistics
import sys

sys.stdout.reconfigure(encoding="utf-8")


def clock(m):
    """9시부터 분 → 'n일 hh:mm'"""
    if m is None:
        return "못 끝냄"
    t = 540 + m
    day, rest = divmod(t, 1440)
    return f"{int(day) + 1}일 {int(rest // 60):02d}:{int(rest % 60):02d}"


def q(xs, p):
    xs = sorted(xs)
    if not xs:
        return None
    i = min(len(xs) - 1, max(0, int(round(p * (len(xs) - 1)))))
    return xs[i]


def summarize(runs):
    n = len(runs)
    ends = [max(st["endClock"] for st in r["steps"]) if len(r["steps"]) == 25 else None for r in runs]
    done = [e for e in ends if e is not None]
    # 못 끝낸 판은 무한대로 두고 가운데 값
    inf = 10 ** 9
    allends = sorted(e if e is not None else inf for e in ends)
    med = allends[n // 2] if allends[n // 2] < inf else None
    p75 = allends[int(0.75 * (n - 1))] if allends[int(0.75 * (n - 1))] < inf else None
    steps = {}
    for r in runs:
        for st in r["steps"]:
            steps.setdefault(st["id"], []).append(st)
    per_step = {}
    for sid, sts in steps.items():
        mins = [s["onlineMinutes"] for s in sts]
        per_step[sid] = {"reached": len(sts), "skips": sum(s["skipped"] for s in sts), "med": round(statistics.median(mins), 2), "p80": round(q(mins, 0.8), 2),
                         "start": round(statistics.median(s["startOnline"] for s in sts), 1)}
    days = []
    for d in range(1, 8):
        rows = []
        for r in runs:
            ss = [s for s in r["sessions"] if s["day"] == d]
            c = lambda s, k: s["counts"].get(k, 0)
            online = sum(c(s, "bakery") + c(s, "tips") + c(s, "restaurant") + c(s, "seats") + c(s, "debris") - c(s, "wages") for s in ss)
            off = sum(s["offlineCoins"] for s in ss)
            rows.append((online, off, sum(c(s, "questCoins") for s in ss), sum(c(s, "chestCoins") for s in ss), ss[-1]["points"]))
        days.append({"day": d, "online": round(statistics.median(x[0] for x in rows)), "offline": round(statistics.median(x[1] for x in rows)),
                     "quest": round(statistics.median(x[2] for x in rows)), "chest": round(statistics.median(x[3] for x in rows)),
                     "points": round(statistics.mean(x[4] for x in rows), 1)})
    return {"n": n, "done": len(done), "endMedian": med, "endP75": p75, "ends": ends, "steps": per_step, "days": days}


if __name__ == "__main__":
    data = json.load(open(sys.argv[1], encoding="utf-8"))
    out = {}
    for v in sys.argv[2:]:
        r = summarize(data[v])
        out[v] = r
        print(f"== {v}: 끝낸 판 {r['done']}/{r['n']} · 끝난 때 가운데 {clock(r['endMedian'])} · 4분의 3 {clock(r['endP75'])}")
        print("   걸음(가운데/80% 분):", " ".join(f"{sid}:{s['med']}/{s['p80']}" for sid, s in sorted(r["steps"].items(), key=lambda kv: kv[1]["start"]) if s["med"] >= 1 or s["p80"] >= 3))
        for d in r["days"][:4]:
            print(f"   {d['day']}일 접속 {d['online']} 오프라인 {d['offline']} 퀘스트 {d['quest']} 상자 {d['chest']} 점수 {d['points']}")
    json.dump(out, open(sys.argv[1].replace("rates.json", "qcompare.json"), "w", encoding="utf-8"), ensure_ascii=False)

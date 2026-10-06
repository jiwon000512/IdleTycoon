# 아트방 → 프로그래밍방 넘김 목록(.claude/state/handoff.json, 저장소 밖). 메시지는 알림, 기록은 이 파일.
# 사용: python3 Tools/handoff.py add <id> "<메모: 크기 · 피벗 · 끼울 자리>" <파일 또는 glob ...>   (아트방, 등록 뒤)
#       python3 Tools/handoff.py list            남은 항목(받음 · 붙임)
#       python3 Tools/handoff.py done <id>       프로그래밍방이 붙였다(커밋 때까지 남는다)
#       python3 Tools/handoff.py files           붙인 항목의 파일(커밋 묶음용) · clear  붙인 항목을 지운다(커밋 뒤)
import datetime
import glob
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATH = os.path.join(ROOT, ".claude", "state", "handoff.json")


def load():
    try:
        return json.load(open(PATH, encoding="utf-8"))
    except (OSError, ValueError):
        return []


def save(items):
    os.makedirs(os.path.dirname(PATH), exist_ok=True)
    json.dump(items, open(PATH, "w", encoding="utf-8"), ensure_ascii=False, indent=1)


def expand(patterns):
    paths = []
    for pattern in patterns:
        hits = glob.glob(os.path.join(ROOT, pattern), recursive=True)
        paths += [os.path.relpath(h, ROOT).replace("\\", "/") for h in hits] or [pattern]
    return sorted(set(paths + [p + ".meta" for p in paths if os.path.exists(os.path.join(ROOT, p + ".meta"))]))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    cmd, args = (sys.argv[1] if len(sys.argv) > 1 else "list"), sys.argv[2:]
    items = load()
    if cmd == "add":
        items = [i for i in items if i["id"] != args[0]]
        items.append({"id": args[0], "note": args[1], "files": expand(args[2:]), "state": "받음", "at": datetime.datetime.now().strftime("%m-%d %H:%M")})
        save(items)
        print("넘김 %s: 파일 %d개" % (args[0], len(items[-1]["files"])))
    elif cmd == "done":
        for i in items:
            if i["id"] == args[0]:
                i["state"] = "붙임"
        save(items)
    elif cmd == "files":
        print("\n".join(f for i in items if i["state"] == "붙임" for f in i["files"]))
    elif cmd == "clear":
        save([i for i in items if i["state"] != "붙임"])
    else:
        for i in items:
            print("[%s] %s (%s, 파일 %d) %s" % (i["state"], i["id"], i["at"], len(i["files"]), i["note"]))

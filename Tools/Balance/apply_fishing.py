# -*- coding: utf-8 -*-
# 낚시터 시안(Proposal JSON, Program.cs Proposal 꼴)을 진짜 표에 값만 넣는다(밸런스방 2026-10-14, 설계 61).
# 칸 구조 · id · version · 줄바꿈은 그대로 두고 그 줄의 숫자만 바꾼다. 끝에 notes 한 줄을 더할 수 있다.
# 사용: python Tools/Balance/apply_fishing.py <시안.json> ["notes에 더할 한 줄"]. 시안 JSON의 "notes": {"islands" · "fishing" · "fishSizes": 한 줄}이 있으면 표마다 그 줄을 쓴다
import json
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")
DATA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "ProjectTycoon", "Assets", "Resources", "Data")
proposal = json.load(open(sys.argv[1], encoding="utf-8"))
note = sys.argv[2] if len(sys.argv) > 2 else None
notes = proposal.get("notes", {})


def read(name):
    raw = open(os.path.join(DATA, name), "rb").read()
    return raw.decode("utf-8"), b"\r\n" in raw


def write(name, text, crlf):
    if crlf:
        text = text.replace("\r\n", "\n").replace("\n", "\r\n")
    open(os.path.join(DATA, name), "wb").write(text.encode("utf-8"))
    json.loads(text)  # 유효한 JSON인지
    print("wrote", name)


def sub_once(text, pattern, repl, what):
    new, n = re.subn(pattern, repl, text, count=1)
    assert n == 1, f"{what}: 자리를 찾지 못함"
    return new


def fish_array(spec):
    return "[" + ", ".join(f'{{ "id": "{fid}", "chance": {chance} }}' for fid, chance in spec.items()) + "]"


def add_note(text, line):
    # notes 배열의 마지막 항목 뒤에 한 줄
    m = re.search(r'("notes": \[\n(?:    "[^\n]*\n)*?    "[^\n]*)(\n  \],)', text)
    assert m, "notes 자리를 찾지 못함"
    return text[: m.end(1)] + ',\n    "' + line.replace('"', "'") + '"' + text[m.end(1):]


# ---------- FishingIslandTable ----------
islands = proposal.get("islands", {})
if islands:
    text, crlf = read("FishingIslandTable.json")
    for island, v in islands.items():
        # 그 섬 행 블록(다음 행의 시작 전까지)
        start = text.index(f'"id": "{island}"')
        nxt = text.find("\n    {\n      \"id\": \"", start)
        end = nxt if nxt > 0 else len(text)
        block = text[start:end]
        if "pay" in v:
            block = sub_once(block, r'"pay": [0-9.]+', f'"pay": {v["pay"]}', f"{island} pay")
        if "bridge" in v:
            block = sub_once(block, r'("bridge": \{[^\n]*?"cost": )[0-9.]+', lambda m: m.group(1) + str(v["bridge"]), f"{island} bridge cost")
        if "bridgeFish" in v:
            block = sub_once(block, r'("bridge": \{[^\n]*?"fish": )\[[^\n]*\]( \},)', lambda m: m.group(1) + fish_array(v["bridgeFish"]) + m.group(2), f"{island} bridge fish")
        if "fish" in v:
            block = sub_once(block, r'\n      "fish": \[[^\n]*\],', "\n      \"fish\": " + fish_array(v["fish"]) + ",", f"{island} fish")
        if "seats" in v:
            m = re.search(r'"seats": \[[^\n]*\]', block)
            assert m, f"{island} seats 자리를 찾지 못함"
            costs = iter(v["seats"])
            seats = re.sub(r'"cost": [0-9.]+', lambda _: f'"cost": {next(costs)}', m.group(0))
            block = block[: m.start()] + seats + block[m.end():]
        text = text[:start] + block + text[end:]
    if notes.get("islands", note):
        text = add_note(text, notes.get("islands", note))
    write("FishingIslandTable.json", text, crlf)

# ---------- FishingConfigTable ----------
fishing = proposal.get("fishing", {})
if fishing:
    text, crlf = read("FishingConfigTable.json")
    for key, value in fishing.items():
        text = sub_once(text, rf'"{key}": [0-9.]+', f'"{key}": {value}', f"fishing {key}")
    if notes.get("fishing", note):
        text = add_note(text, notes.get("fishing", note))
    write("FishingConfigTable.json", text, crlf)

# ---------- FishTable(명물 무게) ----------
sizes = proposal.get("fishSizes", {})
if sizes:
    text, crlf = read("FishTable.json")
    for fid, rows in sizes.items():
        assert len(rows) == 1, "크기 한 단계만 지원"
        start = text.index(f'"id": "{fid}"')
        block = text[start:]
        block = sub_once(block, r'"min": [0-9.]+', f'"min": {rows[0][0]}', f"{fid} min")
        block = sub_once(block, r'"max": [0-9.]+', f'"max": {rows[0][1]}', f"{fid} max")
        text = text[:start] + block
    if notes.get("fishSizes", note):
        text = add_note(text, notes.get("fishSizes", note))
    write("FishTable.json", text, crlf)

# ---------- 확인: 넣은 값이 표에서 그대로 읽히나 ----------
table = json.load(open(os.path.join(DATA, "FishingIslandTable.json"), encoding="utf-8"))
for island, v in islands.items():
    row = next(r for r in table["rows"] if r["id"] == island)
    if "pay" in v:
        assert row["pay"] == v["pay"]
    if "bridge" in v:
        assert row["bridge"]["cost"] == v["bridge"]
    if "seats" in v:
        assert [s["cost"] for s in row["seats"]] == v["seats"]
    if "fish" in v:
        assert [(f["id"], f["chance"]) for f in row["fish"]] == list(v["fish"].items())
config = json.load(open(os.path.join(DATA, "FishingConfigTable.json"), encoding="utf-8"))["rows"][0]
for key, value in fishing.items():
    assert config[key] == value, key
print("확인 끝")

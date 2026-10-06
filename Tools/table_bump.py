# 표 버전 올리기(데이터 테이블 규칙 6장): JSON version +1 · notes 한 줄, 테스트 TestCase, 규칙 문서 버전 · 변경 이력 한 줄을 한 번에.
# 사용: python3 Tools/table_bump.py "<까닭(설계 번호 포함)>" <Table> [<Table> ...]
# 이력 줄은 뼈대다(무엇이 바뀌었는지는 손으로 채운다). 줄바꿈은 파일 원래 것을 지킨다
import datetime
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "ProjectTycoon", "Assets", "Resources", "Data")
TESTS = os.path.join(ROOT, "ProjectTycoon", "Assets", "Tests", "EditMode", "TableValidatorTests.cs")
RULES = os.path.join(ROOT, "기획", "데이터-테이블-규칙.md")
TODAY = datetime.date.today().isoformat()


def read(path):
    with open(path, encoding="utf-8-sig", newline="") as f:
        return f.read()


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def bump_json(table, reason):
    path = os.path.join(DATA, table + ".json")
    text = read(path)
    data = json.loads(text)
    version = data["version"] + 1
    text = re.sub(r'("version":\s*)\d+', lambda m: m.group(1) + str(version), text, count=1)
    last = json.dumps(data["notes"][-1], ensure_ascii=False)
    at = text.rindex(last) + len(last)
    indent = re.search(r"\n([ \t]*)" + re.escape(last), text).group(1)
    newline = "\r\n" if "\r\n" in text else "\n"
    note = json.dumps("version %d (%s): %s" % (version, TODAY, reason), ensure_ascii=False)
    write(path, text[:at] + "," + newline + indent + note + text[at:])
    return version


def bump_test(table, version):
    text = read(TESTS)
    pattern = r'\[TestCase\("%s", %d\)\]' % (table, version - 1)
    if not re.search(pattern, text):
        print("테스트에 TestCase(\"%s\", %d)가 없다. 손으로 넣는다" % (table, version - 1))
        return
    write(TESTS, re.sub(pattern, '[TestCase("%s", %d)]' % (table, version), text))


def bump_rules(reason, bumped):
    text = read(RULES)
    m = re.search(r"버전 v1\.(\d+) · \S+", text)
    rule = int(m.group(1)) + 1
    text = text[:m.start()] + "버전 v1.%d · %s" % (rule, TODAY) + text[m.end():]
    newline = "\r\n" if "\r\n" in text else "\n"
    head = "## 변경 이력" + newline + newline
    line = "- v1.%d (%s) %s: %s." % (rule, TODAY, reason, " · ".join("`%s` version %d" % b for b in bumped))
    write(RULES, text.replace(head, head + line + newline, 1))
    return rule


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    if len(sys.argv) < 3:
        sys.exit(__doc__ or "사용: table_bump.py \"<까닭>\" <Table> [...]")
    reason, tables = sys.argv[1], sys.argv[2:]
    bumped = []
    for table in tables:
        version = bump_json(table, reason)
        bump_test(table, version)
        bumped.append((table, version))
    rule = bump_rules(reason, bumped)
    print("규칙 v1.%d · %s" % (rule, ", ".join("%s v%d" % b for b in bumped)))

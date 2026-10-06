# 포트폴리오 5장 숫자: 커밋 · C# 파일(폴더별) · EditMode 테스트 케이스 · JSON 표(행 · 버전). 표 칸에 그대로 붙인다
# 사용: python3 Tools/stats.py
import glob
import json
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "ProjectTycoon", "Assets")
GAMEKIT_TESTS = os.path.join(ROOT, "..", "UnityGameKit", "Tests")


def tests(folder):
    count = 0
    for path in glob.glob(os.path.join(folder, "**", "*.cs"), recursive=True):
        text = open(path, encoding="utf-8-sig").read()
        count += len(re.findall(r"\[Test\]", text)) + len(re.findall(r"\[TestCase\(", text))
    return count


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    commits = subprocess.run(["git", "-C", ROOT, "rev-list", "--count", "HEAD"], capture_output=True, text=True).stdout.strip()
    print("| 커밋 | %s |" % commits)
    folders = ["Core", "Data", "Game", "UI", "World", "Editor"]
    files = ["%s %d" % (f, len(glob.glob(os.path.join(ASSETS, "Scripts", f, "**", "*.cs"), recursive=True))) for f in folders]
    files.append("Tests %d" % len(glob.glob(os.path.join(ASSETS, "Tests", "**", "*.cs"), recursive=True)))
    print("| C# 파일 | %s |" % " · ".join(files))
    ours, kit = tests(os.path.join(ASSETS, "Tests")), tests(GAMEKIT_TESTS)
    print("| EditMode 테스트 케이스 | %d (GameKit %d 포함) |" % (ours + kit, kit))
    tables = []
    for path in sorted(glob.glob(os.path.join(ASSETS, "Resources", "Data", "*.json")), key=os.path.getmtime, reverse=True):
        data = json.load(open(path, encoding="utf-8-sig"))
        rows = data.get("rows", [])
        tables.append("%s %d행 v%d" % (data.get("table", os.path.basename(path)[:-5]), len(rows), data.get("version", 0)))
    print("| JSON 테이블 | %d (%s) |" % (len(tables), ", ".join(tables)))

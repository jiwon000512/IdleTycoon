# 커밋 묶음 제안(workflow 커밋 절: 기능 · 문서를 나누고, GameKit은 따로). 바뀐 파일을 경로 규칙으로 나눠 보여 준다
#   아트 넘김: Tools/handoff.py에서 「붙임」인 항목의 파일 → 그 기능 커밋에 같이
#   문서: 기획/ · .claude/rules · .claude/skills · CLAUDE.md · *.md(Source~ README 제외)   도구: Tools/ · .claude/helpers · .claude/settings.json · .gitignore
#   기능: 나머지
# 사용: python3 Tools/commit_groups.py
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def group(path, art):
    if path in art:
        return "아트 넘김(기능에 같이)"
    if path.startswith(("Tools/", ".claude/helpers/", ".claude/settings.json", ".gitignore")):
        return "도구"
    if path.startswith(("기획/", ".claude/rules/", ".claude/skills/", "CLAUDE.md")) or (path.endswith(".md") and "Source~" not in path):
        return "문서"
    return "기능"


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    status = subprocess.run(["git", "-C", ROOT, "-c", "core.quotepath=false", "status", "--porcelain", "-uall"], capture_output=True, text=True, encoding="utf-8").stdout
    art = set(subprocess.run([sys.executable, os.path.join(ROOT, "Tools", "handoff.py"), "files"], capture_output=True, text=True, encoding="utf-8").stdout.split("\n"))
    groups = {}
    for line in status.splitlines():
        path = line[3:].strip('"')
        groups.setdefault(group(path, art), []).append(line[:2] + " " + path)
    for name in ("기능", "아트 넘김(기능에 같이)", "도구", "문서"):
        if name in groups:
            print("## %s (%d)" % (name, len(groups[name])))
            print("\n".join(groups[name]))

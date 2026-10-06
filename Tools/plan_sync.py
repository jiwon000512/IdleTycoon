# 기획서 아티팩트의 지금 판(Artifact read가 저장한 html)을 저장소 원본 기획/기획서.html과 맞춘다.
# 사용: python3 Tools/plan_sync.py <저장된 아티팩트 html>
# 게시 때 붙는 문서 뼈대(<!doctype …<body> · </body></html>)를 떼고 비교한다. 같으면 「같음」, 다르면 저장소 파일을 그 판으로 바꾼다(사용자가 웹에서 고친 것 — git diff로 본다)
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PLAN = os.path.join(ROOT, "기획", "기획서.html")

if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    text = open(sys.argv[1], encoding="utf-8").read()
    if "<body>" in text[:2000]:
        text = text[text.index("<body>") + len("<body>"):]
    text = text.rstrip()
    if text.endswith("</body></html>"):
        text = text[:-len("</body></html>")]
    text = text.strip("\n")
    if text == open(PLAN, encoding="utf-8").read():
        print("같음")
    else:
        open(PLAN, "w", encoding="utf-8", newline="").write(text)
        print("다름: 저장소 원본을 웹 판으로 바꿨다. git diff 기획/기획서.html 로 사용자가 고친 곳을 본다")

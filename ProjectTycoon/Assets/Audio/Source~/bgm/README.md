# 빵집 굴 BGM 시안 (ACE-Step 1.5)

- 생성기: ACE-Step 1.5(MIT, 생성물 상용 가능), 설치 위치 `C:\project\ACE-Step-1.5`(uv 가상환경, 모델은 첫 실행에 checkpoints로 내려받음)
- 실행: `cd C:\project\ACE-Step-1.5` → `%USERPROFILE%\.local\bin\uv.exe run python cli.py -c <이 폴더>\bakery_a.toml` (backend pt, 8GB GPU)
- 결과: `C:\project\ACE-Step-1.5\output\tycoon\bakery_*` (원본 wav는 저장소에 넣지 않는다. 고른 곡만 루프 다듬어 OGG로 `Resources/Audio/`에)
- 시안: A 카페 재즈(88 BPM F장조) · B 로파이 보사노바(96 BPM D장조) · C 칩튠 재즈 트리오(100 BPM B♭장조), 모두 악기곡 90초

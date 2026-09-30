# BGM 원본 (ACE-Step 1.5)

- 생성기: ACE-Step 1.5(MIT, 생성물 상용 가능), 설치 `C:\project\ACE-Step-1.5`(uv 가상환경, 모델 14GB는 checkpoints). 빠른 다운로드에 `hf_xet` 필요.
- 생성: `cd C:\project\ACE-Step-1.5` → `set PYTHONUTF8=1` → `%USERPROFILE%\.local\bin\uv.exe run python cli.py -c <이 폴더>\bakery_f.toml`
  (backend pt, `thinking = false` = 언어 모델 없이 음악 모델만. 8GB GPU에서 한 곡 약 1분. Windows 콘솔 cp949에서는 끝의 이모지 출력 때문에 PYTHONUTF8 필요)
- 후처리(ACE-Step venv의 python, Demucs 설치됨):
  1. `mellow.py <원곡 wav> <출력>` — 드럼 −12dB, 멜로디 어택 누르기·3kHz 위 −6dB·잔향 20%(은은하게)
  2. `loop.py <wav> <BPM> <출력 ogg> [최소 마디] [이음새 미리듣기]` — 박자 격자로 끝·시작이 비슷한 마디 쌍을 골라 한 박자 크로스페이드
  3. (선택) `lower.py <입력> <반음> <출력>` — 테이프 속도식으로 곡 전체를 낮춤
- 원본 WAV는 저장소 밖 `C:\project\ACE-Step-1.5\output\tycoon\`. 게임에는 루프 OGG만 `Resources/Audio/`에 넣는다.
- 결정(2026-09-28): 빵집 굴 = `bakery_f.toml`(말랑한 칩튠 가게, 108 BPM F장조) → mellow → loop 4~32마디(62.2초). 다른 시안 설정(a~k)은 기록으로 둔다. 프롬프트의 「drumless/no drums」는 잘 안 먹혀 드럼은 mellow.py(Demucs)로 줄인다.
- 결정(2026-09-28): 광장 = `plaza_d.toml`(등불 광장, 84 BPM F장조, 굴 속이라 낮은 음 중심) → `mellow.py <wav> <출력> 2.0 0.3 7000`(굴 울림) → loop 5~22마디(48.6초, 최소 16마디). 시안 plaza_a~f는 기록.
- 결정(2026-09-30, 에이전트 선택 · 사용자 피드백 대기): 농장 = `farm_c.toml`(굴 속 들판, 80 BPM D장조: 뜯는 기타 · 마림바 · 칼림바 · 리코더 · 낮은 첼로) → `mellow.py 2.0 0.3 7000` → loop 11~25마디(42.0초, 최소 12마디, 이음새 0.938). 8마디 24초(0.948) · 17마디 51초(0.920) · 24마디 72초(0.898)도 `output/tycoon/farm_c/loop_*.ogg`에 둠. 수치(`metrics.py`, mellow 기준): a 튀는 음 7.3dB · 무게중심 313Hz, b 14.1dB · 388Hz(칩튠이라 튐), c 6.3dB · 310Hz → c.

---
name: bgm
description: 배경음악(BGM)이나 효과음을 새로 만들거나 고칠 때 읽는다 — 로컬 ACE-Step 1.5로 곡 생성, 은은하게 후처리, 마디 루프, 들어 보는 비교 페이지, BgmTable 등록까지. 사용자가 원하는 곡 느낌도 여기 있다.
---

# BGM 만들기

원본 설정과 스크립트는 `ProjectTycoon/Assets/Audio/Source~/bgm/`(README에 확정 곡 기록). 생성기는 `C:\project\ACE-Step-1.5`(MIT, 생성물 상용 가능, 모델 14GB 받아 둠). 출력 WAV는 저장소 밖 `C:\project\ACE-Step-1.5\output\tycoon\`.

## 사용자가 원하는 느낌

- **우아한 카페 재즈는 반려됨.** 귀엽고 소박하게: 부드러운 칩튠(사각파·삼각파) + 실제 악기 조금(칼림바·마림바·우쿨렐레·뜯는 기타).
- **은은하게, 귀에 꽂히는 음 없이.** 박수·셰이커·하이햇 같은 박자 소리는 약하게.
- 방치형이라 오래 틀어 둔다: 단순하고 듬성듬성한 멜로디, 장조.
- **굴 속 공간은 낮은 음으로** 표현한다(낮은 패드·첼로·낮은 옥타브). 밝지만 따뜻하고 조용하게.
- 곳마다 다른 곡이되 한 세계로 들려야 한다.

## 순서

1. `bakery_f.toml`을 본보기로 `<곳>_<a..>.toml` 세 개를 쓴다(결이 다른 caption, bpm, keyscale, seed, `save_dir`). `thinking = false`, `backend = "pt"`.
2. 생성(한 곡 1분쯤):
   ```bash
   cd /c/project/ACE-Step-1.5
   PYTHONIOENCODING=utf-8 PYTHONUTF8=1 "$USERPROFILE/.local/bin/uv.exe" run python cli.py -c "C:/project/Tycoon/ProjectTycoon/Assets/Audio/Source~/bgm/<이름>.toml" > output/tycoon/log.txt 2>&1
   ```
3. 은은하게: `.venv/Scripts/python.exe <bgm>/mellow.py <원곡.wav> <출력 경로(확장자 없이)> [잔향 초] [잔향 비율] [고음 걷기 Hz]`
   - 기본(빵집): 인자 없음. 굴 속 울림(광장): `2.0 0.3 7000`.
   - 하는 일: Demucs로 나눠 드럼 −12dB, 멜로디 어택 누르기, 3kHz 위 −6dB, 잔향. WAV·OGG·MP3를 쓴다.
4. 비교 페이지: `<audio controls>`에 OGG와 MP3를 같이 건다. 확정된 다른 곳의 곡도 위에 넣어 이어 듣게 한다. 곡마다 프롬프트와 수치를 적는다.
5. 고르면 루프: `.venv/Scripts/python.exe <bgm>/loop.py <mellow.wav> <BPM> <출력.ogg> [최소 마디] [이음새 미리듣기.ogg]`
   - 이음새 유사도가 0.9 아래면 최소 마디를 바꿔 다시 찾고, 길이와 매끄러움이 엇갈리면 둘 다 들려주고 고르게 한다.
6. 등록: `Assets/Resources/Audio/bgm_<곳>.ogg`로 복사 → 임포트 설정 Streaming·Vorbis 0.7 → `BgmTable.json`에 행. 코드는 바꾸지 않는다(`AreaBgm`이 곳 이동 때 전환).
7. 플레이에서 `SoundManager`의 AudioSource 상태(clip·isPlaying·volume)로 확인하고, README에 확정 곡을 적는다.

## 알아 둘 것

- 에이전트는 소리를 들을 수 없다. 보고할 때 그 사실을 적고 수치로 확인한다: 3kHz 위 비율(고음), 중역 스펙트럼 증가의 99퍼센타일(튀는 음), 250Hz 아래 비율과 스펙트럼 무게중심(음역), Demucs 드럼 비중.
- 프롬프트의 「drumless / no drums」는 잘 듣지 않는다(드럼 비중 58~62%로 남음). 박자는 `mellow.py`로 줄인다.
- 곡 전체를 낮추려면 `lower.py <입력> <반음> <출력>`(테이프 속도식: 음정과 템포가 같이 내려간다).
- Windows 콘솔(cp949)에서는 cli.py 끝의 이모지 출력이 오류를 낸다 → `PYTHONUTF8=1`. 파일은 그 전에 저장된다.
- 모델을 다시 받아야 하면 `hf_xet`이 있어야 빠르다(`uv pip install hf_xet`).
- OGG는 1초씩 나눠 써야 한다(한 번에 쓰면 조용히 죽는다).

## 효과음

- `Assets/Audio/Source~/make_sfx.py`가 numpy로 합성해 `Resources/Audio/<id>.wav`에 쓴다(칩튠 결, 44.1kHz 모노). 도구: `sweep`(미끄러지는 음) · `note` · `noise`(띠 통과 잡음) · `seq`(이어 붙이기) · `soft`(첫소리 부드럽게) · `bell`.
- 표는 `SoundTable.json`(음량·최소 간격·연속 피치). 어느 사건에 나는지는 `World/WorldSound`(월드 사건)와 UI(`PressScale`·View). 재생은 어디서든 `SoundManager.Instance.Play(SoundTable.k_*)`.
- 새 효과음: `make_sfx.py`에 한 줄 → 표에 행 → `SoundTable.cs`에 `k_*`와 `Ids` → 사건에 연결. 표에 없는 id를 부르면 예외가 난다.
- 소리 하나만 고칠 때는 `make_sfx.py`의 그 줄만 바꿔 다시 돌린다(다른 소리는 그대로 나온다). 음량·간격만이면 표만 고친다.
- 2026-09-28의 17개는 에이전트가 1차로 판단해 넣었다. 사용자가 플레이로 듣고 하나씩 피드백한다. 방향은 BGM과 같다: 은은하게, 첫소리가 튀지 않게, 짧게.
- 확인: 플레이 중 `SoundManager`의 `m_cues`에서 `Last`가 찍힌 id를 읽으면 실제로 난 소리를 알 수 있다.
- 시안 미리듣기는 게임에서 나는 그대로 합친다: 연출 시간표에 맞춘 소리는 그 시각에 놓아 한 파일로(표의 연속 피치는 재생 속도로 흉내), 자주 나는 소리는 가장 잦은 간격으로 세 번. 시안은 `make_sfx.py`의 `OUT`만 scratchpad로 바꿔 돌려 Resources를 건드리지 않고 확인한다(상대 방이 에디터를 쓰는 동안에도 됨).

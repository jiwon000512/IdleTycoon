# -*- coding: utf-8 -*-
# BGM 루프 다듬기(2026-09-28): 박자 격자를 찾아 인트로·페이드아웃을 피한 구간 중
# 「끝 부근」과 「시작 부근」이 가장 비슷한 마디 쌍을 고르고, 이음새를 한 박자 동안 겹쳐 섞는다(등전력 크로스페이드).
# 실행(ACE-Step venv): .venv\Scripts\python.exe loop.py <입력 wav> <BPM> <출력 ogg> [최소 마디 수=24] [미리듣기 경로]
import sys
import numpy as np
import soundfile as sf

src, bpm, out = sys.argv[1], float(sys.argv[2]), sys.argv[3]
min_bars = int(sys.argv[4]) if len(sys.argv) > 4 else 24
preview = sys.argv[5] if len(sys.argv) > 5 else None
x, sr = sf.read(src, dtype='float32')
mono = x.mean(1)

# 온셋 세기: 짧은 창의 스펙트럼이 늘어난 양
hop, n = 512, 2048
win = np.hanning(n)
frames = np.stack([mono[i:i + n] * win for i in range(0, len(mono) - n, hop)])
spec = np.abs(np.fft.rfft(frames, axis=1))
onset = np.maximum(np.diff(np.log1p(spec), axis=0), 0).sum(1)
beat = 60 / bpm * sr / hop                     # 한 박자 = 프레임 수(소수)
best = max(np.arange(0, beat, 0.25), key=lambda o: onset[np.round(np.arange(o, len(onset) - 1, beat)).astype(int)].sum())
beat_frames = np.arange(best, len(onset) - 1, beat)
beat_samples = (beat_frames * hop).astype(int)

# 박자마다 음색(로그 대역 32개) 평균
edges = np.geomspace(60, 12000, 33)
f = np.fft.rfftfreq(n, 1 / sr)
bands = [m for m in ((f >= edges[i]) & (f < edges[i + 1]) for i in range(32)) if m.any()]   # 칸이 없는 저역 대역은 뺀다
feats = []
for i in range(len(beat_frames) - 1):
    a, b = int(beat_frames[i]), int(beat_frames[i + 1])
    s = spec[a:b].mean(0)
    v = np.log1p(np.array([s[m].mean() for m in bands]))
    feats.append(v / (np.linalg.norm(v) + 1e-9))
feats = np.array(feats)

# 페이드아웃 전까지만: 박자 음량이 곡 중앙값의 절반 아래로 떨어지는 곳 앞 한 마디
level = np.array([np.sqrt(np.mean(mono[beat_samples[i]:beat_samples[i + 1]] ** 2)) for i in range(len(beat_samples) - 1)])
usable = len(level)
for i in range(len(level) - 1, 0, -1):
    if level[i] >= 0.5 * np.median(level):
        usable = i - 4
        break

W = 8  # 비교 창(박자): 이음새 앞 6박 + 뒤 2박
scores = []
for s_bar in range(2, usable // 4):
    for e_bar in range(s_bar + min_bars, usable // 4 + 1):
        s, e = s_bar * 4, e_bar * 4
        if e + 2 > len(feats) or s - 6 < 0:
            continue
        sim = float((feats[e - 6:e + 2] * feats[s - 6:s + 2]).sum(1).mean())
        scores.append((sim, s_bar, e_bar))
sim, s_bar, e_bar = max(scores)
s0, e0 = beat_samples[s_bar * 4], beat_samples[e_bar * 4]
cf = int(beat_samples[1] - beat_samples[0])       # 한 박자

loop = x[s0:e0].copy()
t = np.linspace(0, np.pi / 2, cf)[:, None]
loop[:cf] = loop[:cf] * np.sin(t) + x[e0:e0 + cf] * np.cos(t)


def write_ogg(path, y):
    # libsndfile Vorbis는 한 번에 크게 쓰면 조용히 죽는다: 1초씩 나눠 쓴다
    with sf.SoundFile(path, 'w', sr, y.shape[1], format='OGG', subtype='VORBIS') as o:
        for i in range(0, len(y), sr):
            o.write(y[i:i + sr])


write_ogg(out, loop)
print('bars %d..%d (%d bars) %.2fs..%.2fs -> loop %.1fs, seam similarity %.3f' % (s_bar, e_bar, e_bar - s_bar, s0 / sr, e0 / sr, len(loop) / sr, sim))
if preview:
    tail = loop[-8 * sr:]
    write_ogg(preview, np.concatenate([tail, loop[:8 * sr]]))

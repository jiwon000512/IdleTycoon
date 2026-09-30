# BGM 시안을 수치로 비교한다(에이전트는 소리를 들을 수 없다, 스킬 bgm 「알아 둘 것」).
# 사용: python metrics.py <wav> [<wav> ...]  → 곡마다 RMS(dBFS) · 250Hz 아래 비율 · 3kHz 위 비율 · 스펙트럼 무게중심(Hz) · 튀는 음(프레임 에너지 증가의 99퍼센타일, dB)
import sys
import wave
import numpy as np


def load(path):
    with wave.open(path, 'rb') as w:
        rate = w.getframerate()
        ch = w.getnchannels()
        width = w.getsampwidth()
        raw = w.readframes(w.getnframes())
    if width == 2:
        x = np.frombuffer(raw, np.int16).astype(np.float64) / 32768.0
    elif width == 4:
        x = np.frombuffer(raw, np.int32).astype(np.float64) / 2147483648.0
    else:
        raise SystemExit('지원하지 않는 샘플 폭 ' + str(width))
    if ch > 1:
        x = x.reshape(-1, ch).mean(1)
    return rate, x


def metrics(path):
    rate, x = load(path)
    n = 4096
    hop = 2048
    frames = [x[i:i + n] for i in range(0, len(x) - n, hop)]
    win = np.hanning(n)
    spec = np.abs(np.fft.rfft(np.array(frames) * win, axis=1)) ** 2
    freqs = np.fft.rfftfreq(n, 1 / rate)
    total = spec.sum(1) + 1e-12
    low = spec[:, freqs < 250].sum(1) / total
    high = spec[:, freqs > 3000].sum(1) / total
    centroid = (spec * freqs).sum(1) / total
    energy = 10 * np.log10(total)
    rises = np.diff(energy)
    rms = 20 * np.log10(np.sqrt((x ** 2).mean()) + 1e-12)
    return dict(rms=rms, low=low.mean(), high=high.mean(), centroid=centroid.mean(), spike=np.percentile(rises, 99), seconds=len(x) / rate)


for path in sys.argv[1:]:
    m = metrics(path)
    print('%-60s rms %5.1f dB  low<250Hz %4.0f%%  high>3kHz %4.1f%%  centroid %4.0f Hz  spike99 %4.1f dB  %.0fs'
          % (path, m['rms'], m['low'] * 100, m['high'] * 100, m['centroid'], m['spike'], m['seconds']))

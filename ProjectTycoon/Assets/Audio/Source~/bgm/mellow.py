# -*- coding: utf-8 -*-
# 빵집 BGM 후처리(2026-09-28): ACE-Step 원곡을 은은하게 — 귀에 꽂히는 음 없이.
# Demucs(htdemucs)로 드럼·베이스·나머지로 나눈 뒤
#   드럼: −12dB + 5kHz 위 걷기(박수·하이햇이 튀지 않게)
#   나머지(멜로디·코드): 어택 누르기(빠른 컴프레서) + 3kHz 위 −6dB + 8kHz 위 걷기 + 작은 방 잔향 20%
#   베이스: 4kHz 위 걷기
# 를 다시 섞고, 버스 컴프레서로 크기를 고른 뒤 RMS −21dBFS·피크 0.7 안으로 맞춘다.
# 실행(ACE-Step venv): .venv\Scripts\python.exe mellow.py <원곡 wav> <출력 경로(확장자 없이)> [잔향 초=1.2] [잔향 비율=0.2] [고음 걷기 Hz=8000]
#   굴 속 광장(2026-09-28): 2.0 0.3 7000 — 더 길고 많은 울림, 고음 조금 더 걷기
import sys
import numpy as np
import soundfile as sf
import torch
from scipy.signal import butter, sosfilt, lfilter
from demucs.pretrained import get_model
from demucs.apply import apply_model


def lowpass(x, sr, hz):
    return sosfilt(butter(2, hz, 'low', fs=sr, output='sos'), x, axis=-1)


def treble_cut(x, sr, hz, db):
    # 하이 셸빙 근사: 고역만 뽑아 그만큼 빼기
    hi = sosfilt(butter(2, hz, 'high', fs=sr, output='sos'), x, axis=-1)
    return x + (10 ** (db / 20) - 1) * hi


def compress(x, sr, threshold_db, ratio, attack_ms, release_ms):
    # 좌우 합 음량을 따라가는 포락선으로 이득을 줄인다(어택이 빠를수록 음의 첫소리를 누른다)
    level = np.abs(x).max(axis=0)
    a_att = np.exp(-1 / (sr * attack_ms / 1000))
    a_rel = np.exp(-1 / (sr * release_ms / 1000))
    # 올라갈 때는 빠르게, 내려갈 때는 느리게: 두 번 걸러 근사
    up = lfilter([1 - a_att], [1, -a_att], level)
    env = np.maximum(up, lfilter([1 - a_rel], [1, -a_rel], level))
    thr = 10 ** (threshold_db / 20)
    gain = np.where(env > thr, (thr / np.maximum(env, 1e-9)) ** (1 - 1 / ratio), 1.0)
    return x * gain


def room(x, sr, seconds=1.2, wet=0.2, seed=7):
    rng = np.random.default_rng(seed)
    n = int(sr * seconds)
    t = np.arange(n) / sr
    ir = rng.standard_normal((2, n)) * np.exp(-t / (seconds / 5))
    ir = lowpass(ir, sr, 4000)
    ir /= np.sqrt((ir ** 2).sum(axis=1, keepdims=True))
    from scipy.signal import fftconvolve
    tail = np.stack([fftconvolve(x[c], ir[c])[:x.shape[1]] for c in range(2)])
    return (1 - wet) * x + wet * tail


def main(src_path, out_base, room_seconds=1.2, wet=0.2, top_hz=8000):
    x, sr = sf.read(src_path, dtype='float32')
    model = get_model('htdemucs')
    model.eval()
    dev = 'cuda' if torch.cuda.is_available() else 'cpu'
    model.to(dev)
    with torch.no_grad():
        stems = apply_model(model, torch.tensor(x.T)[None].to(dev), device=dev, split=True, overlap=0.25)[0].cpu().numpy().astype('float64')
    s = dict(zip(model.sources, stems))
    drums = lowpass(s['drums'] * 10 ** (-12 / 20), sr, 5000)
    bass = lowpass(s['bass'], sr, 4000)
    other = s['other'] + s['vocals']
    other = compress(other, sr, -24, 4, 2, 120)
    other = treble_cut(other, sr, 3000, -6)
    other = lowpass(other, sr, top_hz)
    other = room(other, sr, room_seconds, wet)
    mix = drums + bass + other
    mix = compress(mix, sr, -18, 2, 10, 250)
    rms = np.sqrt(np.mean(mix ** 2))
    mix *= 10 ** (-21 / 20) / rms
    peak = np.abs(mix).max()
    if peak > 0.7:
        mix *= 0.7 / peak
    y = mix.T.astype('float32')
    for fmt, sub, ext in (('WAV', 'PCM_16', 'wav'), ('OGG', 'VORBIS', 'ogg'), ('MP3', 'MPEG_LAYER_III', 'mp3')):
        with sf.SoundFile(f'{out_base}.{ext}', 'w', sr, 2, format=fmt, subtype=sub) as out:
            for i in range(0, len(y), sr):
                out.write(y[i:i + sr])
    crest = lambda a: 20 * np.log10(np.abs(a).max() / np.sqrt(np.mean(a ** 2)))
    print('%s crest %.1fdB -> %.1fdB' % (out_base.split('/')[-1], crest(x), crest(mix)), flush=True)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2], *[float(a) for a in sys.argv[3:6]])

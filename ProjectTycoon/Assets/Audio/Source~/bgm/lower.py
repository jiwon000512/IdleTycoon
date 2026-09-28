# -*- coding: utf-8 -*-
# 테이프를 느리게 트는 식으로 곡 전체를 n반음 낮춘다(음정과 템포가 같이 내려간다: 2^(n/12)배 길어짐).
# 음정만 바꾸는 방식(위상 보코더)보다 소리가 뭉개지지 않는다.
# 실행(ACE-Step venv): .venv\Scripts\python.exe lower.py <입력 오디오> <반음 수> <출력 경로(확장자 없이)>
import sys
from fractions import Fraction
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly

src, semitones, out_base = sys.argv[1], float(sys.argv[2]), sys.argv[3]
x, sr = sf.read(src, dtype='float32')
ratio = Fraction(2 ** (semitones / 12)).limit_denominator(1000)
y = resample_poly(x, ratio.numerator, ratio.denominator, axis=0).astype('float32')
y *= 0.7 / max(np.abs(y).max(), 1e-6) if np.abs(y).max() > 0.7 else 1.0
for fmt, sub, ext in (('OGG', 'VORBIS', 'ogg'), ('MP3', 'MPEG_LAYER_III', 'mp3')):
    with sf.SoundFile(f'{out_base}.{ext}', 'w', sr, 2, format=fmt, subtype=sub) as out:
        for i in range(0, len(y), sr):
            out.write(y[i:i + sr])
print(out_base.split('/')[-1], '%.1fs -> %.1fs' % (len(x) / sr, len(y) / sr))

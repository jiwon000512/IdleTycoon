# -*- coding: utf-8 -*-
# 설계 10: 효과음 3개를 칩튠 결로 합성한다(numpy). 2026-09-24 시안 3개씩 중 사용자 선택
#   ../../Resources/Audio/pay.wav        결제 짤랑: 금속 방울 배음(C7·E7·G7), 0.22초 (시안 B)
#   ../../Resources/Audio/oven_done.wav  굽기 완료 띵: G6 주방 타이머 종(배음 + 지수 감쇠), 0.8초 (시안 A)
#   ../../Resources/Audio/give_up.wav    화난 퇴장 뿌우~: 흔들리며 내려가는 삼각파, 0.45초 (시안 B)
# 실행은 Windows Python(numpy 있음): Python312/python.exe make_sfx.py
import os, wave
import numpy as np

R = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Resources', 'Audio')


def save(name, x, peak):
    k = int(R * 0.01)
    x = x.copy()
    x[-k:] *= np.linspace(1, 0, k)
    x = x / np.max(np.abs(x)) * peak
    with wave.open(os.path.join(OUT, name + '.wav'), 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(R)
        w.writeframes((x * 32767).astype('<i2').tobytes())


def bell(freq_amps, seconds, decay):
    t = np.arange(int(R * seconds)) / R
    return sum(a * np.sin(2 * np.pi * f * t) * np.exp(-t / d) for f, a, d in [(f, a, decay(f)) for f, a in freq_amps])


save('pay', bell(((2093, 1), (2637, 0.6), (3136 * 1.01, 0.35)), 0.22, lambda f: 0.06), 0.45)
save('oven_done', bell(((1568 * h, a) for h, a in ((1, 1), (2.76, 0.4), (5.4, 0.15))), 0.8, lambda f: 0.25 / (f / 1568) ** 0.5), 0.6)

n = int(R * 0.45)
x = np.arange(n) / n
freq = 440 * 2 ** (-x * 0.35) * (1 + 0.04 * np.sin(2 * np.pi * 4.05 * x))
tri = 4 * np.abs(np.cumsum(freq) / R % 1 - 0.5) - 1
save('give_up', tri * np.minimum(1, (1 - x) * 4) * np.minimum(1, x * 40), 0.55)

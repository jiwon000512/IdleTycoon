# -*- coding: utf-8 -*-
# 설계 10: 더미 효과음 3개를 합성한다(표준 라이브러리만). 실제 소리가 오면 같은 경로에 덮어쓴다.
#   ../../Resources/Audio/pay.wav        결제 짤랑: 높은 사인 두 번(E6 → A6), 0.18초
#   ../../Resources/Audio/oven_done.wav  굽기 완료 띵: C6 종소리(배음 + 지수 감쇠), 0.6초
#   ../../Resources/Audio/give_up.wav    화난 퇴장 흥: 내려가는 낮은 사각파 콧소리, 0.25초
import math, os, struct, wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Resources', 'Audio')


def save(name, samples):
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + '.wav'), 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, s)) * 32767)) for s in samples))


def tone(freq, seconds, decay, harmonics=((1, 1.0),)):
    n = int(RATE * seconds)
    return [sum(a * math.sin(2 * math.pi * freq * h * i / RATE) for h, a in harmonics) * math.exp(-decay * i / RATE) * min(1, i / 60) for i in range(n)]


pay = [0.45 * s for s in tone(1318.5, 0.07, 30, ((1, 1), (2, 0.3)))] + [0.45 * s for s in tone(1760, 0.11, 25, ((1, 1), (2, 0.3)))]
save('pay', pay)

save('oven_done', [0.4 * s for s in tone(1046.5, 0.6, 6, ((1, 1), (2.76, 0.35), (5.4, 0.12)))])

n = int(RATE * 0.25)
phase, huff = 0.0, []
for i in range(n):
    t = i / n
    phase += (300 - 130 * t) / RATE
    square = 1 if (phase % 1) < 0.5 else -1
    huff.append(0.25 * square * math.sin(math.pi * t) ** 0.5)
save('give_up', huff)

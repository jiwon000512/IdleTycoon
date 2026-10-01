# -*- coding: utf-8 -*-
# 효과음을 칩튠 결로 합성한다(numpy). 표는 Resources/Data/SoundTable.json, 어느 사건에 나는지는 World/WorldSound·UI.
# pay·oven_done은 2026-09-24 시안 3개씩 중 사용자 선택. 나머지 16개는 2026-09-28 1차(에이전트 판단, 플레이 피드백으로 하나씩 고친다):
#   은은하게 — 첫소리를 3ms로 부드럽게, 높은 배음은 적게, 길이는 짧게
#   ../../Resources/Audio/pay.wav        결제 짤랑: 금속 방울 배음(C7·E7·G7), 0.22초 (시안 B)
#   ../../Resources/Audio/oven_done.wav  굽기 완료 띵: G6 주방 타이머 종(배음 + 지수 감쇠), 0.8초 (시안 A)
#   (give_up.wav 화난 퇴장은 2026-09-25 삭제)
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


# ---------- 2026-09-28 1차 효과음 16개 ----------
def t_of(seconds):
    return np.arange(int(R * seconds)) / R


def soft(x, attack=0.003):
    # 첫소리를 부드럽게(귀에 꽂히지 않게)
    k = int(R * attack)
    x = x.copy()
    x[:k] *= np.linspace(0, 1, k)
    return x


def sweep(f0, f1, seconds, decay, shape='sine'):
    # f0 → f1로 미끄러지는 음. shape: sine · tri(삼각파, 칩튠 결) · square(부드러운 사각파)
    t = t_of(seconds)
    f = f0 * (f1 / f0) ** (t / seconds)
    ph = 2 * np.pi * np.cumsum(f) / R
    if shape == 'tri':
        w = 2 / np.pi * np.arcsin(np.sin(ph))
    elif shape == 'square':
        w = np.tanh(3 * np.sin(ph)) * 0.7
    else:
        w = np.sin(ph)
    return soft(w * np.exp(-t / decay))


def noise(seconds, lo, hi, seed):
    # 띠 통과 잡음(FFT로 lo~hi Hz만 남김)
    n = int(R * seconds)
    x = np.random.default_rng(seed).standard_normal(n)
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(n, 1 / R)
    X[(f < lo) | (f > hi)] = 0
    x = np.fft.irfft(X, n)
    return x / np.max(np.abs(x))


def seq(parts, gap):
    # 소리 조각을 gap초 간격으로 이어 붙인다(겹쳐 울림)
    n = int(R * gap * (len(parts) - 1)) + max(len(p) for p in parts)
    out = np.zeros(n)
    for i, p in enumerate(parts):
        a = int(R * gap * i)
        out[a:a + len(p)] += p
    return out


def note(freq, seconds, decay, shape='tri'):
    return sweep(freq, freq, seconds, decay, shape)


# UI
save('ui_tap', sweep(1100, 800, 0.05, 0.015), 0.4)
save('ui_open', sweep(420, 760, 0.13, 0.06, 'tri'), 0.4)
save('ui_close', sweep(700, 380, 0.11, 0.05, 'tri'), 0.4)
save('nego_good', seq([note(1319, 0.3, 0.12), note(1760, 0.4, 0.16)], 0.09), 0.5)
save('nego_bad', seq([note(262, 0.22, 0.1, 'square'), note(208, 0.3, 0.13, 'square')], 0.12), 0.45)

# 빵집
t = t_of(0.4)
save('bake_start', soft(noise(0.4, 300, 2500, 1) * np.minimum(t / 0.12, 1) * np.exp(-np.maximum(t - 0.12, 0) / 0.1), 0.01)
     + np.pad(sweep(140, 90, 0.2, 0.08), (0, int(R * 0.2))) * 0.8, 0.45)
save('take_out', sweep(520, 940, 0.12, 0.045, 'tri'), 0.45)
save('put', sweep(330, 190, 0.11, 0.04), 0.5)
save('pick', note(990, 0.08, 0.025), 0.35)
save('dig', seq([soft(noise(0.09, 150, 1800, 10 + i) * np.exp(-t_of(0.09) / 0.03), 0.002) for i in range(3)], 0.055)
     + np.pad(sweep(130, 70, 0.16, 0.06), (0, int(R * 0.04))) * 0.9, 0.55)
save('upgrade', seq([note(f, 0.3, 0.1) for f in (1047, 1319, 1568, 2093)], 0.07), 0.5)
save('place', seq([sweep(430, 380, 0.08, 0.025), sweep(660, 600, 0.07, 0.02) * 0.6], 0.035), 0.5)

# 계산 중 영수증이 한 칸 올라올 때마다: 출력기 톱니 소리(짧은 잡음 + 낮은 딸깍). 계산 1.5초에 15번 난다
save('receipt', soft(noise(0.035, 900, 3200, 21) * np.exp(-t_of(0.035) / 0.009), 0.001) * 0.8
     + np.pad(sweep(260, 200, 0.02, 0.006), (0, int(R * 0.035) - int(R * 0.02))) * 0.6, 0.35)

# 점원·이동·대화
save('clerk_hired', seq([note(784, 0.25, 0.1), note(1047, 0.4, 0.16)], 0.11), 0.5)
# 월급날: 코인 세 닢이 내려놓이는 소리(점원 수와 무관하게 한 번)
save('payday', seq([note(1568, 0.12, 0.05), note(1319, 0.12, 0.05), note(1047, 0.3, 0.12)], 0.07), 0.45)
save('clerk_fired', seq([note(659, 0.25, 0.1), note(494, 0.45, 0.18)], 0.14), 0.45)
t = t_of(0.26)
save('wake', soft(np.sin(2 * np.pi * np.cumsum(300 * (700 / 300) ** (t / 0.26) * (1 + 0.04 * np.sin(2 * np.pi * 28 * t))) / R) * np.exp(-t / 0.12)), 0.5)
t = t_of(0.45)
save('passage', soft(noise(0.45, 200, 1400, 3) * np.sin(np.pi * t / 0.45) ** 2, 0.02), 0.4)
save('say', note(740, 0.06, 0.02, 'square'), 0.3)

# 설계 24 웜뱃 똥(2026-09-29 1차, 에이전트 판단 · 시안 비교 전): 떨어질 때 낮게 「뽁」, 치울 때 「쓱싹」 두 번 + 작은 반짝
save('poop', sweep(360, 120, 0.13, 0.04) + np.pad(soft(noise(0.02, 200, 1200, 31) * np.exp(-t_of(0.02) / 0.006), 0.001) * 0.4, (0, int(R * 0.13) - int(R * 0.02))), 0.45)
t = t_of(0.09)
swish = [soft(noise(0.09, 2500, 7000, 40 + i) * np.sin(np.pi * t / 0.09) ** 2, 0.004) for i in range(2)]
save('clean', seq(swish + [note(2093, 0.22, 0.07) * 0.35], 0.1), 0.45)

# 설계 25 농사 1차(에이전트 판단, 시안 비교 전): 심기 = 흙 톡톡 두 번 + 낮은 퐁, 거두기 = 사각 잎 스침 + 밝은 두 음(도 · 솔)
save('plant', seq([soft(noise(0.06, 150, 1200, 41 + i) * np.exp(-t_of(0.06) / 0.018), 0.002) for i in range(2)]
                  + [sweep(300, 480, 0.12, 0.05, 'tri') * 0.7], 0.06), 0.45)
save('harvest', seq([soft(noise(0.12, 1500, 6000, 51) * np.sin(np.pi * t_of(0.12) / 0.12) ** 2, 0.005) * 0.5,
                     note(1047, 0.2, 0.08), note(1568, 0.3, 0.12)], 0.07), 0.45)

# 웜뱃 걸음(2026-09-30 사용자: 흙 밟는 소리, 시안 a 「사각」에서 귀를 때리는 높은 잡음을 더 누름): 마른 흙 알갱이가 갈리는 짧은 잡음(700~2600Hz, 4ms로 부드럽게)
# + 낮은 밟힘(200~800Hz). 걷기 프레임 1 · 5(몸이 내려앉을 때)마다 WombatView가 낸다
save('step', soft(noise(0.07, 700, 2600, 71) * np.exp(-t_of(0.07) / 0.02), 0.004)
     + np.pad(soft(noise(0.05, 200, 800, 72) * np.exp(-t_of(0.05) / 0.02), 0.003) * 0.7, (0, int(R * 0.07) - int(R * 0.05))), 0.28)

# 설계 28 덤(반짝돌)이 나옴(1차, 에이전트 판단 · 시안 비교 전): 밝은 두 음(솔 · 높은 도) + 아주 짧은 반짝 잡음
save('bonus', seq([soft(noise(0.05, 5000, 9000, 81) * np.exp(-t_of(0.05) / 0.015), 0.002) * 0.25,
                   note(1568, 0.14, 0.06), note(2093, 0.32, 0.13)], 0.05), 0.45)

# 설계 29 · 30 웜뱃 석상(1차, 에이전트 판단): 빌기(이름이 돌기 시작) = 낮은 돌 울림 위로 올라가는 반짝 소리, 축복이 걸림 = 밝은 네 음 + 반짝
save('statue_roll', seq([soft(noise(0.18, 120, 700, 91) * np.exp(-t_of(0.18) / 0.07), 0.01) * 0.7,
                         sweep(520, 1560, 0.35, 0.14, 'tri') * 0.6], 0.05), 0.45)
save('statue_blessed', seq([note(f, 0.3, 0.11) for f in (1047, 1319, 1568, 2093)] + [soft(noise(0.08, 5000, 9000, 93) * np.exp(-t_of(0.08) / 0.03), 0.002) * 0.3], 0.06), 0.5)

# 설계 30 축복이 풀림(1차, 에이전트 판단): 은은하게 내려가는 두 음(솔 → 도)
save('blessing_end', seq([note(784, 0.16, 0.07), note(523, 0.36, 0.14)], 0.08), 0.35)

# 반짝돌 뽑기 돌 깨기(2026-10-01 사용자 선택 A 「칩튠 돌」, 시안 B 결정 울림 · C 묵직한 바위는 버림). 연출 시간표: 금 0.15 · 0.45 「톡」, 0.75 「쩍」, 1.0 「산산조각」, 1.05 「등장」(별은 석상 축복 그대로)
#   톡 = 흙빛 딸깍(300~2200Hz) + 낮은 삼각 「톡」(두 번째가 조금 높다), 쩍 = 더 길고 거친 잡음 + 내려가는 삼각,
#   산산조각 = 잡음 와장창 + 작은 딸깍 다섯이 흩어짐 + 낮은 「쿵」, 등장 = 올라가는 삼각 + 솔 · 높은 도 + 반짝
for i in range(2):
    save('draw_tap_%d' % i, np.pad(soft(noise(0.05, 300, 2200, 100 + i) * np.exp(-t_of(0.05) / 0.012), 0.002), (0, int(R * 0.12) - int(R * 0.05)))
         + sweep(300 + 40 * i, 200 + 30 * i, 0.12, 0.035, 'tri') * 0.8, 0.4)
save('draw_crack', np.pad(soft(noise(0.16, 400, 3500, 110) * np.exp(-t_of(0.16) / 0.04), 0.002), (0, int(R * 0.22) - int(R * 0.16)))
     + sweep(420, 160, 0.22, 0.06, 'tri') * 0.7, 0.45)
debris = seq([soft(noise(0.03, 1500, 5000, 130 + k) * np.exp(-t_of(0.03) / 0.008), 0.001) * (0.5 - 0.06 * k) for k in range(5)], 0.06)
save('draw_break', np.pad(soft(noise(0.4, 250, 4000, 120) * np.exp(-t_of(0.4) / 0.09), 0.003), (0, int(R * 0.55) - int(R * 0.4)))
     + np.pad(debris, (0, int(R * 0.55) - len(debris))) * 0.8 + np.pad(sweep(180, 70, 0.3, 0.1), (0, int(R * 0.55) - int(R * 0.3))) * 0.6, 0.5)
save('draw_reveal', seq([sweep(520, 1560, 0.3, 0.12, 'tri') * 0.6, note(1568, 0.16, 0.07), note(2093, 0.4, 0.15),
                         soft(noise(0.08, 5000, 9000, 140) * np.exp(-t_of(0.08) / 0.03), 0.002) * 0.25], 0.09), 0.45)

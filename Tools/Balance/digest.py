# 밸런스 측정 결과(rates.json)를 보고서 그래프용 숫자로 줄인다(2026-10-08 밸런스방).
# 사용: python Tools/Balance/digest.py <rates.json> <digest.json>
import json
import statistics
import sys

d = json.load(open(sys.argv[1], encoding='utf-8'))
week = d['week']
runs = [week] + d['weeks']
SESSION_MIN = {s['index']: s['minutes'] for s in week['sessions']}


def category(name):
    for key, cat in [('점원', '점원'), ('새 후보', '점원'), ('빵 ', '빵 · 작물'), ('작물', '빵 · 작물'), ('농장', '농장'), ('farm', '농장'),
                     ('좌대', '낚시터'), ('바위', '낚시터'), ('댐', '낚시터'), ('횟집', '횟집'), ('수조', '횟집'), ('뜨기', '횟집'),
                     ('빵집', '빵집 사물'), ('bakery 굴', '빵집 사물'), ('restaurant 굴', '횟집'), ('진열대', '빵집 업그레이드'),
                     ('굽기', '빵집 업그레이드'), ('계산', '빵집 업그레이드')]:
        if key in name:
            return cat
    return '기타'


def online_minutes(run):
    # 세션 시작 분 → 그 세션 전까지 쌓인 접속 분
    acc, out = 0.0, {}
    for s in run['sessions']:
        out[s['index']] = (s['startMin'], acc)
        acc += s['minutes']
    return out


def purchases_online(run):
    starts = online_minutes(run)
    rows = []
    for p in run['purchases']:
        start, acc = starts[p['session']]
        rows.append(dict(p, online=round(acc + (p['min'] - start), 2)))
    return rows


# 1) 마일스톤: 씨앗 다섯의 처음 시각(벽시계 분, 첫날 0시 기준)과 접속 분
MILESTONES = ['★1', '★3', '★5', '★8', '빵 크루아상', '좌대 home', '작물 strawberry', '빵 케이크', '바위 down1', '좌대 down1',
              '농장2층 열기', '유물 다 모음', '횟집 열기', '농장3층 열기', '바위 up1', '좌대 up1']
milestones = []
for key in MILESTONES:
    clock, online = [], []
    for run in runs:
        hit = next((p for p in purchases_online(run) if p['name'] == key), None)
        clock.append(hit['min'] if hit else None)
        online.append(hit['online'] if hit else None)
    got = [c for c in clock if c is not None]
    milestones.append({'name': key, 'clock': clock, 'online': online, 'reached': len(got),
                       'median': statistics.median(got) if got else None})

# 2) 씨앗 1의 산 것(접속 분 · 값 · 갈래) · 산 사이 간격(접속 분)
buys = [p for p in purchases_online(week) if p['cost'] > 0 or p['name'].startswith('댐')]
gaps = []
for prev, cur in zip(buys, buys[1:]):
    gaps.append({'online': cur['online'], 'gap': round(cur['online'] - prev['online'], 2), 'name': cur['name'], 'cost': cur['cost']})
spend = {}
for p in buys:
    spend[category(p['name'])] = spend.get(category(p['name']), 0) + p['cost']

# 3) 코인 흐름(씨앗 1, 한 주): 들어온 곳 · 나간 곳
inflow = {'빵집 손맛 · 점원(접속 중)': 0, '팁': 0, '좌대 손님': 0, '횟집': 0, '댐 잔해': 0, '오프라인 정산(판 돈)': 0}
outflow = {'월급(접속 중)': 0, '월급(오프라인)': 0}
gems_in = {'수확 덤(접속 중)': 0, '수확 덤(오프라인)': 0, '별 평가 보상': 0}
day_rows = {}
for s in week['sessions']:
    c = s['counts']
    inflow['빵집 손맛 · 점원(접속 중)'] += c.get('bakery', 0)
    inflow['팁'] += c.get('tips', 0)
    inflow['좌대 손님'] += c.get('seats', 0)
    inflow['횟집'] += c.get('restaurant', 0)
    inflow['댐 잔해'] += c.get('debris', 0)
    inflow['오프라인 정산(판 돈)'] += s['offlineSales']
    outflow['월급(접속 중)'] += c.get('wages', 0)
    outflow['월급(오프라인)'] += s['offlineWages']
    gems_in['수확 덤(접속 중)'] += c.get('gem:harvest', 0)
    gems_in['수확 덤(오프라인)'] += (s['offlineItems'] or {}).get('gem', 0)
    gems_in['별 평가 보상'] += 3 * c.get('evalPass', 0)
    day = s['day']
    r = day_rows.setdefault(day, {'day': day, 'onlineMin': 0, 'online': 0, 'wages': 0, 'offline': 0, 'seats': 0, 'bakery': 0, 'restaurant': 0})
    r['onlineMin'] += s['minutes']
    r['online'] += c.get('bakery', 0) + c.get('tips', 0) + c.get('seats', 0) + c.get('restaurant', 0) + c.get('debris', 0)
    r['wages'] += c.get('wages', 0)
    r['offline'] += s['offlineCoins']
    r['seats'] += c.get('seats', 0)
    r['bakery'] += c.get('bakery', 0) + c.get('tips', 0)
    r['restaurant'] += c.get('restaurant', 0)
for cat, v in spend.items():
    outflow['사기: ' + cat] = v
relic_draws = sum(s['counts'].get('relicDraw', 0) for s in week['sessions'])

# 4) 하루 행동(씨앗 1, 둘째 날부터 세션 평균 · 첫 세션 60분)
ACTIONS = ['served', 'tips', 'harvest:wheat', 'harvest:strawberry', 'manure', 'pray', 'relicDraw', 'evalPass', 'evalFail', 'wake', 'hires',
           'fish', 'dishes', 'restaurantAngry', 'farmSeconds', 'fishSeconds', 'restaurantSeconds', 'gaveUp']
later = [s for s in week['sessions'] if s['day'] >= 2]
first = week['sessions'][0]
actions = {a: {'first60': first['counts'].get(a, 0), 'perSession': round(sum(s['counts'].get(a, 0) for s in later) / len(later), 1)} for a in ACTIONS}

# 5) 곳마다 속도
rates = {k: d[k] for k in ['solo', 'clerks', 'farm', 'fish', 'seats', 'restaurant', 'checks'] if k in d}
evals = [{'stage': r['stage'], 'seed': r['seed'], 'stars': r['stars'], 'log': [(e['star'], e['passed'], e['seconds'], e['at']) for e in r['log']]} for r in d.get('eval', [])]

# 6) 첫 시간 · 한 주 코인 선(씨앗 1)
starts = online_minutes(week)
line = []
for c in week['coins']:
    line.append(c)

# 7) 세션마다: 산 것 수 · 접속 중 순수입(판 돈 − 월급) ÷ 분 · 오프라인 순수입 · 세션을 마칠 때 가장 싼 다음 목표 값
per_session = []
for s in week['sessions']:
    c = s['counts']
    earned = c.get('bakery', 0) + c.get('tips', 0) + c.get('seats', 0) + c.get('restaurant', 0) + c.get('debris', 0)
    end = [x for x in week['coins'] if s['startMin'] < x['min'] <= s['startMin'] + s['minutes'] + 0.01]
    per_session.append({'index': s['index'], 'day': s['day'], 'start': s['startMin'], 'buys': sum(1 for p in buys if p['session'] == s['index']),
                        'netPerMin': round((earned - c.get('wages', 0)) / s['minutes']), 'offline': s['offlineCoins'],
                        'nextCost': end[-1]['nextCost'] if end else None, 'next': end[-1]['next'] if end else None,
                        'gems': end[-1]['gems'] if end else None, 'wheat': end[-1]['wheat'] if end else None, 'stars': end[-1]['stars'] if end else None})

# 8) 퀘스트 사슬(guide · guide2 · guide3): 걸음마다 걸린 접속 분 가운데 값 · 끝난 벽시계 분 · 건너뛴 판 수 · 닿은 판 수
chains = {}
for key in ['guide', 'guide2', 'guide3']:
    if key not in d:
        continue
    steps = {}
    for run in d[key]:
        for st in run['steps']:
            steps.setdefault(st['name'], []).append(st)
    chains[key] = [{'name': name, 'reached': len(sts), 'skips': sum(1 for st in sts if st['skipped']),
                    'minutes': statistics.median(st['onlineMinutes'] for st in sts),
                    'endClock': statistics.median_low([st['endClock'] for st in sts])} for name, sts in steps.items()]

digest = {'chains': chains, 'perSession': per_session, 'milestones': milestones, 'buys': buys, 'gaps': gaps, 'spend': spend, 'inflow': inflow, 'outflow': outflow, 'gemsIn': gems_in,
          'relicDraws': relic_draws, 'days': list(day_rows.values()), 'actions': actions, 'rates': rates, 'evals': evals, 'line': line,
          'sessions': [{k: v for k, v in s.items() if k != 'counts'} for s in week['sessions']]}
json.dump(digest, open(sys.argv[2], 'w', encoding='utf-8'), ensure_ascii=False)

if __name__ == '__main__':
    for m in milestones:
        print(m['name'], m['reached'], m['median'], m['clock'])
    print('spend', spend)
    print('in', inflow)
    print('out', outflow)
    print('gems', gems_in, 'draws', relic_draws)
    for r in day_rows.values():
        print(r)
    for a, v in actions.items():
        print(a, v)
    big = sorted(gaps, key=lambda g: -g['gap'])[:12]
    print('longest gaps', [(g['online'], g['gap'], g['name']) for g in big])

# M. 시연으로 배우기 ★★★

> 사람이 Heuristic 으로 직접 푼 판을 녹화하고, 그 기록을 학습에 섞어 **처음 성공까지 걸리는 시간**을 줄인다.

| | |
|---|---|
| 예상 학습 시간 | 바탕 과제와 비슷하거나 짧게 — 예상치 |
| 새로 배우는 개념 | **Demonstration Recorder** · **BC (따라 하기)** · **GAIL (사람처럼 하면 보상)** |
| 출발점 | 보상이 드물어 학습이 느린 과제. 추천: F 상자 밀어 넣기 · G 주차 · H 착륙선 |

---

## 1. 목표 (성공 기준)

- 같은 과제를 **PPO 만 / PPO + BC / PPO + GAIL** 세 번 학습해, **처음 성공률 50% 에 닿는 스텝 수** 비교.
- 시연이 **서툴 때**(일부러 엉망으로 녹화)도 해 본다. 어떻게 되는지 적는다.

## 2. 무엇이 다른가

| | 보통 강화학습 | + 시연 |
|---|---|---|
| 처음 | 아무렇게나 움직이며 우연한 성공을 기다림 | **사람이 한 판들을 보고 시작** |
| BC | — | "이 관측일 때 사람은 이 행동" 을 그대로 따라 한다 (지도학습) |
| GAIL | — | "사람이 한 것과 비슷하면" 보상을 하나 더 준다 |
| 위험 | — | 사람의 습관(나쁜 것까지)을 따라 한다 |

## 3. 녹화하기

1. 에이전트 오브젝트에 **Demonstration Recorder** 컴포넌트를 붙인다.

   | 칸 | 값 |
   |---|---|
   | Record | 켬 |
   | Num Steps To Record | 0 (멈출 때까지) 또는 원하는 스텝 수 |
   | Demonstration Name | `push_good` |
   | Demonstration Directory | `Assets/StudentProjects/<내폴더>/Demos` |

2. Behavior Type = **Heuristic Only** → Play → 키보드로 **성공 20판** 정도.
3. Play 를 멈추면 `.demo` 파일이 생긴다. Inspector 에서 판 수 · 평균 보상을 볼 수 있다.
4. 녹화가 끝나면 **Record 를 끈다** (학습 중에 계속 녹화되지 않게).

> 시연은 관측 · 행동 수가 지금 Behavior Parameters 와 같아야 한다. 관측을 바꾸면 다시 녹화한다.

## 4. yaml

```yaml
behaviors:
  MyAgent:
    # ... 기존 설정 그대로 ...
    behavioral_cloning:
      demo_path: Assets/StudentProjects/<내폴더>/Demos/push_good.demo
      strength: 0.5
      steps: 50000          # 처음 5만 스텝 동안만 따라 하기 비중을 두고 줄인다
    reward_signals:
      extrinsic: { gamma: 0.99, strength: 1.0 }
      gail:                 # GAIL 실험 때만 넣는다 (BC 와 따로 한 번씩)
        strength: 0.1
        demo_path: Assets/StudentProjects/<내폴더>/Demos/push_good.demo
```

- **세 실험은 한 번에 하나씩**: PPO 만 → `behavioral_cloning` 만 추가 → `gail` 만 추가.
- GAIL `strength` 가 extrinsic 보다 크면 "과제 성공"보다 "사람 흉내"를 더 중요하게 본다. 0.1 정도로 작게 시작.

## 5. 만드는 순서

1. 바탕 과제를 PPO 만으로 학습 (기준선 그래프 저장).
2. 성공 시연 20판 녹화.
3. BC 실험 → GAIL 실험. 같은 max_steps.
4. (선택) 서툰 시연 녹화 → BC 다시. 결과 비교.

## 6. 자주 밟는 함정

| 증상 | 원인 |
|---|---|
| 학습 시작 시 demo 관련 에러 | 관측/행동 크기가 녹화 때와 다르다 · 경로 오타 |
| 학습 중 .demo 파일이 계속 커진다 | Record 를 안 껐다 |
| 사람보다 잘 못하고 거기서 멈춘다 | BC 비중이 너무 오래 크다 → `steps` 줄이기 |
| GAIL 만 오르고 실제 성공은 안 는다 | GAIL strength 가 크다 |

## 7. 다음 단계

- **시연 판 수 비교** — 5판 / 20판 / 50판.
- **시연 없이 할 수 없던 과제** — 보상을 끝에만(진전 보상 없이) 주는 F 상자 밀기를 시연만으로 풀 수 있는지.

## 8. opencode 에 맡길 때

```
(코드 변경 없음) 내 config.yaml 에 behavioral_cloning 블록만 추가해 줘. demo_path 는 이 경로, strength 0.5, steps 50000.
다른 하이퍼파라미터는 바꾸지 마.
```

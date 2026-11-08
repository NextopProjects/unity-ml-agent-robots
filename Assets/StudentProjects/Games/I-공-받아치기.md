# I. 공 받아치기 ★★

> 떨어지는 공을 패들로 받아 다시 위로 띄운다. 공을 바닥에 떨어뜨리면 끝.

| | |
|---|---|
| 예상 학습 시간 | 30~60분 — 예상치 |
| 새로 배우는 개념 | **Decision Period 와 반응 속도** · **관측 쌓기 (Stacked Vectors)** — 한 장면만으로는 속도를 모를 때 |
| 출발점 | 새 씬 |

---

## 1. 목표 (성공 기준)

- 한 판 평균 **받아치기 20회 이상** (20판 평균, MaxStep 3000).
- Decision Period 1 / 5 / 10 세 가지 비교 표.

## 2. RollerBall 과 비교

| | RollerBall | 공 받아치기 |
|---|---|---|
| 목표 | 가만히 있다 | **떨어지며 움직인다** — 미래 위치를 예측해야 |
| 결정 간격 | 아무래도 됨 | **늦으면 놓친다** |
| 반복 | 한 번 닿으면 끝 | 계속 받아친다 (A 동전처럼 판을 안 끝냄) |

## 3. 장면 구성

```
PaddleArea
├── Floor     Plane 6×6 (scale 0.6), 태그 "Floor"
├── Paddle    Cube scale (1.5,0.2,1.5) 높이 0.5
│             Rigidbody isKinematic — MovePosition 으로 움직인다
└── Ball      Sphere 0.4, Rigidbody mass 0.2
              Physic Material: Bounciness 0.9, Bounce Combine Maximum
```

- 패들은 **kinematic + MovePosition**. `transform.position` 으로 옮기면 공을 쳐도 힘이 전달되지 않는다
  (RobotArms 의 "물린 물체가 빠진다" 함정과 같은 원리).

## 4. 설계

| 관측 (7) | 왜 |
|---|---|
| 공 − 패들 (x, z) | 공 아래로 가야 한다 |
| 공 높이 | 언제 닿나 |
| 공 속도 (x, y, z) | **어디에 떨어질지** 예측 |
| 패들 x ÷ 3 | 벽(바닥 끝) 가까운지 (z 도 넣을지는 선택) |

| 행동 (연속 2) | 패들 이동 속도 x, z (× 4 m/s), 바닥 범위 ±2.5 로 자름 |

| 보상 · 끝 | |
|---|---|
| 공이 패들에 닿음 (공의 위쪽 속도가 생김) | **+0.1** |
| 공이 바닥에 닿음 | **판 끝** |
| MaxStep 3000 | 판 끝 |

무작위: 공 시작 위치 x, z ∈ [−2, 2], 높이 4, 시작 옆 속도 조금.

## 5. 만드는 순서

1. 장면만 → 공이 패들 위에서 몇 번 튀는지 본다 (가만둬도 튀어야 한다).
2. Heuristic (WASD) 로 받아쳐 본다. 패들 속도 4 m/s 가 너무 느리면 조절.
3. 같은 공을 패들이 **한 번 닿을 때 한 번만** +0.1 (OnCollisionEnter 는 튀는 동안 여러 번 올 수 있다).
4. 학습. 그 다음 Decision Period 비교 실험.

## 6. 자주 밟는 함정

| 증상 | 원인 |
|---|---|
| 패들이 공을 쳐도 공이 그냥 통과/멈춤 | `transform.position` 으로 옮겼다 → MovePosition |
| 한 번 받았는데 보상이 여러 번 | 연속 충돌. 공이 위로 떠나기 전에는 다시 안 주기 |
| Decision Period 10 이면 전혀 못 한다 | 정상 — 그게 실험 결과다. 왜 그런지 설명을 적는다 |
| 공이 점점 높이 튄다 | Bounciness 1 이상 / Combine Maximum + 패들 위로 치기. 0.8~0.9 로 |

## 7. 다음 단계

- **속도를 관측에서 빼고 Stacked Vectors 3** — Behavior Parameters 의 Stacked Vectors 를 올리면 최근 3번 관측이 함께 들어간다.
  속도 없이도 "위치 변화"로 속도를 알아내는지, 속도를 준 버전과 비교.
- **공 2개** — 관측 +6. 어느 공을 먼저 받을지.

## 8. opencode 에 맡길 때

```
PaddleAgent.cs: 패들은 kinematic Rigidbody, OnActionReceived 에서 MovePosition 으로 x,z 를 ±2.5 안에서 움직여 줘. 관측 7칸은 DESIGN.md 대로.
공이 패들에 닿으면 +0.1 — 공이 패들에서 0.5 이상 떠오르기 전에는 다시 주지 마.
```

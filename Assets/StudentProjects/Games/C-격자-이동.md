# C. 격자 이동 ★

> 바둑판 위 말이 한 칸씩 움직여 목표 칸에 간다. 물리가 없다.

| | |
|---|---|
| 예상 학습 시간 | 5~15분 — 예상치 |
| 새로 배우는 개념 | **이산 행동** (5갈래 중 하나 고르기) · **시간 벌점** · (확장) **행동 마스킹** |
| 출발점 | 새 씬. Rigidbody 를 쓰지 않는다 |

---

## 1. 목표 (성공 기준)

- 7×7 격자에서 20판 중 **19판 이상** 도착.
- 평균 걸음 수가 **최단 거리의 1.3배 이하** (최단 거리 = |dx| + |dz|).

## 2. RollerBall 과 비교

| | RollerBall | 격자 이동 |
|---|---|---|
| 행동 | 연속 2 (힘) | **이산 1 가지(branch), 5개 중 하나** |
| 움직임 | 물리 (Rigidbody.AddForce) | `localPosition` 을 한 칸 옮김 |
| 속도 관측 | 필요 | **필요 없다** (속도가 없다) |
| 시간 | 상관없음 | 스텝마다 −0.001 → 빨리 갈수록 이득 |

## 3. 장면 구성

```
GridArea
├── Floor     Plane scale (0.7,1,0.7) = 7×7
├── Agent     Cube (0,0.5,0) scale 0.9   — Rigidbody 없음
└── Target    Cube scale (0.9,0.1,0.9) 초록
```

- 칸 좌표는 정수 -3 ~ 3. 위치 = (x, 0.5, z).
- Behavior Parameters: Continuous 0 · **Discrete Branches 1, Branch 0 Size 5**.
- Decision Requester: Decision Period **1** 로 해도 된다(한 결정 = 한 칸). 너무 빨라 보이면 화면용으로만 늦춘다.

## 4. 설계

| 관측 (4) | 왜 |
|---|---|
| 내 칸 (x, z) ÷ 3 | −1~1 로 맞춘다 |
| 목표 칸 (x, z) ÷ 3 | |

(목표 − 나 2칸만 줘도 된다. 둘을 비교해 보는 것도 좋은 실험이다)

| 행동 (이산 5) | |
|---|---|
| 0 | 가만히 |
| 1 / 2 | +x / −x |
| 3 / 4 | +z / −z |

| 보상 · 끝 | |
|---|---|
| 목표 칸 도착 | **+1, 판 끝** |
| 매 스텝 | **−0.001** |
| 격자 밖으로 나가려 함 | 움직이지 않음 (벌점 없음) |
| MaxStep 100 | 판 끝 |

무작위: 내 칸 · 목표 칸 매 판 무작위, 겹치지 않게.

## 5. 만드는 순서

1. 장면. 칸을 눈으로 셀 수 있게 바닥에 격자 머티리얼을 깔면 좋다.
2. `Heuristic`: 화살표 키 → 0~4. **키를 눌렀을 때 한 번만** 움직이게 하려면 `wasPressedThisFrame` 을 쓴다.
3. `OnActionReceived(actions)` 에서 `actions.DiscreteActions[0]` 으로 한 칸 이동 + 범위 밖이면 무시.
4. 도착 판정 · 시간 벌점 · MaxStep.
5. 관측 4칸 → 학습. 이산 행동은 연속보다 빨리 배우는 경우가 많다. 그래프를 RollerBall 과 비교.

## 6. 자주 밟는 함정

| 증상 | 원인 |
|---|---|
| Heuristic 에서 한 번 누르면 여러 칸 간다 | `isPressed` 를 썼다. `wasPressedThisFrame` |
| 학습된 말이 가끔 제자리에서 영원히 멈춘다 | 시간 벌점이 없다. 또는 벌점이 너무 작다 |
| 시간 벌점을 크게 했더니 이상하다 | −0.01 × 100스텝 = −1 → 도착 보상과 맞먹는다. 벌점 총합이 성공 보상보다 작아야 한다 |
| 관측 에러 | Space Size 4 · Discrete Branch Size 5 확인 |

## 7. 다음 단계

- **벽 칸 + 행동 마스킹** — 격자에 벽 칸 몇 개. 벽이나 바깥으로 가는 행동을 **아예 못 고르게** 막는다.
  ```csharp
  public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
  {
      // 오른쪽이 막혔으면 1번(+x) 행동을 끈다
      if (막혔다) actionMask.SetActionEnabled(0, 1, false);
  }
  ```
  마스킹 있음/없음(없으면 벽에 박는 걸 스스로 배워야 함) 학습 속도 비교.
  벽 위치를 관측에 넣어야 하는지도 생각해 본다 (힌트: 벽이 매 판 바뀌면 필요하다).

## 8. opencode 에 맡길 때

```
이산 행동 1 branch, 크기 5 (0 정지, 1 +x, 2 -x, 3 +z, 4 -z) 로 GridAgent.cs 를 만들어 줘.
Rigidbody 없이 localPosition 을 1칸씩 옮기고, -3~3 밖으로는 안 나가게.
Heuristic 은 화살표 키 wasPressedThisFrame. 보상은 DESIGN.md 대로.
```

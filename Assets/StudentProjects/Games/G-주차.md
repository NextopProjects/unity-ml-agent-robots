# G. 주차 ★★

> 자동차가 앞뒤로 가고 핸들을 꺾어 주차 칸에 **방향까지 맞춰** 선다.

| | |
|---|---|
| 예상 학습 시간 | 40~90분 (영역 9개) — 예상치 |
| 새로 배우는 개념 | **각도는 sin · cos 로 관측** · **내 기준 좌표**(InverseTransformPoint) · 옆으로 못 가는 이동(비홀로노믹) |
| 출발점 | 새 씬 |

---

## 1. 목표 (성공 기준)

- 20판 중 **15판 이상** 칸 안 · 방향 오차 15° 이내 · 정지.
- 관측을 **각도(도) 그대로** 준 버전과 **sin·cos** 버전 비교.

## 2. RollerBall 과 비교

| | RollerBall | 주차 |
|---|---|---|
| 움직임 | 아무 방향으로 힘 | **앞/뒤 + 회전만.** 옆으로 못 간다 |
| 관측 좌표 | 세계 좌표 | **차 기준 좌표** ("목표가 내 앞 왼쪽 3m") |
| 성공 | 위치 | 위치 **+ 방향** + 정지 |

## 3. 장면 구성

```
ParkingArea
├── Floor     Plane 20×20 (scale 2)
├── Car       Cube scale (1,0.5,2) — 앞쪽 표시용 작은 자식 큐브(헤드라이트)를 단다
│             Rigidbody mass 1 · Linear Damping 1 (안 그러면 끝없이 빨라진다) · Freeze Position Y · Freeze Rotation X, Z
└── Spot      Cube scale (1.6,0.01,2.6), 콜라이더 제거 — 앞쪽 끝에 선 하나 그려 방향 표시
```

**차를 움직이는 가장 단순한 방법** (진짜 바퀴 물리 X — 이 과제의 핵심이 아니다)

```csharp
// 가속: 차 앞방향으로 힘, 조향: 속도에 비례해 회전 (멈춰 있으면 안 돈다)
rb.AddForce(transform.forward * throttle * 10f);
float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
rb.MoveRotation(rb.rotation * Quaternion.Euler(0, steer * forwardSpeed * 0.4f, 0)); // 속도 5 에서 초당 약 100°
// 옆미끄러짐 지우기: 옆 방향 속도를 없앤다
rb.linearVelocity -= transform.right * Vector3.Dot(rb.linearVelocity, transform.right);
```

배율(10 · 0.4)은 시작값이다. Heuristic 으로 몰아 보고 정한 값을 설계서에 적는다.

## 4. 설계

| 관측 (6) | 왜 |
|---|---|
| 칸 위치를 **차 기준**으로 (x, z) ÷ 10 | `transform.InverseTransformPoint(spot.position)`. "앞/옆"으로 생각해야 운전이 쉽다 |
| 방향 차의 sin, cos | 359° 와 1° 는 거의 같은데 숫자로는 358 차이 → sin·cos 면 이어진다 |
| 앞방향 속도 | |
| 회전 속도 (angularVelocity.y) | |

방향 차: `Vector3.SignedAngle(transform.forward, spot.forward, Vector3.up)` (도) → 라디안으로 바꿔 sin, cos.

| 행동 (연속 2) | |
|---|---|
| 0 | 가속 (−1 후진 ~ +1 전진) |
| 1 | 조향 (−1 왼쪽 ~ +1 오른쪽) |

| 보상 · 끝 | |
|---|---|
| 거리 < 0.5 · 방향 오차 < 15° · 속력 < 0.2 | **+1, 판 끝** |
| 거리 진전 | + (이전 − 지금) × 0.1, 한 스텝 ±0.01 상한 |
| 바닥 밖 | 판 끝 |
| MaxStep 2000 | 판 끝 |

무작위: 차 위치 · 방향, 칸 위치 · 방향 (칸과 차 4 이상).

## 5. 만드는 순서

1. 장면 + **차 움직임 코드만** + Heuristic (↑↓ 가속, ←→ 조향). 직접 주차해 본다.
   후진 주차가 너무 어려우면 조향 배율을 조정. **사람이 주차 못 하면 학습도 안 된다.**
2. 성공 판정을 화면에 띄워 (거리 · 각도 오차) 사람이 성공 5번.
3. 관측 · 보상 → 학습.
4. 비교 실험: 방향 관측을 "각도 ÷ 180" 한 칸으로 바꿔 다시 학습.

## 6. 자주 밟는 함정

| 증상 | 원인 |
|---|---|
| 차가 얼음처럼 옆으로 미끄러진다 | 옆 속도 지우기를 빼먹었다 |
| 제자리에서 빙글 돈다 | 조향을 속도와 상관없이 줬다 |
| 위치는 맞는데 방향을 못 맞춘다 | 방향 보상이 끝 보상에만 있다. 먼저 방향 기준을 30° 로 느슨하게 시작 |
| 칸 근처에서 앞뒤로 덜덜 | 정지 조건 속력 기준이 너무 빡빡 |

## 7. 다음 단계

- **양옆에 다른 차** (콜라이더 있는 큐브 2개) — 부딪히면 −0.5 판 끝. 관측은? → 광선 센서 ([E](E-장애물-피해-도착.md) 참고).
- **후진만 허용** (가속 0~−1) — "후진 주차"를 배우는지.

## 8. opencode 에 맡길 때

```
위 문서의 차 움직임 3줄을 OnActionReceived 에 써서 ParkingAgent.cs 를 만들어 줘.
관측 6칸: InverseTransformPoint 로 칸 위치 x,z ÷10, 방향 차(SignedAngle) 의 sin, cos, 앞방향 속도, angularVelocity.y.
보상은 DESIGN.md 대로, 숫자 바꾸지 마. Heuristic 은 화살표 키.
```

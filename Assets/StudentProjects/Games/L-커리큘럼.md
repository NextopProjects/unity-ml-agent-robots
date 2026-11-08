# L. 커리큘럼으로 멀리 ★★★

> 이미 만든 과제(A~I 중 하나)를 골라, **쉬운 판에서 어려운 판으로** 난이도를 올리며 학습한다.

| | |
|---|---|
| 예상 학습 시간 | 바탕 과제의 1~2배 — 예상치 |
| 새로 배우는 개념 | **커리큘럼** (`environment_parameters` · lesson) · 파이썬에서 Unity 로 값 보내기 · (확장) 성공률로 직접 난이도 조절 |
| 출발점 | 내가 끝낸 ★ / ★★ 과제. 추천: B 멈춰서 도착 · F 상자 밀어 넣기 · H 착륙선 |

---

## 1. 목표 (성공 기준)

- 가장 어려운 난이도에서 **커리큘럼 있음 vs 없음**(처음부터 어려움) 두 모델을 같은 스텝 수만큼 학습해 성공률 비교.
- 난이도가 바뀌는 시점이 TensorBoard 그래프 어디인지 표시해 보고서에 싣는다.

## 2. 무엇을 난이도로 쓸까

**숫자 하나**만 고른다. 둘을 같이 올리면 어느 쪽이 어려운지 모른다.

| 바탕 과제 | 난이도 숫자 (쉬움 → 어려움) |
|---|---|
| B 멈춰서 도착 | 목표 거리 범위 2 → 4 → 6 |
| F 상자 밀어 넣기 | 상자–골 거리 2 → 4 → 6 |
| H 착륙선 | 패드 폭 4 → 2.5 → 1.5 |
| E 장애물 | 기둥 수 0 → 2 → 4 |

## 3. Unity 쪽 — 값 읽기

```csharp
// 판이 시작할 때마다 파이썬이 정한 현재 난이도를 읽는다 (없으면 기본값 6 = 가장 어려움)
float dist = Academy.Instance.EnvironmentParameters.GetWithDefault("goal_distance", 6f);
```

`OnEpisodeBegin` 에서 읽어 배치에 쓴다. **기본값을 가장 어려운 값으로** 두면 커리큘럼 없이 돌릴 때 = 처음부터 어려움 (비교용).

## 4. yaml 쪽 — 단계 정하기

```yaml
environment_parameters:
  goal_distance:
    curriculum:
      - name: Near
        completion_criteria:
          measure: reward          # 평균 보상이
          behavior: MyAgent
          threshold: 0.8           # 0.8 을 넘으면
          min_lesson_length: 100   # (최근 100판 기준) 다음 단계로
        value: 2.0
      - name: Mid
        completion_criteria: { measure: reward, behavior: MyAgent, threshold: 0.8, min_lesson_length: 100 }
        value: 4.0
      - name: Far
        value: 6.0
```

- `environment_parameters` 는 **이 yaml 전체**에 걸린다. 다른 과제 yaml 과 섞지 않는다 (Humanoid 함정과 같은 이야기).
- 문턱(threshold)은 **보상 구조에 맞춰** 정한다. 끝에 +1 만 주는 과제면 0.8 ≈ 성공률 80% 근처, 진전 보상이 있으면 다르다 → 먼저 쉬운 단계의 평균 보상을 보고 정한다.

## 5. 만드는 순서

1. 바탕 과제에 난이도 숫자 하나를 `GetWithDefault` 로 바꿔 넣는다. 기본값으로 예전과 똑같이 도는지 확인.
2. **커리큘럼 없이** 가장 어려운 값으로 학습 (기준선). 스텝 수를 정해 둔다.
3. yaml 에 커리큘럼 → 같은 스텝 수만큼 학습. TensorBoard 의 `Environment/Lesson Number` 확인.
4. 두 모델을 가장 어려운 값에서 20판씩 평가.

## 6. 자주 밟는 함정

| 증상 | 원인 |
|---|---|
| 단계가 안 넘어간다 | 문턱이 너무 높다 (보상 구조상 도달 못 하는 값) |
| 넘어가자마자 무너지고 못 돌아온다 | yaml 커리큘럼은 **내려가지 않는다** — 아래 다음 단계 참고 |
| 이름이 안 맞아 기본값만 쓴다 | C# 의 문자열과 yaml 키 철자 |

## 7. 다음 단계 — 성공률로 직접 조절하기

Humanoid 6장 실측: yaml 커리큘럼은 성공 0 인데도 올라간 적이 있고, 내려가지 않아서 못 하는 단계에 갇혔다.
그래서 **에이전트 코드에서 직접** 난이도를 정했다:

- 최근 20판 중 14판 성공 → 한 단계 올림, 3판 이하 → 한 단계 내림.
- 영역마다 따로 → 잘하는 영역은 어렵게, 못하는 영역은 쉽게.

yaml 방식과 비교해 보고 장단점을 적는다.

## 8. opencode 에 맡길 때

```
내 PushAgent.cs 의 상자-골 거리 상수를 Academy.Instance.EnvironmentParameters.GetWithDefault("goal_distance", 6f)
로 바꿔 줘. 다른 곳은 손대지 마. yaml 커리큘럼은 내가 쓴다.
```

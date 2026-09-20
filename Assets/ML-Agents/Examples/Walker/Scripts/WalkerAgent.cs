using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgentsExamples;
using Unity.MLAgents.Sensors;
using BodyPart = Unity.MLAgentsExamples.BodyPart;
using Random = UnityEngine.Random;

/*
 * ============================================================================
 *  WalkerAgent - 인간형 래그돌이 "목표 지점을 향해 걷는 법"을 스스로 배우는 예제
 * ============================================================================
 *
 *  [강화학습 기본 용어]
 *   - Agent(에이전트)   : 학습하는 주체. 여기서는 래그돌 캐릭터.
 *   - Observation(관측) : 에이전트가 보는 정보(속도, 자세, 목표 위치 등). 신경망의 "입력".
 *   - Action(행동)      : 에이전트가 내리는 결정(각 관절을 얼마나 돌릴지). 신경망의 "출력".
 *   - Reward(보상)      : 잘했는지 못했는지 알려주는 점수. 이 점수를 최대화하도록 학습함.
 *   - Episode(에피소드) : 한 번의 시도. 끝나면 초기 상태로 리셋하고 다시 시작.
 *
 *  [학습 루프 - 매 스텝마다 아래 순서가 반복됨]
 *   OnEpisodeBegin()     → 몸을 초기 상태로 리셋
 *   CollectObservations()→ 현재 상태를 관측해서 신경망에 전달
 *   OnActionReceived()   → 신경망이 낸 행동값으로 관절을 움직임
 *   FixedUpdate()        → 결과를 평가해서 보상을 부여
 *
 *  [이 에이전트의 규모]
 *   - 관측(Observation) : 243개
 *   - 행동(Action)      : 연속값 39개 (관절 회전 26개 + 관절 힘 13개)
 *   ※ Inspector의 Behavior Parameters 값과 반드시 일치해야 함.
 * ============================================================================
 */
public class WalkerAgent : Agent
{
    [Header("Walk Speed")]
    [Range(0.1f, 10)]
    [SerializeField]
    // 에이전트가 달성하려는 목표 보행 속도
    private float m_TargetWalkingSpeed = 10;

    // 외부에서 목표 속도를 읽고 쓸 수 있게 해주는 프로퍼티.
    // 값을 넣을 때 0.1 ~ 최대속도 범위를 벗어나지 않도록 Clamp로 제한한다.
    public float MTargetWalkingSpeed // property
    {
        get { return m_TargetWalkingSpeed; }
        set { m_TargetWalkingSpeed = Mathf.Clamp(value, .1f, m_maxWalkingSpeed); }
    }

    const float m_maxWalkingSpeed = 10; // 최대 보행 속도

    // 에피소드마다 목표 속도를 랜덤하게 바꿀지 여부.
    // true면 매 에피소드마다 0 ~ 최대속도 사이 값이 무작위로 지정된다.
    // → "빠르게/느리게" 다양한 속도에 대응하는 범용적인 정책을 학습하게 됨(일반화).
    // false면 항상 지정된 속도 하나만 목표로 삼는다.
    public bool randomizeWalkSpeedEachEpisode;

    // 학습 중 에이전트가 걸어가야 할 방향(월드 좌표 기준)
    private Vector3 m_WorldDirToWalk = Vector3.right;

    [Header("Target To Walk Towards")] public Transform target; // 에이전트가 향해 걸어갈 목표물

    // ---- 아래는 래그돌의 각 신체 부위. Inspector에서 직접 연결해 줘야 한다 ----
    [Header("Body Parts")] public Transform hips;   // 골반(몸의 기준점)
    public Transform chest;    // 가슴
    public Transform spine;    // 척추
    public Transform head;     // 머리
    public Transform thighL;   // 왼쪽 허벅지
    public Transform shinL;    // 왼쪽 정강이
    public Transform footL;    // 왼발
    public Transform thighR;   // 오른쪽 허벅지
    public Transform shinR;    // 오른쪽 정강이
    public Transform footR;    // 오른발
    public Transform armL;     // 왼팔(위팔)
    public Transform forearmL; // 왼쪽 아래팔
    public Transform handL;    // 왼손
    public Transform armR;     // 오른팔(위팔)
    public Transform forearmR; // 오른쪽 아래팔
    public Transform handR;    // 오른손

    // 관측의 "기준 좌표계" 역할을 하는 큐브.
    // 래그돌은 학습 중 몸이 제멋대로 흔들리기 때문에, 몸 자체를 기준으로 삼으면 관측값이 불안정하다.
    // 그래서 목표 방향을 향해 안정적으로 정렬된 이 큐브를 기준으로 좌표를 변환해 관측한다. → 학습이 훨씬 잘 됨.
    OrientationCubeController m_OrientationCube;

    // 목표 방향을 시각적으로 알려주는 화살표 오브젝트(학습에는 영향 없음, 디버깅용)
    DirectionIndicator m_DirectionIndicator;

    // 모든 관절(ConfigurableJoint)을 한꺼번에 관리해 주는 도우미 컴포넌트
    JointDriveController m_JdController;

    // 커리큘럼 학습 등에서 외부(설정 파일)로부터 환경 변수를 받아오는 통로
    EnvironmentParameters m_ResetParams;

    /// <summary>
    /// 최초 1회만 호출된다. Unity의 Awake/Start와 비슷한 역할.
    /// 여기서 필요한 컴포넌트를 찾고 신체 부위를 등록한다.
    /// </summary>
    public override void Initialize()
    {
        m_OrientationCube = GetComponentInChildren<OrientationCubeController>();
        m_DirectionIndicator = GetComponentInChildren<DirectionIndicator>();

        // 각 신체 부위를 JointDriveController에 등록한다.
        // 등록해야 관절을 제어하고 관측값을 모을 수 있다.
        m_JdController = GetComponent<JointDriveController>();
        m_JdController.SetupBodyPart(hips);
        m_JdController.SetupBodyPart(chest);
        m_JdController.SetupBodyPart(spine);
        m_JdController.SetupBodyPart(head);
        m_JdController.SetupBodyPart(thighL);
        m_JdController.SetupBodyPart(shinL);
        m_JdController.SetupBodyPart(footL);
        m_JdController.SetupBodyPart(thighR);
        m_JdController.SetupBodyPart(shinR);
        m_JdController.SetupBodyPart(footR);
        m_JdController.SetupBodyPart(armL);
        m_JdController.SetupBodyPart(forearmL);
        m_JdController.SetupBodyPart(handL);
        m_JdController.SetupBodyPart(armR);
        m_JdController.SetupBodyPart(forearmR);
        m_JdController.SetupBodyPart(handR);

        m_ResetParams = Academy.Instance.EnvironmentParameters;
    }

    /// <summary>
    /// 에피소드가 시작될 때마다 호출된다.
    /// 넘어지거나 최대 스텝에 도달해 에피소드가 끝나면, 여기서 처음 상태로 되돌린다.
    /// </summary>
    public override void OnEpisodeBegin()
    {
        // 모든 신체 부위를 초기 위치/회전/속도로 리셋
        foreach (var bodyPart in m_JdController.bodyPartsDict.Values)
        {
            bodyPart.Reset(bodyPart);
        }

        // 시작 방향을 무작위로 회전시킨다.
        // 항상 같은 방향에서만 시작하면 그 상황에만 특화되어 버리므로(과적합),
        // 다양한 시작 조건을 주어 일반화 성능을 높인다.
        hips.rotation = Quaternion.Euler(0, Random.Range(0.0f, 360.0f), 0);

        UpdateOrientationObjects();

        // 이번 에피소드의 목표 보행 속도를 결정
        MTargetWalkingSpeed =
            randomizeWalkSpeedEachEpisode ? Random.Range(0.1f, m_maxWalkingSpeed) : MTargetWalkingSpeed;
    }

    /// <summary>
    /// 신체 부위 하나에 대한 관측값을 센서에 추가한다.
    /// (CollectObservations에서 부위마다 반복 호출됨)
    /// </summary>
    public void CollectObservationBodyPart(BodyPart bp, VectorSensor sensor)
    {
        // 이 부위가 바닥에 닿아 있는가? (발이 땅에 닿았는지 등을 판단하는 중요한 정보)
        sensor.AddObservation(bp.groundContact.touchingGround);

        // 속도/각속도를 큐브 기준 좌표계로 변환해서 관측한다.
        // 월드 좌표로도 가능하지만, 방향이 바뀔 때마다 값이 달라져 학습이 잘 안 된다.
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(bp.rb.linearVelocity));
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(bp.rb.angularVelocity));

        // 골반(hips)에 대한 상대 위치 → "몸통 기준으로 이 부위가 어디에 있는가"
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(bp.rb.position - hips.position));

        // 골반과 양손은 제외한다.
        // 골반은 기준점 자신이고, 손은 걷기에 영향이 적어 관측에서 빼 데이터 양을 줄인 것.
        if (bp.rb.transform != hips && bp.rb.transform != handL && bp.rb.transform != handR)
        {
            sensor.AddObservation(bp.rb.transform.localRotation);                    // 관절의 현재 회전
            sensor.AddObservation(bp.currentStrength / m_JdController.maxJointForceLimit); // 현재 관절 힘(0~1로 정규화)
        }
    }

    /// <summary>
    /// 매 의사결정 스텝마다 호출된다. 에이전트가 "보는" 모든 정보를 여기에 담는다.
    /// 여기 담긴 값들이 곧 신경망의 입력이 된다.
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        var cubeForward = m_OrientationCube.transform.forward; // 목표를 향한 전방 방향

        // 우리가 맞추고 싶은 목표 속도 벡터 (방향 × 속력)
        var velGoal = cubeForward * MTargetWalkingSpeed;
        // 래그돌의 현재 평균 속도
        var avgVel = GetAvgVelocity();

        // 목표 속도와 현재 속도의 차이(스칼라)
        sensor.AddObservation(Vector3.Distance(velGoal, avgVel));
        // 현재 속도 (큐브 기준)
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(avgVel));
        // 목표 속도 (큐브 기준)
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(velGoal));

        // 몸통과 머리가 목표 방향에서 얼마나 틀어져 있는지(회전 차이)
        sensor.AddObservation(Quaternion.FromToRotation(hips.forward, cubeForward));
        sensor.AddObservation(Quaternion.FromToRotation(head.forward, cubeForward));

        // 목표물의 위치 (큐브 기준 상대 좌표)
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformPoint(target.transform.position));

        // 모든 신체 부위의 상태를 추가
        foreach (var bodyPart in m_JdController.bodyPartsList)
        {
            CollectObservationBodyPart(bodyPart, sensor);
        }
    }

    /// <summary>
    /// 신경망이 결정한 행동값을 실제 동작으로 옮기는 곳.
    /// continuousActions는 -1 ~ 1 사이의 연속값 배열이며, 순서대로 꺼내 쓴다.
    /// ++i 를 써서 인덱스를 하나씩 증가시키며 읽는 방식(총 39개 사용).
    /// ※ 여기서 꺼내 쓰는 개수와 Behavior Parameters의 Continuous Actions 수가 반드시 같아야 한다.
    /// </summary>
    public override void OnActionReceived(ActionBuffers actionBuffers)

    {
        var bpDict = m_JdController.bodyPartsDict;
        var i = -1;

        var continuousActions = actionBuffers.ContinuousActions;

        // --- (1) 각 관절의 목표 회전각 설정 (X, Y, Z축 순서) ---
        // 축이 0으로 고정된 것은 실제 인체처럼 그 방향으로는 꺾이지 않기 때문이다.
        // 예: 무릎(shin)과 팔꿈치(forearm)는 한 축으로만 굽혀지므로 값 1개만 사용.
        bpDict[chest].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], continuousActions[++i]);
        bpDict[spine].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], continuousActions[++i]);

        bpDict[thighL].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);
        bpDict[thighR].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);
        bpDict[shinL].SetJointTargetRotation(continuousActions[++i], 0, 0);
        bpDict[shinR].SetJointTargetRotation(continuousActions[++i], 0, 0);
        bpDict[footR].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], continuousActions[++i]);
        bpDict[footL].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], continuousActions[++i]);

        bpDict[armL].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);
        bpDict[armR].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);
        bpDict[forearmL].SetJointTargetRotation(continuousActions[++i], 0, 0);
        bpDict[forearmR].SetJointTargetRotation(continuousActions[++i], 0, 0);
        bpDict[head].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);

        // --- (2) 각 관절에 들어갈 "힘의 세기" 설정 ---
        // 회전 방향뿐 아니라 얼마나 강하게 버틸지도 에이전트가 직접 학습한다.
        // (착지 순간엔 강하게, 다리를 휘두를 땐 약하게 같은 조절이 가능해진다)
        bpDict[chest].SetJointStrength(continuousActions[++i]);
        bpDict[spine].SetJointStrength(continuousActions[++i]);
        bpDict[head].SetJointStrength(continuousActions[++i]);
        bpDict[thighL].SetJointStrength(continuousActions[++i]);
        bpDict[shinL].SetJointStrength(continuousActions[++i]);
        bpDict[footL].SetJointStrength(continuousActions[++i]);
        bpDict[thighR].SetJointStrength(continuousActions[++i]);
        bpDict[shinR].SetJointStrength(continuousActions[++i]);
        bpDict[footR].SetJointStrength(continuousActions[++i]);
        bpDict[armL].SetJointStrength(continuousActions[++i]);
        bpDict[forearmL].SetJointStrength(continuousActions[++i]);
        bpDict[armR].SetJointStrength(continuousActions[++i]);
        bpDict[forearmR].SetJointStrength(continuousActions[++i]);
    }

    // 기준 큐브와 방향 표시 화살표를 목표 방향에 맞춰 갱신
    void UpdateOrientationObjects()
    {
        m_WorldDirToWalk = target.position - hips.position;
        m_OrientationCube.UpdateOrientation(hips, target);
        if (m_DirectionIndicator)
        {
            m_DirectionIndicator.MatchOrientation(m_OrientationCube.transform);
        }
    }

    /// <summary>
    /// 물리 갱신 주기마다 호출된다. 여기서 보상(Reward)을 계산해 준다.
    /// 강화학습에서 가장 중요한 부분으로, "무엇을 잘한 것으로 칠지"를 정의하는 곳이다.
    /// </summary>
    void FixedUpdate()
    {
        UpdateOrientationObjects();

        var cubeForward = m_OrientationCube.transform.forward;

        // 보상은 아래 두 요소를 곱해서 만든다.
        // a. 목표 속도를 얼마나 잘 맞췄는가
        //    완벽히 일치하면 1에 가까워지고, 어긋날수록 0에 가까워진다.
        var matchSpeedReward = GetMatchingVelocityReward(cubeForward * MTargetWalkingSpeed, GetAvgVelocity());

        // NaN(계산 불가 값) 검사.
        // 물리 연산이 폭주하면 값이 깨질 수 있는데, 그대로 두면 학습 전체가 망가지므로 즉시 에러를 낸다.
        if (float.IsNaN(matchSpeedReward))
        {
            throw new ArgumentException(
                "NaN in moveTowardsTargetReward.\n" +
                $" cubeForward: {cubeForward}\n" +
                $" hips.velocity: {m_JdController.bodyPartsDict[hips].rb.linearVelocity}\n" +
                $" maximumWalkingSpeed: {m_maxWalkingSpeed}"
            );
        }

        // b. 목표 방향을 얼마나 잘 바라보고 있는가
        //    Dot(내적)은 두 방향이 같으면 1, 반대면 -1이다.
        //    (Dot + 1) * 0.5 를 하면 0 ~ 1 범위로 바뀐다.
        //    y를 0으로 만드는 이유: 고개를 위아래로 드는 건 무시하고 좌우 방향만 보겠다는 뜻.
        var headForward = head.forward;
        headForward.y = 0;
        // var lookAtTargetReward = (Vector3.Dot(cubeForward, head.forward) + 1) * .5F;
        var lookAtTargetReward = (Vector3.Dot(cubeForward, headForward) + 1) * .5F;

        //NaN 검사
        if (float.IsNaN(lookAtTargetReward))
        {
            throw new ArgumentException(
                "NaN in lookAtTargetReward.\n" +
                $" cubeForward: {cubeForward}\n" +
                $" head.forward: {head.forward}"
            );
        }

        // 두 보상을 "곱한다"는 점이 핵심.
        // 더하기였다면 방향을 무시하고 속도만 챙기는 식의 꼼수가 가능하지만,
        // 곱하기이면 둘 중 하나라도 0에 가까우면 전체 보상이 0이 된다.
        // → 올바른 방향을 보면서 동시에 목표 속도로 움직여야만 높은 점수를 받는다.
        AddReward(matchSpeedReward * lookAtTargetReward);
    }

    // 모든 신체 부위의 평균 속도를 반환한다.
    // 골반 속도만 쓰면 팔다리가 제멋대로 흔들려도 점수를 받을 수 있어서,
    // 평균을 사용해 몸 전체가 고르게 이동하도록 유도한다.
    Vector3 GetAvgVelocity()
    {
        Vector3 velSum = Vector3.zero;

        // 모든 Rigidbody의 속도를 합산
        int numOfRb = 0;
        foreach (var item in m_JdController.bodyPartsList)
        {
            numOfRb++;
            velSum += item.rb.linearVelocity;
        }

        var avgVel = velSum / numOfRb;
        return avgVel;
    }

    /// <summary>
    /// 목표 속도와 실제 속도의 차이를 0~1 사이의 보상값으로 바꿔 준다.
    /// 1에 가까울수록 목표 속도를 잘 따라가고 있다는 뜻.
    /// </summary>
    public float GetMatchingVelocityReward(Vector3 velocityGoal, Vector3 actualVelocity)
    {
        // 목표 속도와 실제 속도의 차이. 최대 목표속도를 넘지 않게 Clamp로 잘라낸다.
        var velDeltaMagnitude = Mathf.Clamp(Vector3.Distance(actualVelocity, velocityGoal), 0, MTargetWalkingSpeed);

        // 1에서 0으로 완만하게 떨어지는 곡선(시그모이드 형태) 값을 돌려준다.
        // 단순 비례(선형)보다 이런 곡선이 학습 신호를 더 부드럽게 만들어 준다.
        return Mathf.Pow(1 - Mathf.Pow(velDeltaMagnitude / MTargetWalkingSpeed, 2), 2);
    }

    /// <summary>
    /// 목표물에 닿았을 때 호출된다(목표물 쪽 스크립트에서 불러 줌).
    /// 매 스텝 주는 작은 보상과 달리, 목표 달성 시 주는 큰 일회성 보너스다.
    /// </summary>
    public void TouchedTarget()
    {
        AddReward(1f);
    }
}
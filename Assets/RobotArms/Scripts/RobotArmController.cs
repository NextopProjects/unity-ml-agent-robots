using UnityEngine;

namespace RobotArms
{
    /// <summary>
    /// 로봇 팔의 "몸"을 담당한다. (챕터 2)
    ///
    /// 이 스크립트는 강화학습을 전혀 모른다. 그냥
    ///   "관절을 이만큼 돌려라"
    /// 라는 명령을 받아서 Transform을 회전시킬 뿐이다.
    ///
    /// 챕터 2에서는 <see cref="ManualArmDriver"/>(키보드)가 이 명령을 내리고,
    /// 챕터 3부터는 <see cref="RobotArmAgent"/>(AI)가 같은 명령을 내린다.
    /// 팔 입장에서는 누가 명령했는지 구분하지 않는다. 그래서 그대로 재사용된다.
    /// </summary>
    public class RobotArmController : MonoBehaviour
    {
        /// <summary>관절 하나의 설정과 현재 상태.</summary>
        [System.Serializable]
        public class Joint
        {
            [Tooltip("화면에 표시할 이름. 예: 어깨, 팔꿈치, 좌우 회전")]
            public string label = "관절";

            [Tooltip("회전만 담당하는 빈 오브젝트. 이 아래에 링크(막대)가 달린다.")]
            public Transform pivot;

            [Tooltip("회전축. 기본값 (-1,0,0)이면 각도가 +일 때 팔이 위로 올라간다.")]
            public Vector3 axis = Vector3.left;

            [Tooltip("이 관절이 돌 수 있는 최소 각도(도).")]
            public float minAngle = -90f;

            [Tooltip("이 관절이 돌 수 있는 최대 각도(도).")]
            public float maxAngle = 90f;

            [Tooltip("초당 최대 몇 도까지 돌 수 있는가.")]
            public float maxSpeed = 120f;

            [Tooltip("에피소드가 시작될 때 돌아갈 각도.")]
            public float startAngle = 0f;

            /// <summary>지금 각도(도). 이 값이 팔의 진짜 상태다.</summary>
            public float Angle { get; private set; }

            /// <summary>지금 각속도(도/초). 관측에 쓴다.</summary>
            public float Velocity { get; private set; }

            internal void ResetTo(float angle)
            {
                Angle = Mathf.Clamp(angle, minAngle, maxAngle);
                Velocity = 0f;
                Apply();
            }

            internal void Drive(float normalizedSpeed, float deltaTime)
            {
                // 입력을 "각도"가 아니라 "속도"로 받아서 조금씩 더해 나간다.
                // 그래야 한 프레임에 팔이 순간이동하듯 튀지 않는다.
                float before = Angle;
                float delta = Mathf.Clamp(normalizedSpeed, -1f, 1f) * maxSpeed * deltaTime;

                Angle = Mathf.Clamp(Angle + delta, minAngle, maxAngle);
                Velocity = deltaTime > 0f ? (Angle - before) / deltaTime : 0f;

                Apply();
            }

            void Apply()
            {
                if (pivot != null)
                {
                    pivot.localRotation = Quaternion.AngleAxis(Angle, axis);
                }
            }
        }

        [Header("관절")]
        [Tooltip("어깨부터 순서대로 넣는다. 관절 개수 = DOF = 액션 개수.")]
        public Joint[] joints = new Joint[0];

        [Header("팔 끝")]
        [Tooltip("물체를 집는 지점.")]
        public Transform tip;

        [Header("크기")]
        [Tooltip("팔이 닿을 수 있는 최대 거리. 링크 길이의 합. 관측 정규화에 쓴다.")]
        public float reach = 2.7f;

        /// <summary>관절 개수 = 자유도(DOF).</summary>
        public int JointCount => joints.Length;

        void Awake()
        {
            ResetPose();
        }

        /// <summary>모든 관절을 시작 각도로 되돌린다.</summary>
        public void ResetPose()
        {
            foreach (var j in joints)
            {
                j.ResetTo(j.startAngle);
            }
        }

        /// <summary>
        /// i번 관절을 돌린다. <paramref name="normalizedSpeed"/>는 -1 ~ 1.
        /// 키보드 입력이든 AI의 액션이든 이 함수 하나로 들어온다.
        ///
        /// 돌린 결과 팔이 테이블을 뚫거나 물체를 관통하면 <b>그 움직임을 되돌린다.</b>
        /// 관절 하나하나의 min/max만으로는 이런 상황을 막을 수 없기 때문이다.
        /// (자세한 이유는 <see cref="IsBlocked"/> 참고)
        /// </summary>
        public void Drive(int i, float normalizedSpeed, float deltaTime)
        {
            // 움직이기 "전"에 이미 막힌 상태였는지 기억해둔다.
            // 어쩌다 막힌 자세로 들어갔을 때 영영 못 움직이게 되는 것을 막기 위해서다.
            bool wasBlocked = blockPenetration && IsBlocked();
            float before = joints[i].Angle;

            joints[i].Drive(normalizedSpeed, deltaTime);

            if (blockPenetration && !wasBlocked && IsBlocked())
            {
                // 이번 움직임 때문에 뚫렸다 → 되돌린다.
                // ResetTo는 각속도도 0으로 만든다. 벽에 닿아 멈춘 것처럼 보인다.
                joints[i].ResetTo(before);
                _lastBlockedTime = Time.time;
            }
        }

        float _lastBlockedTime = -99f;

        /// <summary>방금 전에 바닥·물체 때문에 움직임이 막혔는가. 화면 표시에 쓴다.</summary>
        public bool BlockedRecently => Time.time - _lastBlockedTime < 0.25f;

        // ── 관측용 값들 ──────────────────────────────────────────────
        // 전부 -1 ~ 1 근처로 맞춰서 내보낸다(정규화).
        // 각도(±180)와 위치(±2.7)처럼 단위가 다른 값을 그대로 섞으면
        // 크기가 큰 값이 학습을 지배해버리기 때문이다.

        /// <summary>i번 관절의 각도를 -1 ~ 1로 정규화한 값.</summary>
        public float NormalizedAngle(int i) => joints[i].Angle / 180f;

        /// <summary>i번 관절의 각속도를 -1 ~ 1로 정규화한 값.</summary>
        public float NormalizedVelocity(int i) => joints[i].Velocity / joints[i].maxSpeed;

        /// <summary>월드 좌표를 팔 받침대 기준 로컬 좌표로 바꾼다.</summary>
        public Vector3 ToLocal(Vector3 worldPosition) => transform.InverseTransformPoint(worldPosition);

        /// <summary>월드 좌표를 받침대 기준으로 바꾸고 reach로 나눈 값. 관측에 바로 쓴다.</summary>
        public Vector3 ToNormalizedLocal(Vector3 worldPosition) => ToLocal(worldPosition) / reach;

        /// <summary>팔 끝의 월드 좌표.</summary>
        public Vector3 TipPosition => tip != null ? tip.position : transform.position;

        // ── 바닥·물체 통과 방지 ──────────────────────────────────────
        //
        // 왜 필요한가
        //   관절마다 min/max 각도를 정해두었지만, 그것만으로는 부족하다.
        //   어깨를 20도로 두고 팔꿈치를 -120도로 접으면, 두 각도 모두
        //   각자의 범위 안에 있는데도 팔 끝은 테이블 아래로 내려간다.
        //
        //   → <b>가동 범위는 관절 하나만으로 정해지지 않는다.
        //      관절들의 "조합"에 따른 제약이 따로 있다.</b>
        //
        //   실제 로봇에서도 마찬가지다. 각 축의 리미트와 별개로
        //   "작업대를 치지 않는 자세"인지를 따로 확인한다.

        [Header("바닥·물체 통과 방지")]
        [Tooltip("켜면 팔이 테이블 아래로 내려가거나 물체를 관통하지 못하게 막는다.")]
        public bool blockPenetration = true;

        [Tooltip("바닥(테이블 윗면) 높이. 받침대 기준 y 좌표.")]
        public float floorHeight = 0f;

        [Tooltip("바닥에서 이만큼 띄운다. 링크의 굵기와 팔 끝 구의 반지름을 감안한 여유.")]
        public float clearance = 0.07f;

        [Tooltip("뚫고 지나가면 안 되는 물체들. 보통 Object 하나를 넣는다.")]
        public Transform[] obstacles = new Transform[0];

        [Tooltip("물체를 감싸는 구의 반지름. 이 안으로 팔이 들어오면 막는다.")]
        public float obstacleRadius = 0.16f;

        [Tooltip("팔 끝에서 이 거리 안쪽은 물체 검사에서 제외한다. 물체를 집으러 가야 하기 때문.")]
        public float tipExemptDistance = 0.28f;

        [Tooltip("링크 하나를 몇 점으로 쪼개서 검사할지. 높이면 정확해지고 느려진다.")]
        public int samplesPerLink = 6;

        Transform[] _chain;

        /// <summary>
        /// 팔 끝에서 받침대 쪽으로 거슬러 올라가며 관절 순서를 모은다.
        /// 결과는 [J0, J1, J2, Tip] 처럼 "이어진 뼈대" 순서가 된다.
        /// 이 점들을 이은 선분이 곧 팔의 몸통이다.
        /// </summary>
        Transform[] Chain
        {
            get
            {
                if (_chain != null && _chain.Length > 0) return _chain;
                if (tip == null) return _chain = new Transform[0];

                var list = new System.Collections.Generic.List<Transform>();
                for (var t = tip; t != null && t != transform; t = t.parent) list.Add(t);
                list.Reverse();

                return _chain = list.ToArray();
            }
        }

        /// <summary>지금 자세가 바닥을 뚫거나 물체를 관통하고 있는가.</summary>
        public bool IsBlocked()
        {
            var chain = Chain;
            if (chain.Length < 2) return false;

            float minY = floorHeight + clearance;
            Vector3 tipPos = TipPosition;

            for (int seg = 0; seg < chain.Length - 1; seg++)
            {
                Vector3 a = chain[seg].position;
                Vector3 b = chain[seg + 1].position;

                for (int s = 0; s <= samplesPerLink; s++)
                {
                    Vector3 p = Vector3.Lerp(a, b, s / (float)samplesPerLink);

                    // 바닥 검사
                    if (ToLocal(p).y < minY) return true;

                    // 물체 검사 — 팔 끝 근처는 제외한다.
                    // 그렇지 않으면 물체를 집으러 다가가는 것 자체가 막힌다.
                    if (Vector3.Distance(p, tipPos) <= tipExemptDistance) continue;

                    foreach (var o in obstacles)
                    {
                        if (o == null) continue;
                        if (Vector3.Distance(p, o.position) < obstacleRadius) return true;
                    }
                }
            }

            return false;
        }

        // ── 도달 범위 그리기 ─────────────────────────────────────────

        [Header("물체가 나타나는 구간")]
        [Tooltip("받침대에서 떨어진 거리. 최소값은 팔이 접혔을 때 닿는 거리보다 커야 한다.")]
        public Vector2 spawnDistanceRange = new Vector2(1.2f, 2.4f);

        [Tooltip("좌우 각도 범위(도). (0, 0)이면 정면 한 줄. 좌우 회전 관절(J0)이 생기면 넓힌다.")]
        public Vector2 spawnYawRange = new Vector2(0f, 0f);

        [Tooltip("물체가 테이블 위에 놓였을 때의 높이. (받침대 기준 y 좌표)")]
        public float spawnHeight = 0.1f;

        /// <summary>거리와 좌우 각도로 테이블 위의 한 점을 구한다. (받침대 기준 로컬 좌표)</summary>
        public Vector3 SpawnPointLocal(float distance, float yawDegrees)
        {
            float yaw = yawDegrees * Mathf.Deg2Rad;
            return new Vector3(
                Mathf.Sin(yaw) * distance,
                spawnHeight,
                Mathf.Cos(yaw) * distance);
        }

        [Header("Gizmo")]
        [Tooltip("Scene 뷰에 팔이 닿는 범위를 그린다.")]
        public bool drawReachGizmo = true;

        void OnDrawGizmos()
        {
            if (!drawReachGizmo) return;

            Vector3 shoulder = joints.Length > 0 && joints[0].pivot != null
                ? joints[0].pivot.position
                : transform.position;

            // 흰 구 = 팔이 닿을 수 있는 최대 범위
            Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(shoulder, reach);

            // 초록 = 물체가 나타나는 구역. 반드시 흰 구 안에 들어 있어야 한다.
            Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.9f);
            DrawSpawnArc(spawnDistanceRange.x);
            DrawSpawnArc(spawnDistanceRange.y);
            DrawSpawnRadial(spawnYawRange.x);
            DrawSpawnRadial(spawnYawRange.y);

            if (!blockPenetration) return;

            // 빨강 = 팔이 내려갈 수 있는 한계 높이. 이 아래로는 못 내려간다.
            Gizmos.color = new Color(1f, 0.35f, 0.3f, 0.8f);
            DrawHeightCircle(floorHeight + clearance, reach);

            // 빨강 구 = 뚫고 지나갈 수 없는 물체
            foreach (var o in obstacles)
            {
                if (o != null) Gizmos.DrawWireSphere(o.position, obstacleRadius);
            }
        }

        void DrawHeightCircle(float localY, float radius)
        {
            const int steps = 48;
            Vector3 prev = Vector3.zero;

            for (int i = 0; i <= steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2f;
                Vector3 p = transform.TransformPoint(
                    new Vector3(Mathf.Sin(a) * radius, localY, Mathf.Cos(a) * radius));

                if (i > 0) Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }

        void DrawSpawnArc(float distance)
        {
            const int steps = 32;
            Vector3 prev = Vector3.zero;

            for (int i = 0; i <= steps; i++)
            {
                float yaw = Mathf.Lerp(spawnYawRange.x, spawnYawRange.y, i / (float)steps);
                Vector3 p = transform.TransformPoint(SpawnPointLocal(distance, yaw));

                if (i > 0) Gizmos.DrawLine(prev, p);
                prev = p;
            }

            // 좌우 각도 범위가 0이면 호가 점이 되므로, 구간 끝을 구슬로 표시한다.
            if (Mathf.Approximately(spawnYawRange.x, spawnYawRange.y))
            {
                Gizmos.DrawSphere(prev, 0.05f);
            }
        }

        void DrawSpawnRadial(float yawDegrees)
        {
            Gizmos.DrawLine(
                transform.TransformPoint(SpawnPointLocal(spawnDistanceRange.x, yawDegrees)),
                transform.TransformPoint(SpawnPointLocal(spawnDistanceRange.y, yawDegrees)));
        }
    }
}

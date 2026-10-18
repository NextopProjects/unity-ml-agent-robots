using UnityEngine;

namespace Humanoid
{
    // 몸 · 보상 계산에 쓰는 함수 모음. 입력을 받아 값을 돌려주기만 해서 Play 없이 테스트할 수 있다.
    public static class HumanoidMath
    {
        // ══ 무게중심 ══════════════════════════════════════════════════════

        // 부품 위치와 질량으로 몸 전체의 무게중심을 구한다. 부품이 없으면 (0,0,0).
        public static Vector3 CenterOfMass(Vector3[] positions, float[] masses)
        {
            Vector3 sum = Vector3.zero;
            float totalMass = 0f;
            for (int i = 0; i < positions.Length; i++)
            {
                sum += positions[i] * masses[i];
                totalMass += masses[i];
            }
            return totalMass > 0f ? sum / totalMass : Vector3.zero;
        }

        // ══ 관절 각도 ══════════════════════════════════════════════════════

        // 행동(-1~1)을 관절 목표 각도(도)로 바꾼다. 행동 0 = 0도(차렷).
        public static float ActionToAngle(float action, float low, float high)
        {
            action = Mathf.Clamp(action, -1f, 1f);
            return action >= 0f ? action * high : -action * low;
        }

        // 관절 각도(도)를 -1~1 로 바꾼다. ActionToAngle 의 거꾸로.
        public static float AngleToAction(float angle, float low, float high)
        {
            float range = angle >= 0f ? high : -low;
            if (range <= 0f) return 0f;
            return Mathf.Clamp(angle / range, -1f, 1f);
        }

        // ══ 서기 ══════════════════════════════════════════════════════

        // 세상의 아래(0,-1,0)를 몸 기준 좌표로 바꾼다. 사람의 귓속 평형감각과 같은 정보다.
        public static Vector3 DownInBody(Quaternion bodyRotation)
        {
            return Quaternion.Inverse(bodyRotation) * Vector3.down;
        }

        // 서 있는 정도를 0~1 점수로 준다.
        public static float StandReward(Vector3 up, float height, float standHeight)
        {
            return Mathf.Clamp01(up.y) * Mathf.Clamp01(height / standHeight);
        }

        // ══ 걷기 ══════════════════════════════════════════════════════

        // 바라보는 방향에서 좌우 회전만 남긴 회전. 몸이 앞뒤로 기울어도 보는 방향은 그대로여야 한다.
        public static Quaternion HeadingRotation(Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) return Quaternion.identity;
            return Quaternion.LookRotation(forward.normalized);
        }

        // 목표 속도로 목표 쪽으로 갈수록 1에 가까운 점수 (0~1).
        public static float SpeedReward(Vector3 velocity, Vector3 direction, float targetSpeed)
        {
            velocity.y = 0f;
            float d = Mathf.Clamp((velocity - direction * targetSpeed).magnitude, 0f, targetSpeed);
            float r = 1f - (d / targetSpeed) * (d / targetSpeed);
            return r * r;
        }

        // 몸이 목표 쪽을 볼수록 1에 가까운 점수 (0~1).
        public static float FacingReward(Vector3 forward, Vector3 direction)
        {
            forward.y = 0f;
            direction.y = 0f;
            return (Vector3.Dot(forward.normalized, direction.normalized) + 1f) * 0.5f;
        }
    }

    // ══ 평가 ══════════════════════════════════════════════════════

    // 판 하나의 속도 기록. WalkAgent 가 매 스텝 Add 하고, 판이 끝나면 Average · Error 를 남긴 뒤 Clear 한다.
    public class SpeedStats
    {
        public int Count { get; private set; }
        public float Average { get; private set; }

        public void Add(float value)
        {
            Count++;
            Average += (value - Average) / Count;     // 누적 평균: 합계를 들고 있지 않아도 된다
        }

        public void Clear()
        {
            Count = 0;
            Average = 0f;
        }

        public float Error(float targetSpeed)
        {
            if (targetSpeed <= 0f) return 0f;
            return Mathf.Abs(Average - targetSpeed) / targetSpeed;
        }
    }
}

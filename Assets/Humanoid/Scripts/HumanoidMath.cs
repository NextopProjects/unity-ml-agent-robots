using UnityEngine;

namespace Humanoid
{
    /// <summary>
    /// 빈칸 실습 함수 모음. 입력을 받아 값을 돌려주는 함수뿐이라 Play 없이 채점된다.
    /// (튜토리얼 창의 코드 단계에서 [빈칸 만들기] → [채점하기])
    ///
    /// "// ▼ 빈칸 N-k" ~ "// ▲ 빈칸 N-k | 임시값" 줄은 지우지 않는다. 빈칸 도구가 이 표시로 자리를 찾는다.
    /// </summary>
    public static class HumanoidMath
    {
        // ══ 1장 ══════════════════════════════════════════════════════

        /// <summary>[빈칸 1-1] 무게중심. (m1·p1 + m2·p2 + ...) / (m1 + m2 + ...)  질량 합이 0이면 (0,0,0)</summary>
        public static Vector3 CenterOfMass(Vector3[] positions, float[] masses)
        {
            // ▼ 빈칸 1-1
            Vector3 sum = Vector3.zero;
            float totalMass = 0f;
            for (int i = 0; i < positions.Length; i++)
            {
                sum += positions[i] * masses[i];
                totalMass += masses[i];
            }
            return totalMass > 0f ? sum / totalMass : Vector3.zero;
            // ▲ 빈칸 1-1 | return Vector3.zero;
        }

        // ══ 2장 ══════════════════════════════════════════════════════

        /// <summary>
        /// [빈칸 2-1] 행동(-1~1) → 목표 각도(도).  -1 → low,  0 → 0도(차렷),  +1 → high
        /// 범위 밖 행동은 -1~1로 자른다.
        /// </summary>
        public static float ActionToAngle(float action, float low, float high)
        {
            // ▼ 빈칸 2-1
            action = Mathf.Clamp(action, -1f, 1f);
            return action >= 0f ? action * high : -action * low;
            // ▲ 빈칸 2-1 | return 0f;
        }

        /// <summary>
        /// [빈칸 2-2] 각도(도) → -1~1.  ActionToAngle 의 거꾸로. 결과는 -1~1로 자른다.
        /// 나눌 범위가 0이면(무릎의 high) 0을 돌려준다. 0으로 나누면 NaN이 된다.
        /// </summary>
        public static float AngleToAction(float angle, float low, float high)
        {
            // ▼ 빈칸 2-2
            float range = angle >= 0f ? high : -low;
            if (range <= 0f) return 0f;
            return Mathf.Clamp(angle / range, -1f, 1f);
            // ▲ 빈칸 2-2 | return 0f;
        }

        // ══ 3장 ══════════════════════════════════════════════════════

        /// <summary>
        /// [빈칸 3-1] 몸이 느끼는 "아래쪽" — 세상의 아래(0,-1,0)를 몸 기준 좌표로 바꾼다.
        /// 똑바로 서면 (0,-1,0), 앞으로 기울면 z가 생긴다. 사람의 귓속 평형감각과 같은 정보다.
        /// </summary>
        public static Vector3 DownInBody(Quaternion bodyRotation)
        {
            // ▼ 빈칸 3-1
            return Quaternion.Inverse(bodyRotation) * Vector3.down;
            // ▲ 빈칸 3-1 | return Vector3.zero;
        }

        /// <summary>
        /// [빈칸 3-2] 서기 보상(0~1) = 똑바른 정도 × 높이 비율.
        ///   똑바른 정도 = up.y (위를 보면 1, 누우면 0) 를 0~1로 자른 값
        ///   높이 비율   = height / standHeight 를 0~1로 자른 값
        /// 더하지 않고 곱한다: 둘 중 하나라도 0이면 0점. (쪼그려 앉아 똑바로 있기, 기운 채 높이 있기를 막는다)
        /// </summary>
        public static float StandReward(Vector3 up, float height, float standHeight)
        {
            // ▼ 빈칸 3-2
            return Mathf.Clamp01(up.y) * Mathf.Clamp01(height / standHeight);
            // ▲ 빈칸 3-2 | return 0f;
        }
    }
}

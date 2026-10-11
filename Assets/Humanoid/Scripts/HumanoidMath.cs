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
    }
}

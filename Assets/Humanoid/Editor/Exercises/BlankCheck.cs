using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using M = Humanoid.HumanoidMath;

namespace HumanoidEditor
{
    /// <summary>
    /// 빈칸 채점기. 학생이 채운 함수를 실제로 불러서 기대값과 비교한다. (문자열은 보지 않는다)
    /// 케이스 규칙: ① 임시값에서는 하나 이상 실패해야 한다  ② 이름에 (경계)가 붙은 케이스를 넣는다
    /// </summary>
    internal static class BlankCheck
    {
        public struct Result { public string label, expected, actual; public bool pass; }

        static readonly List<(string id, string label, object expected, Func<object> actual)> Cases =
            new List<(string, string, object, Func<object>)>
        {
            ("1-1", "같은 무게 둘 → 가운데", new Vector3(1, 0, 0), () => M.CenterOfMass(new[] { Vector3.zero, new Vector3(2, 0, 0) }, new[] { 1f, 1f })),
            ("1-1", "3kg 위 · 1kg 아래 → 무거운 쪽으로", new Vector3(0, 1.5f, 0), () => M.CenterOfMass(new[] { new Vector3(0, 2, 0), Vector3.zero }, new[] { 3f, 1f })),
            ("1-1", "부품 하나 (경계) → 그 자리", new Vector3(0.3f, 0.9f, -0.2f), () => M.CenterOfMass(new[] { new Vector3(0.3f, 0.9f, -0.2f) }, new[] { 8f })),
            ("1-1", "부품 없음 (경계) → (0,0,0)", Vector3.zero, () => M.CenterOfMass(new Vector3[0], new float[0])),

            ("2-1", "엉덩이(-30~90) · +1 → 90", 90f, () => M.ActionToAngle(1f, -30f, 90f)),
            ("2-1", "엉덩이 · -0.5 → -15", -15f, () => M.ActionToAngle(-0.5f, -30f, 90f)),
            ("2-1", "행동 0 (경계) → 0도 차렷", 0f, () => M.ActionToAngle(0f, -30f, 90f)),          // 범위 가운데로 보내는 방식이면 30
            ("2-1", "행동 3 (경계: 범위 밖) → 90", 90f, () => M.ActionToAngle(3f, -30f, 90f)),
            ("2-1", "무릎(-120~0) · +1 (경계) → 0", 0f, () => M.ActionToAngle(1f, -120f, 0f)),
            ("2-2", "무릎 -60도 → -0.5", -0.5f, () => M.AngleToAction(-60f, -120f, 0f)),
            ("2-2", "엉덩이 45도 → 0.5", 0.5f, () => M.AngleToAction(45f, -30f, 90f)),
            ("2-2", "한계를 넘은 -40도 (경계) → -1", -1f, () => M.AngleToAction(-40f, -30f, 90f)),
            ("2-2", "무릎 +3도 (경계: high=0) → 0, NaN 아님", 0f, () => M.AngleToAction(3f, -120f, 0f)),

            ("3-1", "똑바로 → (0,-1,0)", Vector3.down, () => M.DownInBody(Quaternion.identity)),
            ("3-1", "앞으로 60도 숙임 → 아래가 몸의 앞쪽(+z)", new Vector3(0, -0.5f, 0.866f), () => M.DownInBody(Quaternion.Euler(60, 0, 0))),  // rot*down 이면 z가 음수
            ("3-1", "거꾸로 (경계) → (0,1,0)", Vector3.up, () => M.DownInBody(Quaternion.Euler(180, 0, 0))),
            ("3-2", "똑바로 · 제 높이 → 1", 1f, () => M.StandReward(Vector3.up, 0.96f, 0.96f)),
            ("3-2", "똑바로 · 절반 높이 → 0.5", 0.5f, () => M.StandReward(Vector3.up, 0.48f, 0.96f)),
            ("3-2", "누움 (경계) → 0", 0f, () => M.StandReward(Vector3.forward, 0.96f, 0.96f)),           // 더하기면 0.5
            ("3-2", "거꾸로 (경계: up.y<0) → 0", 0f, () => M.StandReward(Vector3.down, 0.96f, 0.96f)),
            ("3-2", "더 높이 뜀 (경계) → 1을 넘지 않음", 1f, () => M.StandReward(Vector3.up, 1.5f, 0.96f)),

            ("4-1", "정면 → 회전 없음", Quaternion.identity, () => M.HeadingRotation(Vector3.forward)),
            ("4-1", "오른쪽(+x)을 봄 → 좌우 90도", Quaternion.Euler(0, 90, 0), () => M.HeadingRotation(Vector3.right)),
            ("4-1", "숙인 채 오른쪽 → 좌우 90도만", Quaternion.Euler(0, 90, 0), () => M.HeadingRotation(new Vector3(0.5f, -0.8f, 0f))),
            ("4-1", "바로 아래를 봄 (경계) → 회전 없음", Quaternion.identity, () => M.HeadingRotation(Vector3.down)),
            ("4-2", "목표 속도 그대로 → 1", 1f, () => M.SpeedReward(Vector3.forward, Vector3.forward, 1f)),
            ("4-2", "멈춤 → 0", 0f, () => M.SpeedReward(Vector3.zero, Vector3.forward, 1f)),
            ("4-2", "절반 속도 → 0.5625", 0.5625f, () => M.SpeedReward(Vector3.forward * 0.5f, Vector3.forward, 1f)),
            ("4-2", "반대로 달림 (경계) → 0", 0f, () => M.SpeedReward(Vector3.back * 2f, Vector3.forward, 1f)),
            ("4-2", "떨어지는 속도는 무시 (경계) → 1", 1f, () => M.SpeedReward(new Vector3(0, -3, 1), Vector3.forward, 1f)),
            ("4-3", "같은 쪽 → 1", 1f, () => M.FacingReward(Vector3.forward, Vector3.forward)),
            ("4-3", "옆 → 0.5", 0.5f, () => M.FacingReward(Vector3.right, Vector3.forward)),
            ("4-3", "반대 → 0", 0f, () => M.FacingReward(Vector3.back, Vector3.forward)),
            ("4-3", "고개 숙임 (경계) → 1", 1f, () => M.FacingReward(new Vector3(0, -1, 1), Vector3.forward)),

        };

        /// <summary>id가 prefix로 시작하는 케이스를 실행한다. ("2-1" = 빈칸 하나, "" = 전부)</summary>
        public static List<Result> Grade(string prefix)
        {
            var results = new List<Result>();
            foreach (var c in Cases)
            {
                if (!c.id.StartsWith(prefix)) continue;
                object value;
                try { value = c.actual(); }
                catch (Exception e) { value = e.GetType().Name; }   // 학생 코드가 터져도 채점은 계속한다

                bool pass = value is float f ? c.expected is float g && Mathf.Abs(f - g) < 1e-3f
                          : value is Vector3 v ? c.expected is Vector3 w && (v - w).magnitude < 1e-3f
                          : value is Quaternion q ? c.expected is Quaternion r && Quaternion.Angle(q, r) < 0.1f
                          : Equals(value, c.expected);
                results.Add(new Result { label = c.label, expected = Show(c.expected), actual = Show(value), pass = pass });
            }
            return results;
        }

        static string Show(object o) =>
            o is float f ? (float.IsNaN(f) ? "NaN" : f.ToString("0.###"))
          : o is Quaternion q ? "좌우 " + q.eulerAngles.y.ToString("0") + "° 앞뒤 " + Mathf.DeltaAngle(0, q.eulerAngles.x).ToString("0") + "°"
          : o is Vector3 v ? v.ToString("0.##")
          : o?.ToString() ?? "null";

        // 개발자용: 정답 상태에서 전부 통과하는지
        [MenuItem("Humanoid/개발자/전체 채점", false, 302)]
        static void RunAll()
        {
            var failed = Grade("").FindAll(r => !r.pass);
            if (failed.Count == 0) Debug.Log("[Humanoid] 전체 채점 — " + Cases.Count + "개 케이스 전부 통과");
            else Debug.LogWarning("[Humanoid] 전체 채점 — 실패 " + failed.Count + "개: " + string.Join(", ", failed.ConvertAll(r => r.label)));
        }
    }
}

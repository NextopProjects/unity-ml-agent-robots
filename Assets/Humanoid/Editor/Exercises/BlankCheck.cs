using System;
using System.Collections.Generic;
using System.Linq;
using Humanoid;
using UnityEditor;
using UnityEngine;
using M = Humanoid.HumanoidMath;

namespace HumanoidEditor
{
    // 실습 테스트. 학습자가 작성한 함수·클래스를 실제로 불러서 기대값과 비교한다. (코드 문자열은 보지 않는다)
    // 케이스 하나 = 요구사항 하나를 확인하는 입력. 실패하면 그 요구사항의 수정 방향(fix)을 보여 준다.
    // 규칙: ① 시작 코드에서는 하나 이상 실패해야 한다  ② 이름에 (경계)가 붙은 케이스를 넣는다
    internal static class BlankCheck
    {
        public struct Result { public string req, label, expected, actual, fix, why; public bool pass; }

        struct Case { public string id, req, label, fix; public object expected; public Func<object> actual; }

        static readonly List<Case> Cases = new List<Case>();

        static void Add(string id, string req, string fix, params (string label, object expected, Func<object> actual)[] cases)
        {
            foreach (var c in cases) Cases.Add(new Case { id = id, req = req, fix = fix, label = c.label, expected = c.expected, actual = c.actual });
        }

        static SpeedStats Stats(params float[] values)
        {
            var s = new SpeedStats();
            foreach (var v in values) s.Add(v);
            return s;
        }

        static BlankCheck()
        {
            Add("1-1", "질량으로 가중한 평균", "위치를 그냥 평균 내지 않았나? 위치 × 질량을 모두 더하고, 질량의 합으로 나눈다.",
                ("같은 무게 둘 → 가운데", new Vector3(1, 0, 0), () => M.CenterOfMass(new[] { Vector3.zero, new Vector3(2, 0, 0) }, new[] { 1f, 1f })),
                ("3kg 위 · 1kg 아래 → 무거운 쪽으로", new Vector3(0, 1.5f, 0), () => M.CenterOfMass(new[] { new Vector3(0, 2, 0), Vector3.zero }, new[] { 3f, 1f })));
            Add("1-1", "부품 하나여도 맞다", "반복 범위가 배열 전체(0 ~ Length-1)인지 본다.",
                ("부품 하나 (경계) → 그 자리", new Vector3(0.3f, 0.9f, -0.2f), () => M.CenterOfMass(new[] { new Vector3(0.3f, 0.9f, -0.2f) }, new[] { 8f })));
            Add("1-1", "0으로 나누지 않는다", "질량 합이 0이면 나누지 말고 Vector3.zero 를 돌려준다.",
                ("부품 없음 (경계) → (0,0,0)", Vector3.zero, () => M.CenterOfMass(new Vector3[0], new float[0])));

            Add("2-1", "양쪽을 따로 늘린다", "+쪽은 action × high, -쪽은 -action × low. 한 직선으로 늘리면 0이 0도가 되지 않는다.",
                ("엉덩이(-30~90) · +1 → 90", 90f, () => M.ActionToAngle(1f, -30f, 90f)),
                ("엉덩이 · -0.5 → -15", -15f, () => M.ActionToAngle(-0.5f, -30f, 90f)),
                ("행동 0 (경계) → 0도 차렷", 0f, () => M.ActionToAngle(0f, -30f, 90f)));          // 범위 가운데로 보내는 방식이면 30
            Add("2-1", "범위 밖 행동을 자른다", "먼저 Mathf.Clamp(action, -1f, 1f) 로 자른다.",
                ("행동 3 (경계: 범위 밖) → 90", 90f, () => M.ActionToAngle(3f, -30f, 90f)));
            Add("2-1", "high 가 0인 관절", "무릎(-120~0)은 + 쪽 한계가 0이라 +1 이 0도여야 한다.",
                ("무릎(-120~0) · +1 (경계) → 0", 0f, () => M.ActionToAngle(1f, -120f, 0f)));

            Add("2-2", "범위로 나눈다 (주어진 기능)", "양수 각도는 high 로, 음수 각도는 -low 로 나눈다. 시작 코드의 식이다.",
                ("무릎 -60도 → -0.5", -0.5f, () => M.AngleToAction(-60f, -120f, 0f)),
                ("엉덩이 45도 → 0.5", 0.5f, () => M.AngleToAction(45f, -30f, 90f)));
            Add("2-2", "결과를 -1~1 로 자른다", "나눈 결과를 Mathf.Clamp(…, -1f, 1f) 로 감싼다.",
                ("한계를 넘은 -40도 (경계) → -1", -1f, () => M.AngleToAction(-40f, -30f, 90f)));
            Add("2-2", "0으로 나누지 않는다", "나눌 범위를 변수로 빼고, 0 이하이면 나누기 전에 0을 돌려준다.",
                ("무릎 +3도 (경계: high=0) → 0, NaN 아님", 0f, () => M.AngleToAction(3f, -120f, 0f)));

            Add("3-1", "세상의 아래를 몸 기준으로", "회전 × 벡터 는 몸 → 세상 방향이다. 반대 방향은 Quaternion.Inverse(회전) × 벡터.",
                ("똑바로 → (0,-1,0)", Vector3.down, () => M.DownInBody(Quaternion.identity)),
                ("앞으로 60도 숙임 → 아래가 몸의 앞쪽(+z)", new Vector3(0, -0.5f, 0.866f), () => M.DownInBody(Quaternion.Euler(60, 0, 0))),  // rot*down 이면 z가 음수
                ("거꾸로 (경계) → (0,1,0)", Vector3.up, () => M.DownInBody(Quaternion.Euler(180, 0, 0))));

            Add("3-2", "똑바름 × 높이 비율", "똑바른 정도(up.y)와 높이 비율(height / standHeight)을 곱한다.",
                ("똑바로 · 제 높이 → 1", 1f, () => M.StandReward(Vector3.up, 0.96f, 0.96f)),
                ("똑바로 · 절반 높이 → 0.5", 0.5f, () => M.StandReward(Vector3.up, 0.48f, 0.96f)));
            Add("3-2", "더하지 않고 곱한다", "누웠는데 점수가 0이 아니라면 두 값을 더한 것이다. 곱하면 하나라도 0일 때 0점.",
                ("누움 (경계) → 0", 0f, () => M.StandReward(Vector3.forward, 0.96f, 0.96f)));           // 더하기면 0.5
            Add("3-2", "0~1 로 자른다", "두 값을 각각 Mathf.Clamp01 로 자른다. 거꾸로면 up.y 가 음수, 높이 뛰면 비율이 1을 넘는다.",
                ("거꾸로 (경계: up.y<0) → 0", 0f, () => M.StandReward(Vector3.down, 0.96f, 0.96f)),
                ("더 높이 뜀 (경계) → 1을 넘지 않음", 1f, () => M.StandReward(Vector3.up, 1.5f, 0.96f)));

            Add("4-1", "보는 방향으로 회전", "y 를 버린 forward 를 Quaternion.LookRotation 에 넣는다.",
                ("정면 → 회전 없음", Quaternion.identity, () => M.HeadingRotation(Vector3.forward)),
                ("오른쪽(+x)을 봄 → 좌우 90도", Quaternion.Euler(0, 90, 0), () => M.HeadingRotation(Vector3.right)));
            Add("4-1", "앞뒤 기울기를 버린다", "LookRotation 에 넣기 전에 forward.y = 0 이어야 한다 (주어진 첫 줄을 지우지 않았나).",
                ("숙인 채 오른쪽 → 좌우 90도만", Quaternion.Euler(0, 90, 0), () => M.HeadingRotation(new Vector3(0.5f, -0.8f, 0f))));
            Add("4-1", "방향이 없으면 회전 없음", "y 를 버린 뒤 길이가 거의 0(sqrMagnitude < 1e-6)이면 Quaternion.identity 를 돌려준다.",
                ("바로 아래를 봄 (경계) → 회전 없음", Quaternion.identity, () => M.HeadingRotation(Vector3.down)));

            Add("4-2", "목표 속도와의 차이로 점수", "d = |velocity − direction × targetSpeed|,  r = 1 − (d/targetSpeed)²,  r × r 을 돌려준다.",
                ("목표 속도 그대로 → 1", 1f, () => M.SpeedReward(Vector3.forward, Vector3.forward, 1f)),
                ("멈춤 → 0", 0f, () => M.SpeedReward(Vector3.zero, Vector3.forward, 1f)),
                ("절반 속도 → 0.5625", 0.5625f, () => M.SpeedReward(Vector3.forward * 0.5f, Vector3.forward, 1f)));
            Add("4-2", "차이를 targetSpeed 까지로 자른다", "d 를 Mathf.Clamp(d, 0, targetSpeed) 로 자르지 않으면 r 이 음수가 되고 제곱하면 다시 커진다.",
                ("반대로 달림 (경계) → 0", 0f, () => M.SpeedReward(Vector3.back * 2f, Vector3.forward, 1f)));
            Add("4-2", "위아래 속도는 버린다", "주어진 첫 줄(velocity.y = 0)을 지우지 않았나.",
                ("떨어지는 속도는 무시 (경계) → 1", 1f, () => M.SpeedReward(new Vector3(0, -3, 1), Vector3.forward, 1f)));

            Add("4-3", "내적을 0~1 로", "길이 1 벡터의 내적은 -1~1 이다. (내적 + 1) × 0.5 로 0~1 로 옮긴다.",
                ("같은 쪽 → 1", 1f, () => M.FacingReward(Vector3.forward, Vector3.forward)),
                ("옆 → 0.5", 0.5f, () => M.FacingReward(Vector3.right, Vector3.forward)),
                ("반대 → 0", 0f, () => M.FacingReward(Vector3.back, Vector3.forward)));
            Add("4-3", "숙인 것은 따지지 않는다", "두 벡터 모두 y 를 버린 뒤 normalized 로 길이를 1로 만들고 내적한다.",
                ("고개 숙임 (경계) → 1", 1f, () => M.FacingReward(new Vector3(0, -1, 1), Vector3.forward)));

            Add("5-1", "평균을 낸다", "Add 에서 개수를 1 늘리고 평균 += (값 − 평균) / 개수. Average 가 값을 저장하는 속성이어야 한다.",
                ("2, 4 를 더함 → 평균 3", 3f, () => Stats(2f, 4f).Average),
                ("1 ~ 100 을 더함 → 평균 50.5", 50.5f, () => Stats(Enumerable.Range(1, 100).Select(i => (float)i).ToArray()).Average));
            Add("5-1", "개수를 센다", "Count 가 값을 저장하는 속성인지(예: { get; private set; }), Add 에서 1씩 늘리는지 본다.",
                ("세 번 더함 → Count 3", 3f, () => (float)Stats(1f, 1f, 1f).Count));
            Add("5-1", "처음 상태", "아무것도 더하지 않았으면 평균 0. 첫 값에서 0으로 나누지 않도록 개수를 먼저 늘린다.",
                ("새로 만듦 (경계) → 평균 0", 0f, () => new SpeedStats().Average),
                ("값 하나 (경계) → 그 값", 7f, () => Stats(7f).Average));
            Add("5-1", "Clear 로 처음부터", "Clear 에서 Count 와 Average 를 둘 다 0으로 되돌린다. 개수를 안 지우면 다음 평균이 틀어진다.",
                ("5, 5 → Clear → 1 (경계) → 평균 1", 1f, () => { var s = Stats(5f, 5f); s.Clear(); s.Add(1f); return s.Average; }));

            Add("5-2", "오차 비율 (주어진 기능)", "|평균 − 목표| / 목표. 5-1 의 Average 가 맞아야 이 케이스도 맞는다.",
                ("목표 1 · 평균 0.8 → 0.2", 0.2f, () => Stats(0.8f).Error(1f)),
                ("목표 0.5 · 평균 1.0 → 1", 1f, () => Stats(1f).Error(0.5f)),
                ("뒤로 감 (경계) → 1.5", 1.5f, () => Stats(-0.5f).Error(1f)));
            Add("5-2", "목표 0 이하 처리", "맨 앞에서 targetSpeed <= 0 이면 바로 0 을 돌려준다 (이른 반환).",
                ("목표 0 (경계) → 0, NaN 아님", 0f, () => Stats(0.3f).Error(0f)));
        }

        // 이 실습의 요구사항 이름들 (창에 미리 보여 준다)
        public static List<string> Requirements(string id) => Cases.Where(c => c.id == id).Select(c => c.req).Distinct().ToList();

        // id가 prefix로 시작하는 케이스를 실행한다. ("2-1" = 실습 하나, "" = 전부)
        public static List<Result> Grade(string prefix)
        {
            var results = new List<Result>();
            foreach (var c in Cases)
            {
                if (!c.id.StartsWith(prefix)) continue;
                object value;
                try { value = c.actual(); }
                catch (Exception e) { value = e; }      // 학습자 코드가 터져도 테스트는 계속한다

                bool pass = value is float f ? c.expected is float g && Mathf.Abs(f - g) < 1e-3f
                          : value is Vector3 v ? c.expected is Vector3 w && (v - w).magnitude < 1e-3f
                          : value is Quaternion q ? c.expected is Quaternion r && Quaternion.Angle(q, r) < 0.1f
                          : Equals(value, c.expected);
                results.Add(new Result { req = c.req, label = c.label, expected = Show(c.expected), actual = Show(value), fix = c.fix, why = Why(value), pass = pass });
            }
            return results;
        }

        // 값만 보고 알 수 있는 흔한 원인
        static string Why(object value)
        {
            bool bad(float x) => float.IsNaN(x) || float.IsInfinity(x);
            if (value is float f && bad(f) || value is Vector3 v && (bad(v.x) || bad(v.y) || bad(v.z)))
                return "NaN · 무한대가 나왔다 — 0으로 나눴을 가능성이 크다. 나누기 전에 나눌 값을 검사한다.";
            if (value is IndexOutOfRangeException) return "배열 범위를 벗어났다 — 반복 조건(i < Length)을 본다.";
            if (value is NullReferenceException) return "null 을 썼다 — 만들지 않은 객체를 쓰지 않았나.";
            if (value is DivideByZeroException) return "정수를 0으로 나눴다 — 나누기 전에 0인지 검사한다.";
            if (value is Exception e) return "예외 " + e.GetType().Name + " — " + e.Message;
            return null;
        }

        static string Show(object o) =>
            o is float f ? (float.IsNaN(f) ? "NaN" : float.IsInfinity(f) ? "무한대" : f.ToString("0.###"))
          : o is Quaternion q ? "좌우 " + q.eulerAngles.y.ToString("0") + "° 앞뒤 " + Mathf.DeltaAngle(0, q.eulerAngles.x).ToString("0") + "°"
          : o is Vector3 v ? v.ToString("0.##")
          : o is Exception e ? "예외 " + e.GetType().Name
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

using UnityEngine;

namespace Humanoid
{
    // 걸음걸이 성적표. 매 물리 스텝 Step 을 부르면 판 하나 동안 "어떻게 걸었는가"를 모은다.
    // 보상과 따로 둔다 — "목표에 갔는가"만 보면 몸을 숙이고 발을 끌며 가도 같은 점수라서, 걸음 모양은 따로 재야 보인다.
    // 전부 시뮬레이션에서 직접 잴 수 있는 값이다 (발 접촉 · 발 위치 · 속도 · 관절 목표).
    public class GaitStats
    {
        // 발 하나의 기록
        public class Foot
        {
            public int steps;           // 발을 들었다가 내린 횟수 (0.1초 넘게 떠 있었을 때만)
            public float contactTime;   // 바닥에 닿아 있던 시간 (초)
            public float slip;          // 바닥에 닿은 채로 미끄러진 거리 (m) — 발 끌기
            public float lift;          // 걸음마다 가장 높이 든 높이의 합 (m)
            public float stride;        // 걸음마다 발이 옮겨 간 거리의 합 (m)

            internal bool wasDown = true;
            internal int airSteps;
            internal float maxHeight;
            internal Vector3 liftPos;
        }

        public Foot left = new Foot(), right = new Foot();
        public float time, distance, tilt, targetMotion;
        public int samples;

        BodyPart[] _joints;
        Quaternion[] _lastTargets;

        public void Clear()
        {
            left = new Foot();
            right = new Foot();
            time = distance = tilt = targetMotion = 0f;
            samples = 0;
        }

        // forward = 가야 할 방향(수평, 길이 1). 그 방향으로 간 거리만 이동 거리로 친다.
        public void Step(HumanoidBody body, Vector3 forward, float dt)
        {
            float ground = body.transform.position.y;
            Track(left, body.footL, ground, dt);
            Track(right, body.footR, ground, dt);

            Vector3 v = body.CenterOfMassVelocity;
            distance += Vector3.Dot(new Vector3(v.x, 0f, v.z), forward) * dt;
            tilt += Vector3.Angle(body.torso.transform.up, Vector3.up);

            // 관절 목표가 얼마나 바쁘게 바뀌나(도/초). 덜덜 떠는 행동을 잡는다
            if (_joints == null)
            {
                _joints = System.Array.FindAll(body.GetComponentsInChildren<BodyPart>(), p => p.joint != null);
                _lastTargets = new Quaternion[_joints.Length];
            }
            for (int i = 0; i < _joints.Length; i++)
            {
                Quaternion q = _joints[i].joint.targetRotation;
                if (samples > 0) targetMotion += Quaternion.Angle(_lastTargets[i], q) / dt;
                _lastTargets[i] = q;
            }

            time += dt;
            samples++;
        }

        static void Track(Foot f, BodyPart foot, float ground, float dt)
        {
            Vector3 p = foot.rb.worldCenterOfMass;
            if (foot.touchingGround)
            {
                f.contactTime += dt;
                f.slip += foot.SlipSpeed * dt;
                if (!f.wasDown && f.airSteps >= 5)     // 0.1초 넘게 떠 있다 내렸다 = 한 걸음
                {
                    f.steps++;
                    f.lift += f.maxHeight;
                    f.stride += new Vector2(p.x - f.liftPos.x, p.z - f.liftPos.z).magnitude;
                }
                f.airSteps = 0;
            }
            else
            {
                if (f.wasDown) { f.liftPos = p; f.maxHeight = 0f; }
                f.airSteps++;
                f.maxHeight = Mathf.Max(f.maxHeight, p.y - ground - 0.03f);   // 발을 바닥에 놓았을 때 중심 높이가 0.03
            }
            f.wasDown = foot.touchingGround;
        }

        // ── 판 하나의 성적 ──
        static float Div(float a, float b) => b > 1e-4f ? a / b : 0f;

        public float Speed => Div(distance, time);                                         // 가야 할 쪽으로 (m/s)
        public float Cadence => Div(left.steps + right.steps, time);                       // 걸음/초
        public float StepBalance => Div(Mathf.Min(left.steps, right.steps), Mathf.Max(left.steps, right.steps));   // 1 = 좌우 같음
        public float Slip => Div(left.slip + right.slip, time);                            // 발이 미끄러지는 속도 (m/s)
        public float SlipPerMeter => Div(left.slip + right.slip, Mathf.Abs(distance));     // 1m 갈 때 발이 미끄러진 거리
        public float LiftHeight => Div(left.lift + right.lift, left.steps + right.steps);  // 걸음당 발 든 높이 (m)
        public float StrideLength => Div(left.stride + right.stride, left.steps + right.steps);
        public float Tilt => Div(tilt, samples);              // 몸통 기울기 평균 (도)
        public float Jitter => Div(targetMotion, samples);    // 관절 목표 변화 (도/초)

        public string Report() => string.Format(
            "속도 {0:F2} m/s · 걸음 L{1} R{2} (좌우 {3:F2}) · {4:F2} 걸음/초 · 보폭 {5:F2}m · 발 든 높이 {6:F3}m · " +
            "끌림 {7:F2} m/m · 기울기 {8:F1}° · 떨림 {9:F0}°/s",
            Speed, left.steps, right.steps, StepBalance, Cadence, StrideLength, LiftHeight, SlipPerMeter, Tilt, Jitter);
    }
}

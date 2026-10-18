using UnityEngine;
using UnityEngine.InputSystem;

namespace Humanoid
{
    // 키보드로 관절을 하나씩 움직이고, 화면에 각도를 보여준다.
    // 키보드가 만드는 것도 AI와 똑같은 "행동 18개(-1~1)"다. 에이전트를 붙이면 AI가 이 자리에 들어온다.
    [RequireComponent(typeof(HumanoidBody))]
    public class KeyboardControl : MonoBehaviour
    {
        [Tooltip("끄면 키보드는 행동만 만들고 몸은 움직이지 않는다. (에이전트의 Heuristic이 대신 쓴다)")]
        public bool driveBody = true;

        [HideInInspector] public float[] actions;
        [HideInInspector] public string statusLine;    // 에이전트가 채우는 한 줄

        HumanoidBody _body;
        int _selected;
        float[] _from, _goal;
        float _blend = 1f;

        // 무릎 굽히기 자세(도). 순서 = 행동 순서:
        // 허리 앞뒤·좌우 | 왼 어깨 앞뒤·좌우, 팔꿈치 | 오른팔 | 왼 엉덩이 앞뒤·좌우, 무릎, 발목 앞뒤·좌우 | 오른다리
        static readonly float[] BendPose = { 0, 0, 0, 0, 0, 0, 0, 0, 20, 0, -40, 20, 0, 20, 0, -40, 20, 0 };

        void Start()
        {
            _body = GetComponent<HumanoidBody>();
            actions = new float[_body.JointCount];
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.rightArrowKey.wasPressedThisFrame) _selected = (_selected + 1) % actions.Length;
            if (kb.leftArrowKey.wasPressedThisFrame) _selected = (_selected + actions.Length - 1) % actions.Length;

            float dir = (kb.upArrowKey.isPressed ? 1f : 0f) - (kb.downArrowKey.isPressed ? 1f : 0f);
            if (dir != 0f)
            {
                _blend = 1f;    // 손으로 움직이면 자세 전환을 멈춘다
                actions[_selected] = Mathf.Clamp(actions[_selected] + dir * Time.deltaTime, -1f, 1f);
            }

            if (kb.digit1Key.wasPressedThisFrame) StartPose(new float[actions.Length]);
            if (kb.digit2Key.wasPressedThisFrame)
            {
                var goal = new float[actions.Length];
                for (int i = 0; i < goal.Length; i++)
                {
                    Vector2 r = _body.Range(i);
                    goal[i] = HumanoidMath.AngleToAction(BendPose[i], r.x, r.y);
                }
                StartPose(goal);
            }

            // 자세를 한 번에 바꾸면 그 움직임에 몸이 밀려 넘어진다. 2초에 걸쳐 바꾼다.
            if (_blend < 1f)
            {
                _blend = Mathf.Min(1f, _blend + Time.deltaTime / 2f);
                for (int i = 0; i < actions.Length; i++) actions[i] = Mathf.Lerp(_from[i], _goal[i], _blend);
            }

            var pelvis = _body.pelvis.rb;
            if (kb.hKey.wasPressedThisFrame)    // 공중에 매달기: 넘어지지 않게 하고 관절만 본다
            {
                pelvis.isKinematic = !pelvis.isKinematic;
                if (pelvis.isKinematic) pelvis.transform.position += Vector3.up * 0.5f;
            }
            // 밀기: 몸통을 앞으로 20 N·s. 관절이 단단해도 넘어진다 (실측: 10은 버티고 20은 넘어진다)
            if (kb.pKey.wasPressedThisFrame) _body.torso.rb.AddForce(transform.forward * 20f, ForceMode.Impulse);
            if (kb.rKey.wasPressedThisFrame && driveBody)
            {
                pelvis.isKinematic = false;
                _body.ResetBody();
                ResetActions();
            }
        }

        // 목표 각도를 전부 차렷(0)으로. 몸만 되돌리고 이것을 안 지우면
        // 이전 자세로 다시 당겨져 또 넘어진다. (R 키, 에이전트가 판을 새로 열 때)
        public void ResetActions()
        {
            if (actions != null) System.Array.Clear(actions, 0, actions.Length);
            _blend = 1f;
        }

        void StartPose(float[] goal)
        {
            _from = (float[])actions.Clone();
            _goal = goal;
            _blend = 0f;
        }

        void FixedUpdate()
        {
            if (driveBody) _body.SetActions(actions);
        }

        void OnGUI()
        {
            if (actions == null) return;
            GUI.Box(new Rect(10, 10, 330, 90 + actions.Length * 20), GUIContent.none);
            GUILayout.BeginArea(new Rect(20, 16, 320, 600));
            GUILayout.Label("←→ 관절 선택  ↑↓ 움직이기  1 차렷  2 무릎 굽히기\nH 공중 매달기  P 밀기  R 처음으로");
            for (int i = 0; i < actions.Length; i++)
            {
                Vector2 r = _body.Range(i);
                GUILayout.Label(string.Format("{0} {1,-12} 목표 {2,5:F0}°  실제 {3,5:F0}°",
                    i == _selected ? "▶" : "  ", _body.JointName(i),
                    HumanoidMath.ActionToAngle(actions[i], r.x, r.y), _body.Angle(i)));
            }
            GUILayout.Label(string.Format("무게중심 높이 {0:F2}  {1}", _body.CenterOfMass.y, _body.HasFallen ? "넘어짐!" : ""));
            if (!string.IsNullOrEmpty(statusLine)) GUILayout.Label(statusLine);
            GUILayout.EndArea();
        }
    }
}

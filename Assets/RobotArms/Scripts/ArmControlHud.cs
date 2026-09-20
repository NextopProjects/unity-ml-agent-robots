using UnityEngine;

namespace RobotArms
{
    /// <summary>
    /// 화면에 조작 방법과 팔 상태를 표시한다. (챕터 2)
    ///
    /// Canvas나 프리팹 없이 <c>OnGUI</c>만으로 그린다.
    /// 씬에 오브젝트를 더 만들지 않아서, 2장의 초점이 흐려지지 않는다.
    ///
    /// 보여주는 것
    ///   - 어떤 키가 어떤 관절을 움직이는지
    ///   - 각 관절이 지금 몇 도인지, 가동 범위의 어디쯤인지
    ///   - 바닥·물체에 막혀서 멈췄는지
    ///   - 팔 끝과 물체 사이 거리
    ///
    /// 관절 각도를 눈으로 보면서 조작하면
    /// "가동 범위가 어디까지인지"를 훨씬 빨리 감 잡을 수 있다.
    /// </summary>
    [RequireComponent(typeof(RobotArmController))]
    public class ArmControlHud : MonoBehaviour
    {
        [Header("표시 대상")]
        [Tooltip("팔 끝과의 거리를 표시할 대상. 보통 Object.")]
        public Transform target;

        [Tooltip("이 거리 안으로 들어오면 '도달'로 표시한다.")]
        public float successDistance = 0.25f;

        [Header("표시 설정")]
        [Tooltip("시작할 때 도움말을 켜둘지. 실행 중에는 F1으로 켜고 끈다.")]
        public bool showHelp = true;

        [Tooltip("글자 크기. 화면이 크면 키운다.")]
        public int fontSize = 15;

        RobotArmController _arm;
        GUIStyle _label, _title, _panel;
        Font _font;

        void Awake()
        {
            _arm = GetComponent<RobotArmController>();
        }

        void Update()
        {
            if (RobotArmInput.ToggleHelpPressed) showHelp = !showHelp;
        }

        void OnGUI()
        {
            EnsureStyles();

            if (!showHelp)
            {
                GUI.Label(new Rect(14, 12, 400, 24),
                    RobotArmInput.ToggleHelpKeyLabel + " : 조작법 보기", _label);
                return;
            }

            const int width = 300;
            int rows = _arm.JointCount + 3;
            int height = 92 + rows * 22;

            GUI.Box(new Rect(10, 10, width, height), GUIContent.none, _panel);

            float x = 24f;
            float y = 22f;

            GUI.Label(new Rect(x, y, width, 22), "로봇 팔 조작", _title);
            y += 26f;

            // ── 관절 ──
            for (int i = 0; i < _arm.JointCount; i++)
            {
                var j = _arm.joints[i];
                float t = Mathf.InverseLerp(j.minAngle, j.maxAngle, j.Angle);

                // 가동 범위의 양 끝에 가까우면 표시해준다.
                string edge = t <= 0.01f ? "  (최소)" : (t >= 0.99f ? "  (최대)" : "");

                GUI.Label(new Rect(x, y, width, 22),
                    string.Format("{0,-7} {1,-8} {2,7:F1}°{3}",
                        RobotArmInput.KeyLabel(i), j.label, j.Angle, edge), _label);
                y += 22f;
            }

            y += 6f;
            GUI.Label(new Rect(x, y, width, 22),
                string.Format("{0,-7} {1}", RobotArmInput.ResetKeyLabel, "자세 초기화"), _label);
            y += 22f;

            GUI.Label(new Rect(x, y, width, 22),
                string.Format("{0,-7} {1}", RobotArmInput.GripKeyLabel, "잡기 (4장부터)"), _label);
            y += 26f;

            // ── 상태 ──
            if (target != null)
            {
                float d = Vector3.Distance(_arm.TipPosition, target.position);
                bool reached = d < successDistance;

                _label.normal.textColor = reached ? new Color(0.45f, 1f, 0.55f) : Color.white;
                GUI.Label(new Rect(x, y, width, 22),
                    string.Format("팔 끝 ↔ 물체   {0:F2}{1}", d, reached ? "   도달!" : ""), _label);
                _label.normal.textColor = Color.white;
                y += 22f;
            }

            if (_arm.blockPenetration && _arm.BlockedRecently)
            {
                _label.normal.textColor = new Color(1f, 0.55f, 0.45f);
                GUI.Label(new Rect(x, y, width, 22), "바닥·물체에 막혀 멈춤", _label);
                _label.normal.textColor = Color.white;
            }

            GUI.Label(new Rect(14, 10 + height + 4, 400, 22),
                RobotArmInput.ToggleHelpKeyLabel + " : 조작법 숨기기", _label);
        }

        void EnsureStyles()
        {
            if (_label != null) return;

            // Unity 기본 GUI 폰트는 한글이 깨질 수 있다.
            // 운영체제에 설치된 한글 폰트를 직접 불러와 쓴다.
            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans KR", "NanumGothic", "Gulim", "Arial" },
                fontSize);

            _label = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = fontSize,
                richText = false,
            };
            _label.normal.textColor = Color.white;

            _title = new GUIStyle(_label)
            {
                fontSize = fontSize + 2,
                fontStyle = FontStyle.Bold,
            };
            _title.normal.textColor = new Color(1f, 0.85f, 0.4f);

            _panel = new GUIStyle(GUI.skin.box);
            _panel.normal.background = SolidTexture(new Color(0f, 0f, 0f, 0.72f));
        }

        static Texture2D SolidTexture(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }
    }
}

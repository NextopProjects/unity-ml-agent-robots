using System;

namespace MLAgentsGuide
{
    /// <summary>
    /// Inspector에 표시할 설명 문구. 내용만 담고 UI는 다루지 않는다.
    ///
    /// 대상은 강화학습을 처음 접하는 사람이다. 그래서
    ///   - 전문 용어를 먼저 풀어 쓰고(관측 = 에이전트가 보는 것)
    ///   - 값이 "무엇인지"보다 "무슨 일을 하는지"를 먼저 말하고
    ///   - 가능하면 실제 숫자로 된 예를 붙인다.
    ///
    /// 값과 기본값은 ml-agents 4.1.0 기준이다.
    /// </summary>
    internal static class GuideContent
    {
        internal sealed class Item
        {
            /// <summary>Inspector에 보이는 항목 이름.</summary>
            public string Label;

            /// <summary>이 값이 무슨 일을 하는지.</summary>
            public string Text;

            /// <summary>앞 항목과 다르면 구획 제목으로 표시한다. 비어 있으면 표시하지 않는다.</summary>
            public string Group;

            public Item(string group, string label, string text)
            {
                Group = group;
                Label = label;
                Text = text;
            }
        }

        internal sealed class Component
        {
            /// <summary>이 컴포넌트가 무엇인지.</summary>
            public string Summary;

            /// <summary>동작 흐름을 한눈에 보여주는 한 줄. 없으면 null.</summary>
            public string Flow;

            public Item[] Items = Array.Empty<Item>();
        }

        public static readonly Component BehaviorParameters = new Component
        {
            Summary =
                "에이전트의 <b>두뇌 설정</b>입니다.\n" +
                "에이전트는 매 판단마다 주변을 <b>보고</b>, 그 정보를 신경망에 넣어, " +
                "<b>무엇을 할지</b> 결정합니다. 이 컴포넌트는 그 입력(무엇을 보는지)과 " +
                "출력(무엇을 할 수 있는지)의 크기, 그리고 <b>누가 판단할지</b>를 정합니다.",
            Flow = "보기(관측) → 신경망 → 하기(행동)",
            Items = new[]
            {
                new Item(null, "Behavior Name",
                    "이 두뇌에 붙이는 이름입니다. 학습을 돌리는 파이썬 프로그램이 " +
                    "<b>이 이름으로 어느 에이전트를 학습시킬지 찾습니다.</b>\n" +
                    "학습 설정 파일(yaml)에 <b>behaviors: RollerBall:</b> 이라고 적었다면 여기에도 " +
                    "똑같이 RollerBall 이라고 적어야 합니다. 철자가 다르면 " +
                    "에러 없이 그냥 학습이 붙지 않습니다."),

                new Item("보기 — 에이전트가 무엇을 보는가", "Space Size",
                    "에이전트가 매 판단마다 보는 <b>숫자의 개수</b>입니다. 사람으로 치면 눈으로 " +
                    "들어오는 정보의 가짓수입니다.\n" +
                    "스크립트의 CollectObservations에서 AddObservation으로 넣는 값의 개수와 " +
                    "<b>정확히 같아야 하고</b>, 다르면 실행할 때 에러가 납니다.\n" +
                    "예) 목표 위치(Vector3니까 3개) + 내 위치(3개) + 내 속도 x·z(2개) = <b>8</b>\n" +
                    "주의: Vector3 하나는 1개가 아니라 <b>3개</b>입니다. " +
                    "그리고 센서 컴포넌트(Ray Perception 등)가 만드는 관측은 자동으로 합쳐지므로 " +
                    "여기에 더하면 안 됩니다."),

                new Item("보기 — 에이전트가 무엇을 보는가", "Stacked Vectors",
                    "지금 이 순간뿐 아니라 <b>직전 몇 순간까지 함께 보게</b> 합니다.\n" +
                    "사진 한 장만 보면 공이 다가오는지 멀어지는지 알 수 없지만, 여러 장을 겹쳐 보면 " +
                    "알 수 있습니다. 그런 정보를 주는 설정입니다.\n" +
                    "기본 1(현재 순간만). 2로 올리면 신경망이 받는 값이 <b>2배</b>가 되어 학습이 " +
                    "느려지므로 필요할 때만 올립니다. Space Size는 그대로 둡니다."),

                new Item("하기 — 에이전트가 무엇을 할 수 있는가", "Continuous Actions",
                    "에이전트가 돌릴 수 있는 <b>다이얼의 개수</b>입니다. 각 다이얼은 " +
                    "<b>-1에서 1 사이의 실수</b> 하나를 내놓습니다.\n" +
                    "예) 앞뒤로 미는 힘, 좌우로 미는 힘 → 2개\n" +
                    "스크립트의 OnActionReceived에서 ContinuousActions[0], [1] 로 그 값을 받습니다.\n" +
                    "주의: 값이 -1~1이라 그대로 힘으로 쓰면 거의 움직이지 않습니다. " +
                    "보통 배율을 곱해서 씁니다."),

                new Item("하기 — 에이전트가 무엇을 할 수 있는가", "Discrete Branches",
                    "정해진 <b>선택지 중 하나를 고르는</b> 방식입니다. 다이얼이 아니라 버튼에 가깝습니다.\n" +
                    "브랜치 하나가 \"선택 묶음\" 하나입니다. 브랜치를 만들면 그 안에 선택지가 " +
                    "몇 개인지 따로 적습니다.\n" +
                    "예) 브랜치 1개 + 선택지 3개 → \"왼쪽 / 가만히 / 오른쪽\" 중 하나를 고름\n" +
                    "주의: 브랜치 개수와 선택지 개수는 다른 값입니다. " +
                    "연속 행동과 이산 행동은 한 에이전트에서 같이 쓸 수 있습니다."),

                new Item("판단 — 누가 행동을 정하는가", "Behavior Type",
                    "지금 이 에이전트를 <b>누가 조종할지</b> 고르는 스위치입니다.\n" +
                    "<b>Default</b> — 알아서 고릅니다. 학습이 돌아가는 중이면 파이썬 트레이너가, " +
                    "아니면 아래 Model이, 그것도 없으면 내가 짠 Heuristic() 코드(보통 키보드)가 조종합니다.\n" +
                    "<b>Heuristic Only</b> — 항상 Heuristic() 코드로만. 직접 조종해 볼 때 씁니다.\n" +
                    "<b>Inference Only</b> — 항상 Model로만. Model이 비어 있으면 에러가 납니다.\n" +
                    "\"학습이 안 붙어요\", \"키보드로만 움직여요\" 의 원인이 대부분 여기입니다."),

                new Item("판단 — 누가 행동을 정하는가", "Model",
                    "학습이 끝나면 결과로 나오는 <b>.onnx 파일(학습된 두뇌)</b>을 넣는 칸입니다.\n" +
                    "여기에 넣으면 파이썬 없이도 그 두뇌로 스스로 움직입니다. 학습 결과를 " +
                    "확인할 때 씁니다.\n" +
                    "<b>학습을 돌릴 때는 비워 둡니다.</b>"),

                new Item("판단 — 누가 행동을 정하는가", "Inference Device",
                    "넣어 둔 Model을 <b>CPU와 GPU 중 어디서 계산할지</b>입니다.\n" +
                    "보통 기본값 그대로 둡니다. 학습 속도와는 관계가 없습니다. " +
                    "학습 계산은 파이썬 쪽에서 일어나기 때문입니다."),

                new Item("판단 — 누가 행동을 정하는가", "Deterministic Inference",
                    "Model이 행동을 고를 때 <b>매번 조금씩 다르게 고를지(꺼짐), " +
                    "항상 가장 좋다고 본 것만 고를지(켜짐)</b>입니다.\n" +
                    "켜면 결과가 일정해져 확인하기 좋습니다. 학습에는 영향이 없습니다."),

                new Item("그 밖", "Team Id",
                    "에이전트끼리 <b>편을 갈라 겨루게</b> 할 때 쓰는 팀 번호입니다(자기대전).\n" +
                    "에이전트 하나를 학습시키는 단계에서는 <b>0으로 둡니다.</b>"),

                new Item("그 밖", "Use Child Sensors",
                    "자식 오브젝트에 붙인 센서(예: Ray Perception Sensor)도 " +
                    "<b>이 에이전트의 눈으로 칠지</b>입니다.\n" +
                    "보통 켜 둡니다. 끄면 그 센서가 무시되어 보는 정보의 개수가 갑자기 달라집니다."),

                new Item("그 밖", "Observable Attribute Handling",
                    "스크립트의 변수에 [Observable] 을 붙여 두면 <b>자동으로 관측에 넣어 주는</b> 기능입니다.\n" +
                    "기본값 Ignore 는 그 기능을 쓰지 않는다는 뜻입니다. " +
                    "처음에는 관측 개수를 직접 세는 편이 쉬우니 그대로 둡니다."),
            },
        };

        public static readonly Component DecisionRequester = new Component
        {
            Summary =
                "에이전트에게 <b>\"지금 판단해라\"라고 주기적으로 알려 주는 타이머</b>입니다.\n" +
                "에이전트는 스스로 판단을 시작하지 않습니다. 이 컴포넌트가 없으면 스크립트에서 " +
                "RequestDecision()을 직접 불러야 하고, 부르지 않으면 " +
                "<b>에이전트는 가만히 있기만 합니다.</b>",
            Flow = "타이머가 알림 → 에이전트가 관측 → 판단 → 행동",
            Items = new[]
            {
                new Item(null, "Decision Period",
                    "<b>몇 번에 한 번씩 새로 판단할지</b>입니다.\n" +
                    "여기서 \"한 번\"은 물리 계산 1회(FixedUpdate)를 말하고, 기본값은 0.02초입니다. " +
                    "그래서 5로 두면 <b>0.1초마다 한 번</b> 판단합니다.\n" +
                    "작게 하면 더 자주 판단해 반응이 빨라지지만 판단 횟수가 늘어 학습이 오래 걸립니다. " +
                    "크게 하면 반대입니다."),

                new Item(null, "Decision Step",
                    "주기 안에서 <b>몇 번째 차례에 판단할지</b>입니다.\n" +
                    "에이전트를 여러 개 두고 학습할 때 모두 같은 순간에 판단하면 그 프레임만 " +
                    "무거워집니다. 이 값을 서로 다르게 주면 판단 시점이 흩어집니다.\n" +
                    "에이전트가 하나라면 0으로 둡니다. " +
                    "<b>Decision Period보다 작아야 합니다.</b>"),

                new Item(null, "Take Actions Between Decisions",
                    "판단하지 않는 사이 차례에도 <b>직전에 정한 행동을 계속 적용할지</b>입니다.\n" +
                    "켜짐(기본) — 힘이 계속 걸려 부드럽게 움직입니다.\n" +
                    "꺼짐 — 판단한 순간에만 힘이 걸려 움직임이 뚝뚝 끊깁니다.\n" +
                    "Decision Period가 1이면 매번 판단하므로 이 설정은 아무 차이가 없습니다."),
            },
        };
    }
}

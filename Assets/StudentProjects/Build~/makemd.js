// PDF 와 같은 데이터(games2.js)로 개발 기획서 md 를 만든다.  node makemd.js <출력 폴더> [ID...]
const fs = require("fs");
const path = require("path");
const GAMES = require("./games2");

const outDir = process.argv[2];
const only = process.argv.slice(3);
const esc = (t) => String(t).replace(/\|/g, "\\|").replace(/\n/g, "<br>");
const table = (head, rows) => [`| ${head.join(" | ")} |`, `|${head.map(() => "---").join("|")}|`, ...rows.map((r) => `| ${r.map(esc).join(" | ")} |`)].join("\n");

function md(g) {
  const L = [];
  const p = (...xs) => xs.forEach((x) => L.push(x, ""));
  p(`# ${g.id}. ${g.title} — 개발 기획서`,
    `> ${g.concept}`,
    `| | |\n|---|---|\n| 영문 이름 | ${g.en} |\n| 난이도 | ${g.star} |\n| 핵심 개념 | ${g.newConcept} |\n| 같은 내용의 PDF | [${g.file}.pdf](${encodeURI(g.file)}.pdf) — 장면 그림 · 흐름도 · 그래프 |\n| 만드는 법 (구체적인 구성) | [${g.file}.md](${encodeURI(g.file)}.md) |`,
    "표에 적힌 숫자는 **예시 설계**다. 이 기획서대로 만들되, 바꿔 보고 바꾼 이유와 결과를 기록한다.",
    "## 목차",
    "1. 프로젝트 개요\n2. 게임 디자인 — 컨셉 · 플레이 방식 · 플레이 과정 · 점수와 승패 · 등장 요소 · 사람 조작\n3. 강화학습 설계 — 게임 → 강화학습 · 관측 · 행동 · 보상 · 보상이 만드는 플레이 · 에피소드\n4. 학습 계획 — 학습 환경 · 개발 단계 · 예상 문제와 대응",
    "---");

  p("## 1. 프로젝트 개요", table(["항목", "내용"], g.spec), `장면: ${g.sceneTxt}`);

  p("## 2. 게임 디자인", "### 2-1. 게임 컨셉", `**${g.pitch}**`, table(["", ""], g.pillars.map((x) => [`**${x.head}**`, x.body])));
  p("### 2-2. 플레이 방식", "한 판은 이렇게 흘러간다. 판이 끝날 때까지 **상황 보기 → 조작**을 반복한다.",
    "```\n" + g.loop.map((l) => `[${l.title}] ${l.sub}`).join("\n   ↓\n") + "\n   ↺ (판이 끝날 때까지 상황 보기 → 조작 반복)\n```");
  p("### 2-3. 플레이 과정 (단계별)");
  g.frames.forEach((f) => {
    p(`#### ${f.cap} — ${f.sub}`,
      "**게임에서 일어나는 일**", f.game.map((x) => `- ${x}`).join("\n"),
      `플레이어의 판단: *"${f.think}"*`,
      table(["강화학습으로 보면", ""], [["관측", f.rl.obs], ["행동", f.rl.act], ["보상", f.rl.rew]]));
  });
  const R = g.result;
  p("### 2-4. 점수와 승패", R.sub,
    `**${R.win.tag}**\n` + R.win.bullets.map((x) => `- ${x}`).join("\n"),
    `**${R.lose.tag}**\n` + R.lose.bullets.map((x) => `- ${x}`).join("\n"),
    table(["조건", "판정 방법 (코드에서)", "결과"], R.rows));
  p("### 2-5. 등장 요소", "매 판 바뀌는 것이 있어야 에이전트가 자리를 외우지 않는다.", table(["#", "오브젝트", "역할", "컴포넌트", "매 판"], g.objects));
  p("### 2-6. 사람 조작 (Heuristic)", table(["키", "하는 일"], g.keys), `- **왜 먼저 사람이 하나** — ${g.humanWhy}`, `- **기록해 둘 것** — ${g.humanRecord}`);

  p("## 3. 강화학습 설계", "### 3-1. 게임 → 강화학습", "결정할 때마다 관측을 보고 행동을 고르고, 그 결과로 보상을 받는다.", table(["강화학습", "", "이 게임에서"], g.rlMap));
  p(`### 3-2. 관측 설계 (Observation) — 총 ${g.obs.total}칸`, table(["항목", "칸", "왜 필요한가"], g.obs.rows), `칸 수를 Behavior Parameters 의 Space Size 와 맞춘다. ${g.obs.note}`);
  p(`### 3-3. 행동 설계 (Action) — ${g.act.type}`, table(["번호", "값", "하는 일", "키보드"], g.act.rows), g.act.note);
  p("### 3-4. 보상 설계 (Reward)", "한 판의 시간 순서:",
    "```\n" + g.rew.events.map(([lab, chip]) => (chip ? `${lab}(${chip})` : lab)).join(" ── ") + "  → 시간\n```",
    table(["사건", "보상", "판은?"], g.rew.rows), g.rew.note);
  const F = g.rewFlow;
  p("### 3-5. 보상이 만드는 플레이", (F.y ? F.y + " — " : "한 판의 누적 보상 — ") + "**예시 값, 실측 아님**:",
    table([F.x || "스텝", ...F.series.filter((x) => x[0]).map((x) => x[0])], F.labels.map((lab, i) => [lab, ...F.series.filter((x) => x[0]).map((x) => x[1][i])]).filter((_, i) => i % 2 === 0)),
    `- **이 보상이 유도하는 행동** — ${F.want}`, `- **넣지 않은 보상과 이유** — ${F.avoid}`);
  p("### 3-6. 에피소드 설계 (Episode)", table(["단계", "내용"], g.episode.map((e) => [`**${e.head}**`, e.body])), `> ${g.episodeBar}`);

  p("## 4. 학습 계획", "### 4-1. 학습 환경 구성", "영역을 복제해 한 두뇌가 동시에 여러 판을 경험한다.", table(["설정", "예시 값", "뜻"], g.train));
  p("### 4-2. 개발 단계", "완료 기준을 넘어야 다음 단계로 간다.", table(["단계", "만들 것", "완료 기준"], g.stages.map((t, i) => [`**${i + 1}. ${t.head}**`, t.body, t.done])));
  p("### 4-3. 예상 문제와 대응", table(["증상", "원인", "대응"], g.pit), "> 한 번에 하나만 바꾸고, 바꾼 이유와 결과를 숫자로 기록한다.");
  return L.join("\n");
}

for (const g of GAMES) {
  if (only.length && !only.includes(g.id)) continue;
  const f = path.join(outDir, `${g.file}-기획서.md`);
  fs.writeFileSync(f, md(g));
  console.log(f);
}

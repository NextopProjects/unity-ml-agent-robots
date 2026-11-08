// 개발 기획서형 덱.  node build2.js [ID...]
const fs = require("fs");
const path = require("path");
const { createDeck } = require("./deck");
const { scene } = require("./scenes");
const GAMES = require("./games2");

const OUT = path.join(__dirname, "out2");
fs.mkdirSync(OUT, { recursive: true });

const SECTIONS = [
  ["프로젝트 개요", "무엇을 만드는가 — 한 장 요약"],
  ["게임 디자인", "컨셉 · 플레이 방식 · 등장 요소 · 사람 조작"],
  ["강화학습 설계", "게임을 학습 문제로 — 관측 · 행동 · 보상 · 에피소드"],
  ["학습 계획", "학습 환경 · 개발 단계 · 예상 문제와 대응"],
];

function buildDeck(g) {
  const d = createDeck();
  const { C, M } = d;
  const short = `${g.id}. ${g.title}`;
  const col = (n) => C[n] || n;
  // 제목 오른쪽 위에 지금 장을 작게 표시한다
  const sec = (s, i) => d.txt(s, `${String(i).padStart(2, "0")}  ${SECTIONS[i - 1][0]}`, 6.4, 0.47, 3.0, 0.3, { fontSize: 10, color: C.ACC, bold: true, align: "right" });
  const page = (i, notes) => { d.setChapter(`${short} · ${SECTIONS[i - 1][0]}`); const s = d.newSlide(notes); sec(s, i); return s; };
  const exampleTag = (s, x, y) => d.tag(s, "예시 설계 — 바꿔 보고 기록", x, y, 2.3, C.ENV);
  // 스토리보드 한 컷: 데이터의 items 를 0~1 좌표로 그린다
  const frame = (s, f, x, y, w, h) => {
    const k = Math.min(w, h), X = (u) => x + u * w, Y = (v) => y + v * h;
    d.rect(s, x, y, w, h, C.BG, { rectRadius: 0.04 });
    const big = w > 2.5;
    const label = (t, cx, ty, c) => { if (big && t) d.txt(s, t, Math.max(x, Math.min(cx - 0.6, x + w - 1.2)), Math.min(ty, y + h - 0.27), 1.2, 0.25, { fontSize: 10, color: c, align: "center" }); };
    f.items.forEach((it) => {
      const c = col(it.c || "TEXT");
      if (it.t === "dot" || it.t === "ring") {
        const D = 2 * (it.r || 0.07) * k;
        if (it.t === "dot") d.circle(s, X(it.u) - D / 2, Y(it.v) - D / 2, D, c);
        else s.addShape(d.pres.shapes.OVAL, { x: X(it.u) - D / 2, y: Y(it.v) - D / 2, w: D, h: D, fill: { color: C.BG, transparency: 100 }, line: { color: c, width: 1.25, dashType: it.solid ? "solid" : "dash" } });
        label(it.lab, X(it.u), Y(it.v) + D / 2 + 0.04, c);
      } else if (it.t === "box" || it.t === "outline") {
        // 0~1 좌표의 사각형 (왼쪽 위 u,v · 크기 w,h)
        const bx = X(it.u), by = Y(it.v), bw = it.w * w, bh = it.h * h;
        if (it.t === "box") d.rect(s, bx, by, bw, bh, c, { rectRadius: 0.03 });
        else s.addShape(d.pres.shapes.RECTANGLE, { x: bx, y: by, w: bw, h: bh, fill: { color: C.BG, transparency: 100 }, line: { color: c, width: 1.5, dashType: "dash" } });
        label(it.lab, bx + bw / 2, by + bh + 0.04, c);
      } else if (it.t === "rbox") {
        // 가운데 u,v · 크기 w,h(짧은 변 기준) · 회전 deg
        const bw = it.w * k, bh = it.h * k;
        s.addShape(d.pres.shapes.RECTANGLE, { x: X(it.u) - bw / 2, y: Y(it.v) - bh / 2, w: bw, h: bh, rotate: it.deg || 0, fill: { color: c }, line: { color: c, width: 0 } });
        label(it.lab, X(it.u), Y(it.v) + Math.max(bw, bh) / 2 + 0.04, c);
      } else if (it.t === "stick") {
        // 아래 끝 u,v 에서 deg 만큼 기운 막대 (길이 len 은 짧은 변 기준)
        const L = it.len * k, a = ((it.deg || 0) * Math.PI) / 180, th = (it.th || 0.04) * k;
        const cx = X(it.u) + (L / 2) * Math.sin(a), cy = Y(it.v) - (L / 2) * Math.cos(a);
        s.addShape(d.pres.shapes.RECTANGLE, { x: cx - th / 2, y: cy - L / 2, w: th, h: L, rotate: it.deg || 0, fill: { color: c }, line: { color: c, width: 0 } });
        label(it.lab, X(it.u) + L * Math.sin(a), Y(it.v) - L * Math.cos(a) - 0.3, c);
      } else if (it.t === "text") {
        label(it.text, X(it.u), Y(it.v), c);
      } else if (it.t === "line") {
        const x1 = X(it.u1), y1 = Y(it.v1), x2 = X(it.u2), y2 = Y(it.v2), ln = { color: c, width: it.wd || 1.5, dashType: it.dash ? "dash" : "solid" };
        if (it.arrow) ln.endArrowType = "triangle";
        s.addShape(d.pres.shapes.LINE, { x: Math.min(x1, x2), y: Math.min(y1, y2), w: Math.max(Math.abs(x2 - x1), 0.001), h: Math.max(Math.abs(y2 - y1), 0.001), flipH: x2 < x1, flipV: y2 < y1, line: ln });
      } else if (it.t === "chip") {
        d.rect(s, X(it.u) - 0.25, Y(it.v), 0.5, 0.26, c, { rectRadius: 0.05 });
        d.txt(s, it.text, X(it.u) - 0.25, Y(it.v), 0.5, 0.26, { fontSize: 10, bold: true, color: C.BG, align: "center", valign: "middle" });
      } else if (it.t === "edge") {
        d.rect(s, x + w - 0.04, y, 0.04, h, C.NEG, { rectRadius: 0 });
      }
    });
  };

  // 표지
  d.setChapter("");
  let s = d.newSlide(`${g.title} 개발 기획서입니다. ${g.concept} 이 기획서대로 환경을 만들고 에이전트를 학습시키는 것이 이 프로젝트의 목표입니다.`);
  d.tag(s, `프로젝트 ${g.id}`, M, 0.95, 1.25, C.ACC);
  d.txt(s, g.title, M, 1.4, 5.0, 0.85, { fontSize: 40, bold: true });
  d.txt(s, g.en, M, 2.25, 5.0, 0.35, { fontSize: 15, color: C.MUTED });
  d.txt(s, g.concept, M, 2.85, 5.0, 1.0, { fontSize: 13.5 });
  d.txt(s, `난이도 ${g.star}   ·   핵심 개념: ${g.newConcept}`, M, 4.15, 5.0, 0.3, { fontSize: 11.5, color: C.ACC });
  d.txt(s, "ML-Agents 강화학습 프로젝트 · 개발 기획서", M, 4.55, 5.0, 0.3, { fontSize: 10.5, color: C.MUTED });
  d.rect(s, 5.75, 0.85, 3.65, 3.85, C.CARD);
  scene(d, s, g.scene, 5.95, 1.05, 3.25, 3.45);
  d.txt(s, g.sceneTxt, 5.75, 4.8, 3.65, 0.25, { fontSize: 9.5, color: C.MUTED, align: "center" });

  // 목차
  s = d.newSlide("기획서는 네 부분입니다. 무엇을 만드는지 요약하고, 사람이 하는 게임으로 먼저 설계한 뒤, 그 게임을 강화학습 문제로 옮기고, 마지막으로 어떻게 학습시킬지 계획합니다.");
  d.title(s, "목차", "게임을 먼저 설계하고, 그 게임을 강화학습 문제로 옮긴다");
  SECTIONS.forEach(([t, sub], i) => {
    const yy = 1.45 + i * 0.86;
    d.rect(s, M, yy, 8.8, 0.72, C.CARD);
    d.txt(s, String(i + 1).padStart(2, "0"), M + 0.25, yy, 0.7, 0.72, { fontSize: 22, bold: true, color: C.ACC, valign: "middle" });
    d.txt(s, t, M + 1.05, yy, 2.6, 0.72, { fontSize: 16, bold: true, valign: "middle" });
    d.txt(s, sub, M + 3.6, yy, 5.0, 0.72, { fontSize: 12, color: C.MUTED, valign: "middle" });
  });

  // 1. 개요
  s = page(1, `한 장 요약입니다. ${g.spec.map((r) => r.join(": ")).join(". ")}.`);
  d.title(s, "프로젝트 개요", g.concept);
  d.table(s, g.spec.map(([k, v]) => [d.cell(k, { bold: true, color: C.ACC }), d.cell(v)]), M, 1.4, 5.3, [1.25, 4.05], { fontSize: 11.5, rowH: 0.46 });
  d.rect(s, 6.15, 1.4, 3.25, 3.25, C.CARD);
  scene(d, s, g.scene, 6.3, 1.55, 2.95, 2.95);

  // 2. 게임 디자인
  s = page(2, `게임 컨셉입니다. 한 문장으로는 "${g.pitch}" 입니다. ${g.pillars.map((p) => `${p.head}: ${p.body}`).join(". ")}. 잘한 플레이와 못한 플레이를 구분해 두면 나중에 보상을 정하기 쉽습니다.`);
  d.title(s, "게임 컨셉", "이 게임은 무엇이고, 무엇이 잘한 플레이인가");
  d.statement(s, g.pitch, g.pillars.map((p, i) => ({ head: p.head, body: p.body })), 1.4);

  s = page(2, `한 판의 흐름입니다. ${g.loop.map((l) => `${l.title} — ${l.sub}`).join(", ")}. 상황 보기와 조작이 반복되는 이 고리가, 나중에 강화학습의 관측과 행동이 됩니다.`);
  d.title(s, "플레이 방식", "한 판은 이렇게 흘러간다");
  d.flow(s, g.loop.map((l, i) => ({ title: l.title, sub: l.sub, color: i === 1 ? C.ENV : i === 2 ? C.ACC : undefined })), 1.35, { h: 0.9 });
  d.txt(s, "한 판 예시", M, 2.4, 3, 0.28, { fontSize: 11, bold: true, color: C.ACC });
  g.frames.forEach((f, i) => {
    const fx = M + i * 2.25;
    d.rect(s, fx, 2.72, 2.05, 2.3, C.CARD);
    frame(s, f, fx + 0.12, 2.84, 1.81, 1.25);
    d.txt(s, f.cap, fx + 0.12, 4.17, 1.81, 0.3, { fontSize: 12, bold: true });
    d.txt(s, f.sub, fx + 0.12, 4.47, 1.81, 0.5, { fontSize: 10, color: C.MUTED });
    if (i < g.frames.length - 1) d.txt(s, "›", fx + 2.05, 3.2, 0.2, 0.4, { fontSize: 20, color: C.MUTED, align: "center" });
  });

  // 플레이 과정 — 한 컷에 한 쪽. 위의 진행 막대가 지금 단계를 표시한다
  g.frames.forEach((f, i) => {
    s = page(2, `플레이 과정 ${i + 1}단계, ${f.cap.slice(2)}입니다. 게임에서는 ${f.game.join(", ")}. 플레이어는 "${f.think}" 하고 판단합니다. 강화학습으로 보면 관측은 ${f.rl.obs}, 행동은 ${f.rl.act}, 보상은 ${f.rl.rew}입니다.`);
    d.title(s, `플레이 과정  ${f.cap}`, f.sub);
    g.frames.forEach((ff, j) => {
      const px = M + j * 2.225, on = j === i;
      d.rect(s, px, 1.32, 2.125, 0.32, on ? C.ACC : C.CARD2, { rectRadius: 0.14 });
      d.txt(s, ff.cap, px, 1.32, 2.125, 0.32, { fontSize: 10.5, bold: on, color: on ? C.BG : C.MUTED, align: "center", valign: "middle" });
    });
    d.rect(s, M, 1.8, 4.3, 3.2, C.CARD);
    frame(s, f, M + 0.15, 1.95, 4.0, 2.9);
    d.rect(s, 5.1, 1.8, 4.3, 1.5, C.CARD);
    d.txt(s, "게임에서 일어나는 일", 5.3, 1.88, 3.9, 0.3, { fontSize: 12, bold: true, color: C.ACC });
    d.bullets(s, f.game, 5.3, 2.2, 3.95, 1.1, 11);
    d.rect(s, 5.1, 3.4, 4.3, 0.55, C.CARD);
    d.tag(s, "플레이어의 판단", 5.25, 3.53, 1.35, C.ENV);
    d.txt(s, `"${f.think}"`, 6.7, 3.4, 2.6, 0.55, { fontSize: 11, valign: "middle" });
    d.rect(s, 5.1, 4.05, 4.3, 0.95, C.CARD2);
    [["관측", f.rl.obs, C.ENV], ["행동", f.rl.act, C.ACC], ["보상", f.rl.rew, C.POS]].forEach(([lab, v, c], j) => {
      const cx = 5.2 + j * 1.4;
      d.txt(s, lab, cx, 4.1, 1.3, 0.25, { fontSize: 9.5, bold: true, color: c });
      d.txt(s, v, cx, 4.35, 1.32, 0.62, { fontSize: 9.5 });
    });
  });

  const R = g.result;
  s = page(2, `점수와 승패입니다. ${R.sub}. 점수가 오르는 때: ${R.win.bullets.join(", ")}. 판이 끝나는 때: ${R.lose.bullets.join(", ")}. 아래 표의 판정 방법이 그대로 코드가 됩니다.`);
  d.title(s, "점수와 승패", R.sub);
  [[R.win, C.POS], [R.lose, C.NEG]].forEach(([c, colr], i) => {
    const x = M + i * 4.5;
    d.rect(s, x, 1.4, 4.3, 1.85, C.CARD);
    frame(s, g.frames[c.frame], x + 0.15, 1.55, 1.75, 1.55);
    d.tag(s, c.tag, x + 2.05, 1.55, 1.6, colr);
    d.bullets(s, c.bullets, x + 2.05, 1.95, 2.1, 1.25, 11);
  });
  d.table(s, [d.headerRow(["조건", "판정 방법 (코드에서)", "결과"]), ...R.rows.map((r) => [d.cell(r[0], { bold: true }), d.cell(r[1], { color: C.ENV }), d.cell(r[2])])], M, 3.45, 8.8, [1.6, 3.4, 3.8], { fontSize: 11, rowH: 0.38 });

  s = page(2, `장면에 들어가는 오브젝트입니다. ${g.objects.map((o) => `${o[1]}는 ${o[2]}`).join(", ")}. 매 판 바뀌는 것이 있어야 에이전트가 자리를 외우지 않습니다.`);
  d.title(s, "등장 요소", "장면에 무엇을 두고, 무엇이 매 판 바뀌는가");
  d.rect(s, M, 1.4, 3.4, 3.6, C.CARD);
  scene(d, s, g.scene, M + 0.15, 1.55, 3.1, 3.3);
  (g.marks || []).forEach(([u, v, n]) => {
    const cx = M + 0.15 + u * 3.1, cy = 1.55 + v * 3.3;
    d.circle(s, cx - 0.14 + 0.22, cy - 0.14 - 0.2, 0.28, C.TEXT);
    d.txt(s, String(n), cx - 0.14 + 0.22, cy - 0.14 - 0.2, 0.28, 0.28, { fontSize: 10, bold: true, color: C.BG, align: "center", valign: "middle" });
  });
  d.table(s, [d.headerRow(["#", "오브젝트", "역할", "컴포넌트", "매 판"]), ...g.objects.map((r) => r.map((c) => d.cell(c)))], 4.2, 1.4, 5.2, [0.35, 0.95, 1.05, 1.85, 1.0], { fontSize: 10.5, margin: 0.06, rowH: 0.62 });

  s = page(2, `사람 조작입니다. ${g.keys.map((k) => `${k[0]}는 ${k[1]}`).join(", ")}. ${g.humanWhy}. ${g.humanRecord}.`);
  d.title(s, "사람 조작 (Heuristic)", "학습 전에 사람이 먼저 플레이해 본다");
  d.rect(s, M, 1.4, 4.2, 3.6, C.CARD);
  const kz = 0.62, cross = { W: [1, 0], A: [0, 1], S: [1, 1], D: [2, 1], "↑": [1, 0], "←": [0, 1], "↓": [1, 1], "→": [2, 1] };
  const isCross = g.keys.every((k) => cross[k[0]]);
  g.keys.forEach(([k, lab], i) => {
    const nc = g.keys.length <= 4 ? 2 : 3, [cx, cy] = isCross ? cross[k] : [i % nc, Math.floor(i / nc)];
    const kx = M + 0.55 + cx * (kz + 0.55), ky = 1.75 + cy * (kz + 0.6);
    d.rect(s, kx, ky, kz, kz, C.ACC, { rectRadius: 0.06 });
    d.txt(s, k, kx, ky, kz, kz, { fontSize: 18, bold: true, color: C.BG, align: "center", valign: "middle" });
    d.txt(s, lab, kx - 0.3, ky + kz + 0.04, kz + 0.6, 0.3, { fontSize: 10, color: C.TEXT, align: "center" });
  });
  d.rect(s, 5.0, 1.4, 4.4, 1.7, C.CARD);
  d.txt(s, "왜 먼저 사람이 하나", 5.2, 1.52, 4.0, 0.32, { fontSize: 13.5, bold: true, color: C.ACC });
  d.txt(s, g.humanWhy, 5.2, 1.88, 4.0, 1.15, { fontSize: 11.5 });
  d.rect(s, 5.0, 3.3, 4.4, 1.7, C.CARD);
  d.txt(s, "기록해 둘 것", 5.2, 3.42, 4.0, 0.32, { fontSize: 13.5, bold: true, color: C.POS });
  d.txt(s, g.humanRecord, 5.2, 3.78, 4.0, 1.15, { fontSize: 11.5 });

  // 3. 강화학습 설계
  s = page(3, `이제 게임을 강화학습 문제로 옮깁니다. ${g.rlMap.map((r) => `${r[1]}은 ${r[2]}`).join(". ")}. 이 대응표가 기획서의 중심입니다.`);
  d.title(s, "게임 → 강화학습", "게임의 각 요소가 강화학습의 무엇이 되는가");
  d.table(s, [d.headerRow(["강화학습", "", "이 게임에서"]), ...g.rlMap.map(([en, ko, v]) => [d.cell(en, { bold: true, color: C.ACC }), d.cell(ko, { color: C.MUTED }), d.cell(v)])], M, 1.4, 5.6, [1.4, 0.95, 3.25], { fontSize: 11.5, rowH: 0.48 });
  d.rect(s, 6.4, 1.4, 3.0, 3.35, C.CARD);
  d.cycle(s, 6.55, 1.85, 2.7, 1.9, "all", { small: true, left: { title: "Agent", sub: g.rlMap[0][2] }, right: { title: "Env", sub: "환경" }, topLabel: "Action", bottomLabel: "Obs · Reward" });
  d.txt(s, "결정할 때마다 관측을 보고 행동을 고르고, 그 결과로 보상을 받는다", 6.55, 3.85, 2.7, 0.8, { fontSize: 10, color: C.MUTED, align: "center" });

  const loopCard = (ss, hl, cap) => {
    d.rect(ss, 6.4, 1.4, 3.0, 2.3, C.CARD);
    d.cycle(ss, 6.55, 1.55, 2.7, 1.9, hl, { small: true, left: { title: "Agent", sub: "에이전트" }, right: { title: "Env", sub: "환경" }, topLabel: "Action", bottomLabel: "Obs · Reward" });
    d.txt(ss, cap, 6.4, 3.8, 3.0, 0.5, { fontSize: 9.5, color: C.MUTED, align: "center" });
  };

  s = page(3, `관측 설계입니다. 예시로 ${g.obs.rows.map((r) => `${r[0]} ${r[1]}칸`).join(", ")}, 모두 ${g.obs.total}칸입니다. ${g.obs.note}`);
  d.title(s, "관측 설계  Observation", "에이전트가 결정할 때마다 보는 숫자");
  exampleTag(s, M, 1.4);
  d.tag(s, `= ${g.obs.total}칸`, M + 2.45, 1.4, 0.9, C.ACC);
  d.table(s, [d.headerRow(["항목", "칸", "왜 필요한가"]), ...g.obs.rows.map((r) => [d.cell(r[0], { bold: true }), d.cell(r[1], { align: "center" }), d.cell(r[2])])], M, 1.85, 5.6, [1.7, 0.5, 3.4], { fontSize: 11, rowH: 0.55 });
  loopCard(s, "bottom", "환경 → 에이전트. 칸 수를 Behavior Parameters 의 Space Size 와 맞춘다");
  d.bottomBar(s, g.obs.note, 4.4, 0.6, 12);

  s = page(3, `행동 설계입니다. 예시는 ${g.act.type}입니다. ${g.act.rows.map((r) => `${r[0]}번은 ${r[2]}, 키보드 ${r[3]}`).join(". ")}. ${g.act.note}`);
  d.title(s, "행동 설계  Action", "에이전트가 결정할 때마다 고르는 값");
  exampleTag(s, M, 1.4);
  d.tag(s, g.act.type, M + 2.45, 1.4, 0.9, C.ACC);
  d.table(s, [d.headerRow(["번호", "값", "하는 일", "키보드"]), ...g.act.rows.map((r) => r.map((c, j) => d.cell(c, j === 0 || j === 3 ? { align: "center" } : {})))], M, 1.85, 5.6, [0.7, 1.1, 2.6, 1.2], { fontSize: 11, rowH: 0.55 });
  loopCard(s, "top", "에이전트 → 환경. 행동 순서 = 키보드 순서로 맞추면 Heuristic 이 쉽다");
  d.bottomBar(s, g.act.note, 4.4, 0.6, 12);

  s = page(3, `보상 설계입니다. 한 판을 시간 순서로 그리면 위와 같습니다. ${g.rew.rows.map((r) => `${r[0]}: ${r[1]}, ${r[2]}`).join(". ")}. ${g.rew.note}`);
  d.title(s, "보상 설계  Reward", "한 판 동안 언제, 얼마를 받는가");
  exampleTag(s, 7.1, 1.0);
  const ev = g.rew.events, x0 = 1.2, xe = 8.8, ty = 2.05;
  d.rect(s, x0, ty, xe - x0, 0.04, C.MUTED, { rectRadius: 0 });
  d.arrow(s, xe - 0.1, ty + 0.02, xe + 0.25, ty + 0.02, C.MUTED);
  d.txt(s, "시간", xe - 0.05, ty - 0.32, 0.6, 0.25, { fontSize: 9, color: C.MUTED });
  ev.forEach(([lab, chip, c], i) => {
    const cx = x0 + (i * (xe - x0 - 0.4)) / (ev.length - 1);
    d.circle(s, cx - 0.09, ty - 0.07, 0.18, col(c));
    if (chip) { d.rect(s, cx - 0.32, ty - 0.62, 0.64, 0.38, col(c), { rectRadius: 0.06 }); d.txt(s, chip, cx - 0.32, ty - 0.62, 0.64, 0.38, { fontSize: 12, bold: true, color: C.BG, align: "center", valign: "middle" }); }
    d.txt(s, lab, cx - 0.7, ty + 0.2, 1.4, 0.3, { fontSize: 10, color: C.TEXT, align: "center" });
  });
  d.table(s, [d.headerRow(["사건", "보상", "판은?"]), ...g.rew.rows.map((r) => [d.cell(r[0]), d.cell(r[1], { align: "center", bold: true, color: r[1].startsWith("+") ? C.POS : r[1].startsWith("−") || r[1].startsWith("-") ? C.NEG : C.TEXT }), d.cell(r[2])])], M, 2.75, 8.8, [3.2, 1.2, 4.4], { fontSize: 11, rowH: 0.36 });
  d.bottomBar(s, g.rew.note, 4.45, 0.55, 12);

  const F = g.rewFlow;
  s = page(3, `이 보상으로 한 판의 누적 보상을 그려 보면 플레이마다 이렇게 달라집니다. 예시 곡선이며 실측이 아닙니다. 이 보상이 유도하는 행동은 ${F.want}. 넣지 않은 보상은 ${F.avoid}.`);
  d.title(s, "보상이 만드는 플레이", "보상 설계에 따라 어떤 플레이가 높은 점수를 받는가");
  d.tag(s, "예시 곡선 — 실측 아님", M, 1.38, 1.9, C.ENV);
  d.lineChart(s, F.series.filter((x) => x[0]).map(([name, values]) => ({ name, labels: F.labels, values })), M, 1.7, 5.3, 3.3, { xTitle: F.x || "스텝", yTitle: F.y || "누적 보상", colors: [C.POS, C.ACC, C.NEG] });
  d.rect(s, 6.1, 1.4, 3.3, 1.75, C.CARD);
  d.txt(s, "이 보상이 유도하는 행동", 6.3, 1.52, 3.0, 0.3, { fontSize: 12.5, bold: true, color: C.POS });
  d.txt(s, F.want, 6.3, 1.86, 2.95, 1.25, { fontSize: 11 });
  d.rect(s, 6.1, 3.25, 3.3, 1.75, C.CARD);
  d.txt(s, "넣지 않은 보상과 이유", 6.3, 3.37, 3.0, 0.3, { fontSize: 12.5, bold: true, color: C.NEG });
  d.txt(s, F.avoid, 6.3, 3.71, 2.95, 1.25, { fontSize: 11 });

  s = page(3, `에피소드 설계입니다. ${g.episode.map((e) => `${e.head}: ${e.body.replace(/\n/g, ", ")}`).join(". ")}. ${g.episodeBar}`);
  d.title(s, "에피소드 설계  Episode", "한 판은 언제 시작하고, 무엇이 바뀌고, 언제 끝나는가");
  d.cardRow(s, g.episode.map((e, i) => ({ head: e.head, headColor: [C.ENV, C.ACC, C.NEG][i], body: e.body })), 1.4, 2.85, { arrows: true, bodySize: 13, headSize: 13 });
  d.bottomBar(s, g.episodeBar, 4.45, 0.55, 12);

  // 4. 학습 계획
  s = page(4, `학습 환경 설정입니다. ${g.train.map((r) => `${r[0]} ${r[1]}`).join(", ")}. 같은 영역을 여러 개 복제하면 같은 시간에 경험이 그만큼 더 쌓입니다.`);
  d.title(s, "학습 환경 구성", "영역을 복제해 한 두뇌가 동시에 여러 판을 경험한다");
  d.rect(s, M, 1.4, 3.5, 3.6, C.CARD);
  for (let r = 0; r < 3; r++) for (let c = 0; c < 3; c++) {
    const bx = M + 0.2 + c * 1.05, by = 1.6 + r * 0.95;
    d.rect(s, bx, by, 0.95, 0.85, C.CARD2, { rectRadius: 0.04 });
    scene(d, s, g.scene, bx + 0.05, by + 0.05, 0.85, 0.75, true);
  }
  d.txt(s, "같은 Behavior Name 의 영역 9개 → 한 두뇌", M, 4.5, 3.5, 0.3, { fontSize: 9.5, color: C.MUTED, align: "center" });
  d.table(s, [d.headerRow(["설정", "예시 값", "뜻"]), ...g.train.map((r) => [d.cell(r[0], { bold: true }), d.cell(r[1], { color: C.ACC, bold: true }), d.cell(r[2])])], 4.3, 1.4, 5.1, [1.5, 1.2, 2.4], { fontSize: 10.5, rowH: 0.5 });

  s = page(4, `개발 단계입니다. ${g.stages.map((t, i) => `${i + 1}단계 ${t.head}: ${t.body.replace(/\n/g, ", ")}. 완료 기준은 ${t.done}`).join(". ")}. 완료 기준을 넘어야 다음 단계로 갑니다.`);
  d.title(s, "개발 단계", "단계마다 만들 것과, 다음으로 넘어가는 기준");
  g.stages.forEach((t, i) => {
    const x = M + i * 2.25, w = 2.05;
    d.rect(s, x, 1.4, w, 3.6, C.CARD);
    d.rect(s, x, 1.4, w, 0.75, i === g.stages.length - 1 ? C.ACC : C.CARD2);
    d.txt(s, `${i + 1}단계`, x + 0.15, 1.45, w - 0.3, 0.3, { fontSize: 10, bold: true, color: i === g.stages.length - 1 ? C.BG : C.ACC });
    d.txt(s, t.head, x + 0.15, 1.72, w - 0.3, 0.38, { fontSize: 14, bold: true, color: i === g.stages.length - 1 ? C.BG : C.TEXT });
    d.txt(s, "만들 것", x + 0.15, 2.3, w - 0.3, 0.28, { fontSize: 10, bold: true, color: C.MUTED });
    d.txt(s, t.body, x + 0.15, 2.6, w - 0.3, 1.3, { fontSize: 11.5 });
    d.rect(s, x + 0.12, 3.98, w - 0.24, 0.9, C.BG);
    d.txt(s, "완료 기준", x + 0.25, 4.03, w - 0.5, 0.25, { fontSize: 9.5, bold: true, color: C.POS });
    d.txt(s, t.done, x + 0.25, 4.3, w - 0.5, 0.55, { fontSize: 10.5 });
    if (i < g.stages.length - 1) d.txt(s, "›", x + w, 2.9, 0.2, 0.5, { fontSize: 22, color: C.MUTED, align: "center" });
  });

  s = page(4, `예상되는 문제와 대응입니다. ${g.pit.map((p) => `${p[0]} — 원인: ${p[1]}, 대응: ${p[2]}`).join(". ")}. 학습은 처음엔 거의 항상 엉뚱한 쪽으로 갑니다. 하나씩 바꾸고 숫자로 비교합니다.`);
  d.title(s, "예상 문제와 대응", "학습이 엉뚱하게 갈 때 먼저 볼 곳");
  g.pit.forEach(([sym, cause, fix], i) => {
    const yy = 1.4 + i * 1.12;
    d.rect(s, M, yy, 8.8, 0.98, C.CARD);
    d.txt(s, sym, M + 0.25, yy, 2.6, 0.98, { fontSize: 13, bold: true, color: C.NEG, valign: "middle" });
    d.tag(s, "원인", M + 2.95, yy + 0.16, 0.7, C.MUTED);
    d.txt(s, cause, M + 3.75, yy + 0.13, 4.9, 0.34, { fontSize: 11.5 });
    d.tag(s, "대응", M + 2.95, yy + 0.55, 0.7, C.POS);
    d.txt(s, fix, M + 3.75, yy + 0.52, 4.9, 0.34, { fontSize: 11.5 });
  });
  d.txt(s, "한 번에 하나만 바꾸고, 바꾼 이유와 결과를 숫자로 기록한다", M, 4.8, 8.8, 0.3, { fontSize: 12, bold: true, color: C.ACC, align: "center" });

  return d;
}

(async () => {
  const only = process.argv.slice(2);
  for (const g of GAMES) {
    if (only.length && !only.includes(g.id)) continue;
    const r = await buildDeck(g).save(path.join(OUT, `${g.file}.pptx`));
    console.log(g.id, r.slides, "slides");
  }
})();

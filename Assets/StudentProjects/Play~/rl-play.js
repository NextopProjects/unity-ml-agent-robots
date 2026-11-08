// rl-play.js — 게임 하나를 ML-Agents 처럼 돌려 보는 작은 엔진.
// 게임 파일은 RLPlay.start({...}) 에 '게임 규칙'만 넘긴다. 엔진은
//   OnEpisodeBegin → (결정마다) CollectObservations → OnActionReceived → AddReward/EndEpisode
// 순서를 Unity 와 같은 박자(물리 0.02초, Decision Period)로 돌리고, 그 과정을 화면에 보여 준다.
(function () {
  const C = { BG: "#15161B", CARD: "#21232B", CARD2: "#2C2F3A", TEXT: "#EDEEF0", MUTED: "#9AA0AB", ACC: "#F2B65B", ENV: "#6C9BD2", POS: "#6CC59B", NEG: "#E06F63" };
  const $ = (s) => document.querySelector(s);
  const el = (tag, attrs = {}, html = "") => { const e = document.createElement(tag); Object.assign(e, attrs); if (html) e.innerHTML = html; return e; };
  const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
  const fmt = (v) => (typeof v === "number" ? (Math.abs(v) >= 100 ? v.toFixed(0) : v.toFixed(2)) : String(v));
  const rnd = (a, b) => a + Math.random() * (b - a);

  // 월드 좌표(y 위쪽)를 캔버스에 그리는 도우미
  function makeDraw(ctx, view, W, H) {
    const sx = W / (view.x1 - view.x0), sy = H / (view.y1 - view.y0), s = Math.min(sx, sy);
    const ox = (W - (view.x1 - view.x0) * s) / 2, oy = (H - (view.y1 - view.y0) * s) / 2;
    const X = (x) => ox + (x - view.x0) * s, Y = (y) => H - oy - (y - view.y0) * s;
    const g = {
      C, s, X, Y,
      clear() { ctx.fillStyle = C.CARD; ctx.fillRect(0, 0, W, H); },
      rect(x, y, w, h, color, o = {}) { // 왼쪽 아래 (x, y)
        ctx.fillStyle = color; ctx.globalAlpha = o.alpha ?? 1;
        ctx.beginPath(); ctx.roundRect ? ctx.roundRect(X(x), Y(y + h), w * s, h * s, o.r ?? 4) : ctx.rect(X(x), Y(y + h), w * s, h * s); ctx.fill(); ctx.globalAlpha = 1;
      },
      box(cx, cy, w, h, angle, color, o = {}) { // 가운데 (cx, cy), 라디안 회전
        ctx.save(); ctx.translate(X(cx), Y(cy)); ctx.rotate(-angle); ctx.fillStyle = color; ctx.globalAlpha = o.alpha ?? 1;
        ctx.fillRect((-w / 2) * s, (-h / 2) * s, w * s, h * s); ctx.restore(); ctx.globalAlpha = 1;
      },
      outline(x, y, w, h, color, o = {}) { ctx.strokeStyle = color; ctx.lineWidth = o.width ?? 2; ctx.setLineDash(o.dash ?? [6, 4]); ctx.strokeRect(X(x), Y(y + h), w * s, h * s); ctx.setLineDash([]); },
      circle(x, y, r, color, o = {}) {
        ctx.beginPath(); ctx.arc(X(x), Y(y), Math.max(1, r * s), 0, Math.PI * 2);
        if (o.stroke) { ctx.strokeStyle = color; ctx.lineWidth = o.width ?? 2; ctx.setLineDash(o.dash ?? []); ctx.stroke(); ctx.setLineDash([]); }
        else { ctx.fillStyle = color; ctx.globalAlpha = o.alpha ?? 1; ctx.fill(); ctx.globalAlpha = 1; }
      },
      line(x1, y1, x2, y2, color, o = {}) {
        ctx.strokeStyle = color; ctx.lineWidth = o.width ?? 2; ctx.setLineDash(o.dash ?? []); ctx.globalAlpha = o.alpha ?? 1;
        ctx.beginPath(); ctx.moveTo(X(x1), Y(y1)); ctx.lineTo(X(x2), Y(y2)); ctx.stroke(); ctx.setLineDash([]); ctx.globalAlpha = 1;
        if (o.arrow) { const a = Math.atan2(Y(y2) - Y(y1), X(x2) - X(x1)), L = 9; ctx.fillStyle = color; ctx.beginPath(); ctx.moveTo(X(x2), Y(y2)); ctx.lineTo(X(x2) - L * Math.cos(a - 0.4), Y(y2) - L * Math.sin(a - 0.4)); ctx.lineTo(X(x2) - L * Math.cos(a + 0.4), Y(y2) - L * Math.sin(a + 0.4)); ctx.fill(); }
      },
      text(t, x, y, color = C.MUTED, size = 12, align = "center") { ctx.fillStyle = color; ctx.font = `${size}px "Noto Sans KR", sans-serif`; ctx.textAlign = align; ctx.textBaseline = "middle"; ctx.fillText(t, X(x), Y(y)); },
      tri(points, color) { ctx.fillStyle = color; ctx.beginPath(); points.forEach(([x, y], i) => (i ? ctx.lineTo(X(x), Y(y)) : ctx.moveTo(X(x), Y(y)))); ctx.closePath(); ctx.fill(); },
      ctx, W, H,
    };
    return g;
  }

  function start(def) {
    const dt = def.dt || 0.02, DP = def.decisionPeriod || 5;
    const act = def.act;
    const nAct = act.kind === "discrete" ? 1 : act.names.length;
    const modes = [["human", "사람 (Heuristic)"], ["random", "무작위 (학습 전)"], ["rule", "규칙 (학습 후 예시)"]];
    if (def.demo) modes.push(["bc", "따라 하기 (BC 흉내)"]);
    const qp = new URLSearchParams(location.search); // ?mode=random · rule · bc, ?speed=4
    let mode = modes.some((m) => m[0] === qp.get("mode")) ? qp.get("mode") : "human", speed = +(qp.get("speed") || 1), paused = false;
    const S = { params: {} };
    const E = { episode: 0, step: 0, ret: 0, lastAct: new Array(nAct).fill(0), lastObs: [], history: [], log: [], counts: {}, reason: "-", demo: [], lesson: 0 };
    const keys = new Set();
    const pending = []; // 턴 방식 게임의 키 입력 대기열

    // ---------- 화면 만들기 ----------
    document.title = `${def.id}. ${def.title} — 플레이 초안`;
    document.body.innerHTML = "";
    const header = el("header", {}, `<span class="nav"><a href="index.html">← 전체 목록</a> · <a href="../Games/${def.file}.pdf">기획서 PDF</a> · <a href="../Games/${def.file}-기획서.md">기획서 md</a></span><h1>${def.id}. ${def.title} <span class="mu" style="font-size:15px">${def.en || ""}</span></h1><p>${def.concept}</p>`);
    document.body.append(header);
    const main = el("main"); document.body.append(main);
    const stage = el("div", { className: "stage" });
    const cv = el("canvas", { id: "game", width: def.canvas?.[0] || 560, height: def.canvas?.[1] || 560 });
    stage.append(cv);
    const keyHelp = el("div", { className: "card keys", style: "margin-top:10px" }, `<h3>사람 조작</h3>${def.keys.map(([k, t]) => `<span>${k}</span>${t}`).join(" &nbsp; ")}<div class="hint" style="margin-top:6px">${def.turnBased ? "턴 방식 — 키를 누를 때마다 한 결정(한 칸)이 진행된다." : "키를 누르고 있는 동안 그 행동이 결정마다 들어간다."} 화면을 한 번 클릭한 뒤 조작한다.</div>`);
    stage.append(keyHelp);
    main.append(stage);
    const side = el("div", { className: "side" }); main.append(side);

    const ctl = el("div", { className: "card" }, `<h3>누가 행동을 고르나 — Behavior Type</h3><div class="row" id="modes"></div><div class="row" style="margin-top:8px"><span class="hint">속도</span><span id="speeds" class="row"></span><button id="pause">일시정지</button><button id="stepb">한 결정 진행</button><button id="reset">판 다시</button></div><div class="hint" id="modehint" style="margin-top:6px"></div>`);
    side.append(ctl);
    const modeHints = { human: "Heuristic — 사람이 키보드로 행동을 고른다. 학습 전에 사람이 먼저 풀 수 있어야 한다.", random: "학습 전의 에이전트 — 정책이 아무것도 모르면 행동은 거의 무작위다. 이 상태에서 보상을 받아 가며 배운다.", rule: "학습이 잘 됐을 때의 모습을 흉내 낸 손 규칙이다 (실제 신경망 아님). 목표로 삼을 플레이.", bc: "녹화한 시연에서 지금 관측과 가장 비슷한 장면을 찾아 그때 사람이 한 행동을 따라 한다 (BC 의 아이디어)." };
    modes.forEach(([k, t]) => { const b = el("button", { textContent: t }); b.onclick = () => { mode = k; refreshButtons(); }; b.dataset.m = k; $("#modes").append(b); });
    [1, 4, 16].forEach((v) => { const b = el("button", { textContent: v + "×" }); b.dataset.s = v; b.onclick = () => { speed = v; refreshButtons(); }; $("#speeds").append(b); });
    $("#pause").onclick = () => { paused = !paused; $("#pause").textContent = paused ? "계속" : "일시정지"; };
    $("#stepb").onclick = () => { paused = true; $("#pause").textContent = "계속"; runUntilDecision(); };
    $("#reset").onclick = () => beginEpisode("판 다시 (버튼)");
    function refreshButtons() {
      document.querySelectorAll("#modes button").forEach((b) => b.classList.toggle("on", b.dataset.m === mode));
      document.querySelectorAll("#speeds button").forEach((b) => b.classList.toggle("on", +b.dataset.s === speed));
      $("#modehint").textContent = modeHints[mode] + (mode === "bc" ? ` (녹화된 장면 ${E.demo.length}개)` : "");
    }

    const stats = el("div", { className: "card" }, `<h3>에피소드</h3><div class="stats"><div class="stat"><b id="sEp">0</b><span>판 번호</span></div><div class="stat"><b id="sStep">0</b><span>StepCount / ${def.maxStep}</span></div><div class="stat"><b id="sRet">0</b><span>이번 판 누적 보상</span></div><div class="stat"><b id="sAvg">-</b><span>최근 20판 평균</span></div></div><div class="hint" style="margin-top:6px">지난 판이 끝난 이유: <b id="sWhy" class="mu">-</b>${def.curriculum ? ` · <span class="lesson">커리큘럼 단계 <b id="sLesson">1</b> (${def.curriculum.param} = <b id="sParam"></b>)</span>` : ""}${def.demo ? ` · 녹화된 장면 <b id="sDemo">0</b>개 (사람 모드에서 자동 녹화)` : ""}</div><canvas id="hist" width="420" height="70" style="width:100%;height:70px;margin-top:6px;background:${C.BG};border-radius:6px"></canvas>`);
    side.append(stats);

    const methods = [["OnEpisodeBegin", ""], ["CollectObservations", ""], ["OnActionReceived", ""], ["Heuristic", ""], ["AddReward", "pos"], ["EndEpisode", "neg"]];
    const mcard = el("div", { className: "card" }, `<h3>지금 불리는 ML-Agents 메서드</h3><div class="methods">${methods.map(([m]) => `<div class="m" id="m_${m}">${m}()<small id="c_${m}">0</small></div>`).join("")}</div>`);
    side.append(mcard);
    const flashT = {};
    function flash(m, kind) {
      E.counts[m] = (E.counts[m] || 0) + 1;
      const d = $("#m_" + m); if (!d) return;
      d.classList.add("flash"); if (kind) d.classList.add(kind);
      clearTimeout(flashT[m]); flashT[m] = setTimeout(() => d.classList.remove("flash", "pos", "neg"), 260);
    }

    const io = el("div", { className: "card grid2" }, `<div><h3>관측 Observation (${def.obs.length}칸)</h3><table class="vals" id="obsT"></table></div><div><h3>행동 Action (${act.kind === "discrete" ? `이산 ${act.labels.length}` : `연속 ${nAct}`})</h3><table class="vals" id="actT"></table><h3 style="margin-top:10px">보상 기록</h3><ul class="log" id="log"></ul></div>`);
    side.append(io);
    $("#obsT").innerHTML = def.obs.map((o, i) => `<tr><td class="n">${o}</td><td class="v" id="ov${i}">0</td><td><div class="bar"><i id="ob${i}"></i></div></td></tr>`).join("");
    $("#actT").innerHTML = act.kind === "discrete"
      ? `<tr><td class="n">고른 행동</td><td class="v" id="av0">-</td><td></td></tr>` + act.labels.map((l, i) => `<tr><td class="n">${i}: ${l}</td><td class="v"></td><td><div class="bar act"><i id="ab${i}"></i></div></td></tr>`).join("")
      : act.names.map((n, i) => `<tr><td class="n">${i}: ${n}</td><td class="v" id="av${i}">0</td><td><div class="bar act"><i id="ab${i}"></i></div></td></tr>`).join("");

    // 아래쪽: 강화학습 붙이기
    const bottom = el("div", { className: "full grid2" });
    bottom.append(el("div", { className: "card" }, `<h3>보상 · 판의 끝 (기획서의 예시 설계)</h3><table class="rules"><tr><th>사건</th><th>보상</th><th>판은?</th></tr>${def.rewards.map((r) => `<tr><td>${r[0]}</td><td class="${String(r[1]).startsWith("+") ? "p" : String(r[1]).startsWith("−") || String(r[1]).startsWith("-") ? "n" : ""}">${r[1]}</td><td>${r[2]}</td></tr>`).join("")}</table><div class="steps" style="margin-top:12px"><h3>Unity 에서 이렇게 붙인다</h3><ol>${(def.howto || []).map((t) => `<li>${t}</li>`).join("")}</ol></div>`));
    bottom.append(el("div", { className: "card" }, `<h3>에이전트 코드 뼈대 (C#) — 이 화면의 규칙과 같은 내용</h3><pre>${def.code.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/(\/\/[^\n]*)/g, '<span class="c">$1</span>')}</pre>`));
    main.append(bottom);

    // ---------- 키 입력 ----------
    const norm = (e) => (e.key.length === 1 ? e.key.toUpperCase() : e.key);
    window.addEventListener("keydown", (e) => {
      const k = norm(e); if (["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", " "].includes(e.key)) e.preventDefault();
      if (!keys.has(k) && def.turnBased && mode === "human") pending.push(k);
      keys.add(k);
    });
    window.addEventListener("keyup", (e) => keys.delete(norm(e)));
    window.addEventListener("blur", () => keys.clear());

    // ---------- 에피소드 진행 ----------
    function logEvent(text, kind) { E.log.unshift({ step: E.step, text, kind }); E.log.length = Math.min(E.log.length, 30); }
    function beginEpisode(why) {
      if (why) { E.reason = why; }
      if (def.curriculum) {
        const cu = def.curriculum, recent = E.history.slice(-cu.window);
        const avg = recent.length ? recent.reduce((a, b) => a + b, 0) / recent.length : 0;
        if (recent.length >= cu.window && avg > cu.threshold && E.lesson < cu.lessons.length - 1) { E.lesson++; E.history.length = 0; logEvent(`단계 ↑ — 최근 ${cu.window}판 평균 ${avg.toFixed(2)} > ${cu.threshold}`, "p"); }
        S.params[cu.param] = cu.lessons[E.lesson];
      }
      E.episode++; E.step = 0; E.ret = 0; E.ended = false;
      def.reset(S);
      flash("OnEpisodeBegin");
      logEvent(`판 ${E.episode} 시작 — OnEpisodeBegin()`, "mu");
    }
    function endEpisode(reason) {
      E.history.push(E.ret); if (E.history.length > 200) E.history.shift();
      flash("EndEpisode", "neg");
      logEvent(`EndEpisode() — ${reason} · 이번 판 ${E.ret.toFixed(2)}`, "n");
      E.ended = true; E.endTimer = 0.5; E.endReason = reason;
    }
    function chooseAction(obs) {
      if (mode === "human") { flash("Heuristic"); return def.heuristic(keys, pending); }
      if (mode === "rule") return def.rule(S, obs);
      if (mode === "bc") return bcAction(obs);
      if (act.kind === "discrete") return [Math.floor(Math.random() * act.labels.length)];
      return Array.from({ length: nAct }, () => rnd(-1, 1));
    }
    function bcAction(obs) {
      if (!E.demo.length) return act.kind === "discrete" ? [0] : new Array(nAct).fill(0);
      let best = null, bd = Infinity;
      for (const d of E.demo) { let s = 0; for (let i = 0; i < obs.length; i++) { const t = obs[i] - d.o[i]; s += t * t; } if (s < bd) { bd = s; best = d; } }
      return best.a.slice();
    }
    // 물리 한 스텝 (0.02초). 결정 스텝이면 관측 → 행동을 새로 고른다
    function physicsStep() {
      if (E.ended) return;
      if (E.step % DP === 0) {
        if (def.turnBased && mode === "human" && !pending.length) return; // 사람 입력을 기다린다
        const obs = def.observe(S); E.lastObs = obs; flash("CollectObservations");
        const a = chooseAction(obs);
        if (def.demo && mode === "human" && (act.kind === "discrete" || a.some((v) => v !== 0))) { E.demo.push({ o: obs.slice(), a: a.slice() }); if (E.demo.length > 4000) E.demo.shift(); }
        E.lastAct = a.map((v) => (act.kind === "discrete" ? v : clamp(v, -1, 1)));
        flash("OnActionReceived");
      }
      const r = def.step(S, E.lastAct, dt, E) || {};
      E.step++;
      if (r.reward) {
        E.ret += r.reward;
        if (r.event) { flash("AddReward", r.reward > 0 ? "pos" : "neg"); const lv = r.logValue ?? r.reward; logEvent(`${lv > 0 ? "+" : ""}${+lv.toFixed(3)} — ${r.event}`, lv > 0 ? "p" : "n"); }
      } else if (r.event) logEvent(r.event, "mu");
      if (r.done) endEpisode(r.done);
      else if (E.step >= def.maxStep) endEpisode(`Max Step ${def.maxStep} 도달`);
    }
    function runUntilDecision() { let guard = 0; do { if (E.ended) { beginEpisode(E.endReason); } physicsStep(); guard++; } while (E.step % DP !== 0 && !E.ended && guard < 1000); render(); }

    // ---------- 그리기 ----------
    const ctx = cv.getContext("2d");
    const hctx = $("#hist").getContext("2d");
    function render() {
      const g = makeDraw(ctx, def.view, cv.width, cv.height);
      g.clear(); def.draw(g, S, E);
      if (E.ended) { ctx.fillStyle = "rgba(21,22,27,.65)"; ctx.fillRect(0, cv.height / 2 - 30, cv.width, 60); ctx.fillStyle = C.ACC; ctx.font = 'bold 18px "Noto Sans KR"'; ctx.textAlign = "center"; ctx.textBaseline = "middle"; ctx.fillText(`판 끝 — ${E.endReason}`, cv.width / 2, cv.height / 2); }
      if (def.turnBased && mode === "human" && !pending.length && !E.ended) { ctx.fillStyle = C.MUTED; ctx.font = '12px "Noto Sans KR"'; ctx.textAlign = "left"; ctx.fillText("키를 눌러 한 결정 진행", 10, 16); }
      $("#sEp").textContent = E.episode; $("#sStep").textContent = E.step; $("#sRet").textContent = E.ret.toFixed(2);
      const rec = E.history.slice(-20); $("#sAvg").textContent = rec.length ? (rec.reduce((a, b) => a + b, 0) / rec.length).toFixed(2) : "-";
      $("#sWhy").textContent = E.reason;
      if (def.curriculum) { $("#sLesson").textContent = E.lesson + 1; $("#sParam").textContent = S.params[def.curriculum.param]; }
      if (def.demo) { $("#sDemo").textContent = E.demo.length; }
      E.lastObs.forEach((v, i) => { const o = $("#ov" + i), b = $("#ob" + i); if (!o) return; o.textContent = fmt(v); const c = clamp(v, -1, 1); b.style.left = (c < 0 ? 50 + c * 50 : 50) + "%"; b.style.width = Math.abs(c) * 50 + "%"; });
      if (act.kind === "discrete") { $("#av0").textContent = act.labels[E.lastAct[0]] ?? "-"; act.labels.forEach((_, i) => { const b = $("#ab" + i); b.style.left = "0%"; b.style.width = (E.lastAct[0] === i ? 100 : 0) + "%"; }); }
      else E.lastAct.forEach((v, i) => { $("#av" + i).textContent = fmt(v); const b = $("#ab" + i); b.style.left = (v < 0 ? 50 + v * 50 : 50) + "%"; b.style.width = Math.abs(v) * 50 + "%"; });
      $("#log").innerHTML = E.log.slice(0, 12).map((l) => `<li><span class="mu">스텝 ${l.step}</span> · <span class="${l.kind}">${l.text}</span></li>`).join("");
      methods.forEach(([m]) => { const c = $("#c_" + m); if (c) c.textContent = E.counts[m] || 0; });
      drawHist();
    }
    function drawHist() {
      if (hctx.canvas.clientWidth && hctx.canvas.width !== hctx.canvas.clientWidth) hctx.canvas.width = hctx.canvas.clientWidth;
      const W = hctx.canvas.width, H = hctx.canvas.height, h = E.history.slice(-60);
      hctx.fillStyle = C.BG; hctx.fillRect(0, 0, W, H);
      hctx.fillStyle = C.MUTED; hctx.font = '11px "Noto Sans KR"'; hctx.textAlign = "left"; hctx.fillText("판마다 누적 보상 (최근 60판)", 6, 12);
      if (h.length < 2) return;
      const lo = Math.min(0, ...h), hi = Math.max(0.001, ...h);
      const y = (v) => H - 6 - ((v - lo) / (hi - lo)) * (H - 22);
      hctx.strokeStyle = C.CARD2; hctx.beginPath(); hctx.moveTo(0, y(0)); hctx.lineTo(W, y(0)); hctx.stroke();
      hctx.strokeStyle = C.ACC; hctx.lineWidth = 2; hctx.beginPath();
      h.forEach((v, i) => { const x = 6 + (i * (W - 12)) / (h.length - 1); i ? hctx.lineTo(x, y(v)) : hctx.moveTo(x, y(v)); }); hctx.stroke();
    }

    // ---------- 루프 ----------
    let last = performance.now(), acc = 0;
    function loop(now) {
      const el2 = Math.min(0.1, (now - last) / 1000); last = now;
      if (!paused) {
        if (E.ended) { E.endTimer -= el2 * Math.max(1, speed / 2); if (E.endTimer <= 0) beginEpisode(E.endReason); }
        else {
          const rate = def.turnBased && mode !== "human" ? 6 * DP : 1 / dt; // 턴 방식은 초당 6 결정
          if (!(def.turnBased && mode === "human")) { acc += el2 * speed * rate; let n = 0; while (acc >= 1 && n < 2000 && !E.ended) { physicsStep(); acc -= 1; n++; } }
          else { while (pending.length && !E.ended) { physicsStep(); while (E.step % DP !== 0 && !E.ended) physicsStep(); pending.shift(); } acc = 0; }
        }
      }
      render(); requestAnimationFrame(safeLoop);
    }
    const safeLoop = (t) => { try { loop(t); } catch (e) { showError(e); } };
    refreshButtons(); beginEpisode(); E.reason = "-"; requestAnimationFrame(safeLoop);
    cv.tabIndex = 0; cv.addEventListener("click", () => cv.focus());
  }

  // 게임 파일을 고치다 오류가 나면 화면 맨 위에 보여 준다
  function showError(e) {
    let d = document.getElementById("rl-error");
    if (!d) { d = el("div", { id: "rl-error", className: "card", style: "margin:12px 24px;border:2px solid #E06F63" }); document.body.prepend(d); }
    d.innerHTML = `<h3 style="color:#E06F63">스크립트 오류</h3><pre>${String(e && (e.stack || e.message || e)).replace(/</g, "&lt;")}</pre>`;
  }
  window.addEventListener("error", (ev) => showError(ev.error || ev.message));
  window.RLPlay = { start: (d) => { try { start(d); } catch (e) { showError(e); } }, C, rnd, clamp };
})();

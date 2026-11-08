// HTML 게임의 규칙만 꺼내 판을 돌려 본다.  node simgame.js <html> [판 수]
const fs = require("fs"), vm = require("vm");
const file = process.argv[2], N = +(process.argv[3] || 20);
const html = fs.readFileSync(file, "utf8");
const parts = html.split("<script>"); const code = parts[parts.length - 1].split("</script>")[0];
let def;
const RLPlay = { start: (d) => (def = d), rnd: (a, b) => a + Math.random() * (b - a), clamp: (v, a, b) => Math.max(a, Math.min(b, v)), C: new Proxy({}, { get: () => "#000" }) };
const ctx = vm.createContext({ RLPlay, Math, console, URLSearchParams, location: { search: process.argv[4] || "" } }); ctx.window = ctx;
for (const m of html.matchAll(/<script src="([^"]+)"><\/script>/g)) if (m[1] !== "rl-play.js") vm.runInContext(fs.readFileSync(require("path").join(require("path").dirname(file), m[1]), "utf8"), ctx);
vm.runInContext(code, ctx);
const nAct = def.act.kind === "discrete" ? 1 : def.act.names.length;
const g = new Proxy({}, { get: () => () => {} });
function run(policy) {
  const S = { params: {} }, out = [];
  const E = { lastAct: new Array(nAct).fill(0), step: 0 };
  let lesson = 0, hist = [];
  for (let ep = 0; ep < N; ep++) {
    if (def.curriculum) { const cu = def.curriculum; const r = hist.slice(-cu.window); if (r.length >= cu.window && r.reduce((a, b) => a + b, 0) / r.length > cu.threshold && lesson < cu.lessons.length - 1) { lesson++; hist = []; } S.params[cu.param] = cu.lessons[lesson]; }
    def.reset(S); let ret = 0, why = "max", step = 0;
    for (; step < def.maxStep; step++) {
      E.step = step;
      if (step % (def.decisionPeriod || 5) === 0) {
        const o = def.observe(S);
        if (o.length !== def.obs.length) throw new Error(`관측 수 ${o.length} != ${def.obs.length}`);
        if (o.some((v) => !Number.isFinite(v))) throw new Error("관측 NaN " + o);
        E.lastAct = policy === "rule" ? def.rule(S, o) : def.act.kind === "discrete" ? [Math.floor(Math.random() * def.act.labels.length)] : Array.from({ length: nAct }, () => Math.random() * 2 - 1);
      }
      const r = def.step(S, E.lastAct, def.dt || 0.02, E) || {};
      if (r.reward) ret += r.reward;
      if (r.done) { why = r.done; step++; break; }
    }
    hist.push(ret);
    def.draw(g, S, E);
    out.push({ ret, why, step });
  }
  const avg = out.reduce((a, b) => a + b.ret, 0) / N;
  const whys = {}; out.forEach((o) => (whys[o.why] = (whys[o.why] || 0) + 1));
  console.log(`${policy.padEnd(6)} 평균 보상 ${avg.toFixed(2)} · 평균 길이 ${(out.reduce((a, b) => a + b.step, 0) / N).toFixed(0)} ·`, JSON.stringify(whys), def.curriculum ? `· 단계 ${lesson + 1}` : "");
}
run("random"); run("rule");

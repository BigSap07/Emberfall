'use strict';
// Emberfall cockpit web view. Pure renderer: everything comes from GET /api/state (see SCHEMA.md).
const TONE = {ok:'#22c55e',warn:'#eab308',hot:'#f97316',bad:'#ef4444',mute:'#52525b',dim:'#a1a1aa',text:'#fafafa'};
const $ = (s) => document.querySelector(s);
const esc = (v) => String(v ?? '').replace(/[&<>"']/g, (c) => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const tone = (t) => TONE[t] || TONE.text;
const pctNum = (p) => Math.max(0, Math.min(100, Number(p) || 0));
let S = null, planSel = null;
try { planSel = localStorage.getItem('cockpit.plan'); } catch (e) {}

const card = (id, title, desc, right, body, cls = '') =>
  `<section class="card ${cls}" ${id ? `id="${id}"` : ''}><div class="ch"><div><div class="ct">${esc(title)}</div>${desc ? `<div class="cd">${esc(desc)}</div>` : ''}</div>${right || ''}</div>${body}</section>`;
const dot = (t, extra = '') => `<span class="dot" style="background:${tone(t)};${extra}"></span>`;
const badge = (txt, t) => `<span class="badge ${t === 'warn' ? 'warn' : ''}">${t && t !== 'warn' ? dot(t) : ''}${esc(txt)}</span>`;
const bar = (p, t) => `<div class="bar"><i style="width:${pctNum(p)}%;background:${tone(t)}"></i></div>`;

function kpis(d) {
  return `<div class="kpis">${d.kpis.map((k) => `<div class="card pad"><div class="lbl">${esc(k.label)}</div><div class="big n" style="color:${tone(k.tone)}">${esc(k.value)}</div><div class="lbl" style="margin-top:4px">${esc(k.sub)}</div></div>`).join('')}</div>`;
}

function chain(d) {
  const c = d.chain, t0 = new Date(c.axis_start).getTime(), t1 = new Date(c.axis_end).getTime(), span = t1 - t0;
  const at = (iso) => Math.max(0, Math.min(100, (new Date(iso).getTime() - t0) / span * 100));
  const hm = (ms) => new Date(ms).toLocaleTimeString('en-GB', {hour: '2-digit', minute: '2-digit'});
  const ticks = []; for (let t = t0; t <= t1; t += 2 * 3600e3) ticks.push(t);
  const layer = `<div class="g-layer">
    ${c.quiet ? `<div class="g-quiet" style="left:${at(c.quiet.start)}%;width:${at(c.quiet.end) - at(c.quiet.start)}%"></div>` : ''}
    ${ticks.map((t) => `<div class="g-grid" style="left:${(t - t0) / span * 100}%"></div>`).join('')}
    <div class="g-now" style="left:${at(d.now)}%"></div><div class="g-nowt" style="left:${at(d.now)}%">NOW</div></div>`;
  const axis = `<div class="g-axis"><div class="g-lab"></div><div class="g-track" style="height:22px">${ticks.map((t, i) => `<span class="g-tick n" style="left:${(t - t0) / span * 100}%;transform:translateX(${i === 0 ? '0' : i === ticks.length - 1 ? '-100%' : '-50%'})">${hm(t)}</span>`).join('')}</div></div>`;
  const rows = c.rows.map((r) => `<div class="g-row"><div class="g-lab"><b>${esc(r.name)}</b><span class="lbl n">${esc(r.meta)}</span></div><div class="g-track">${r.bars.map((b) => {
    const l = at(b.start); return `<div class="g-bar ${esc(b.kind)}" style="left:${l}%;width:${Math.max(0.6, at(b.end) - l)}%"></div>`; }).join('')}</div></div>`).join('');
  const note = c.note ? `<div class="lbl" style="padding:0 16px 14px;margin-top:-4px">${esc(c.note)}</div>` : '';
  return card('runs', 'Chain', c.desc, c.badge ? badge(c.badge.text, c.badge.tone) : '', `<div class="gantt" style="padding-top:12px">${layer}${axis}${rows}</div>${note}`);
}

function runCost(d) {
  const r = d.run_cost, cols = '2fr 1.4fr 70px 60px 60px 60px 60px';
  const head = `<div class="tr h" style="grid-template-columns:${cols}"><div>Run</div><div></div><div class="r">Cost</div><div class="r">Time</div><div class="r">$/h</div><div class="r">Opus</div><div class="r">Idle</div></div>`;
  const rows = r.rows.map((x) => `<div class="tr row" style="grid-template-columns:${cols}"><div style="display:flex;gap:8px;align-items:center;min-width:0">${dot(x.state_tone)}<span style="font-weight:500;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${esc(x.name)}</span></div>${bar(x.bar, x.hot ? 'hot' : 'dim')}<div class="r n" style="font-weight:500;color:${x.hot ? TONE.hot : '#e4e4e7'}">${esc(x.cost)}</div><div class="r n lbl">${esc(x.time)}</div><div class="r n lbl">${esc(x.rate)}</div><div class="r n" style="color:${x.opus_hot ? TONE.hot : TONE.dim}">${esc(x.opus)}</div><div class="r n" style="color:${x.idle_hot ? TONE.warn : TONE.dim}">${esc(x.idle)}</div></div>`).join('');
  return card('costs', 'Run cost', r.desc, badge(r.badge || ''), `<div class="body">${head}${rows}${r.hint ? `<div class="lbl" style="padding-top:8px">${esc(r.hint)}</div>` : ''}</div>`);
}

function runs(d) {
  const r = d.runs, cols = '2fr 70px 1fr 100px 60px 60px';
  const head = `<div class="tr h" style="grid-template-columns:${cols}"><div>Run</div><div>Found by</div><div>State</div><div class="r">Started</div><div class="r">Time</div><div class="r">Last</div></div>`;
  const rows = r.rows.map((x) => `<div class="tr row" style="grid-template-columns:${cols}" title="${esc(x.flag || x.note)}"><div style="display:flex;gap:8px;align-items:center;min-width:0">${dot(x.state_tone)}<span style="font-weight:500;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${esc(x.name)}</span>${x.flag ? `<span style="color:${TONE.warn}">!</span>` : ''}</div><div class="lbl">${esc(x.source_label)}</div><div style="color:${tone(x.state_tone)}">${esc(x.state)}</div><div class="r n lbl">${esc(x.started)}</div><div class="r n lbl">${esc(x.elapsed)}</div><div class="r n lbl">${esc(x.last)}</div></div>`).join('');
  return card('runs', 'Runs', r.desc, badge(r.badge || ''), `<div class="body">${head}${rows}</div>`);
}
function replicate(d) {
  const r = d.replicate, cols = '2fr 90px 60px 70px 1.4fr 90px';
  const head = `<div class="tr h" style="grid-template-columns:${cols}"><div>Ledger</div><div>State</div><div class="r">Images</div><div class="r">Cost</div><div>Cap</div><div class="r">Rate</div></div>`;
  const rows = r.rows.map((x) => `<div class="tr row" style="grid-template-columns:${cols}"><div style="display:flex;gap:8px;align-items:center;min-width:0">${dot(x.tone)}<span style="font-weight:500;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${esc(x.name)}</span></div><div style="color:${tone(x.tone)}">${esc(x.state)}</div><div class="r n">${esc(x.images)}</div><div class="r n" style="font-weight:500">${esc(x.cost)}</div><div class="lbl">${esc(x.cap)}</div><div class="r n lbl">${esc(x.rate)}</div></div>`).join('');
  const t = r.total;
  return card('replicate', 'Replicate spend', r.desc, badge(r.badge || '', r.live ? 'warn' : ''), `<div class="body">${head}${rows}<div class="tr row" style="grid-template-columns:1fr auto"><div class="lbl">${esc(t.label)}<div>${esc(t.sub)}</div></div><div class="r n" style="font-weight:500">${esc(t.value)}</div></div></div>`);
}
function waiting(d) {
  const w = d.waiting;
  return card('', 'Needs you', w.desc, badge(w.open + ' open', w.open ? 'warn' : 'ok'),
    `<div class="body">${w.items.map((i) => `<div class="row" style="display:flex;gap:12px;padding:10px 0">${dot(i.tone, 'margin-top:5px')}<div><div style="font-weight:500">${esc(i.title)}</div><div class="lbl" style="margin-top:2px;line-height:1.4">${esc(i.sub)}</div></div></div>`).join('') || '<div class="lbl">Nothing waiting on you.</div>'}</div>`);
}

function shipped(d) {
  const s = d.shipped;
  return card('', 'Shipped today', '', badge(s.date),
    `<div class="mini">${s.counters.map((c) => `<div><div class="v n">${esc(c.n)}</div><div class="lbl">${esc(c.label)}</div></div>`).join('')}</div><div class="body">${s.feed.map((f) => `<div class="row" style="display:flex;gap:12px;padding:9px 0"><div class="n lbl" style="width:42px;min-width:42px">${esc(f.time)}</div><div style="line-height:1.4">${esc(f.text)}</div></div>`).join('')}</div>`);
}

function road(d) {
  const r = d.road;
  const ms = r.milestones.map((m) => `<div style="position:absolute;left:${m.pct}%;top:-3px;width:2px;height:16px;background:${tone(m.tone)}"></div><div style="position:absolute;left:${m.pct}%;top:18px;transform:translateX(${m.pct > 95 ? '-100%' : '-50%'});font-size:11px;font-weight:500;color:${tone(m.tone)};white-space:nowrap">${esc(m.label)}</div>`).join('');
  const tr = r.tracks.map((t) => `<div class="row" style="display:grid;grid-template-columns:140px 1fr 44px;gap:12px;align-items:center;padding:9px 0"><div style="font-weight:500">${esc(t.name)}</div><div>${bar(t.pct, t.tone)}<div class="lbl" style="margin-top:4px">${esc(t.sub)}</div></div><div class="r n" style="font-weight:500;color:${tone(t.tone)}">${pctNum(t.pct)}%</div></div>`).join('');
  return card('', 'Road to a real game', r.desc, `<div class="n" style="font-size:38px;font-weight:600;letter-spacing:-.03em;color:${TONE.hot};line-height:1">${pctNum(r.pct)}%</div>`,
    `<div style="padding:16px 16px 6px"><div style="position:relative;height:34px"><div class="bar" style="height:10px;border-radius:5px"><i style="width:${pctNum(r.pct)}%;background:linear-gradient(90deg,#c2410c,#f97316)"></i></div>${ms}</div></div><div style="padding:4px 16px 16px">${tr}</div>`, '');
}

function plan(d) {
  const p = d.plan, sel = (planSel && p.options.find((o) => o.id === planSel)) ? planSel : p.default, o = p.options.find((x) => x.id === sel);
  const seg = `<div class="seg" role="group" aria-label="Plan">${p.options.map((x) => `<button type="button" data-plan="${esc(x.id)}" class="${x.id === sel ? 'on' : ''}" aria-pressed="${x.id === sel}">${esc(x.name)}  ${esc(x.price_label)}</button>`).join('')}</div>`;
  const bars = p.bars.map((b) => { const hl = b.plan_id === sel; return `<div style="display:grid;grid-template-columns:80px 1fr 84px;gap:10px;align-items:center;padding:4px 0"><div class="lbl">${esc(b.label)}</div>${bar(b.pct, b.plan_id ? (hl ? 'hot' : 'mute') : 'mute')}<div class="n r">${esc(b.val)}</div></div>`; }).join('');
  const L = d.limits;
  const blk = `<div style="display:flex;justify-content:space-between"><span style="font-weight:500">5h block</span><span class="n lbl">${esc(L.block.sub)} <button type="button" class="lnk" data-set="block_reset" data-q="Time left until reset, copied from claude.ai Settings, Usage (examples: 3h12, 3:12, 01:12)">set</button></span></div>${bar(L.block.pct, 'hot').replace('class="bar"', 'class="bar" style="margin-top:6px"')}<div style="display:flex;justify-content:space-between;margin-top:5px" class="lbl n"><span>${esc(L.block.left)}</span><span>${esc(L.block.right)}</span></div>`;
  const wk = `<div style="display:flex;justify-content:space-between;margin-top:14px"><span style="font-weight:500">Weekly limit</span><span class="n lbl">${esc(L.weekly.sub)} <button type="button" class="lnk" data-set="weekly" data-q="Weekly percent used and reset, for example: 62% 2d13h">set</button></span></div><div style="position:relative;margin-top:6px">${bar(L.weekly.pct, L.weekly.tone)}${L.weekly.pace != null ? `<div style="position:absolute;left:${pctNum(L.weekly.pace)}%;top:-3px;width:2px;height:12px;background:#fafafa"></div>` : ''}</div><div class="n" style="margin-top:5px;font-size:12px;color:${tone(L.weekly.tone)}">${esc(L.weekly.note)}</div>`;
  const mix = `<div style="margin-top:14px;font-weight:500">Today by model</div><div style="display:flex;height:8px;border-radius:4px;overflow:hidden;margin-top:6px">${L.models.map((m) => `<div style="width:${pctNum(m.pct)}%;background:${tone(m.tone)}"></div>`).join('')}</div><div class="lbl" style="display:flex;gap:16px;margin-top:5px">${L.models.map((m) => `<span>${esc(m.name)} ${esc(m.pct)}%</span>`).join('')}</div>`;
  return card('usage', 'Plan and limits', 'API price equivalent, not a bill.', seg,
    `<div class="stat3"><div><div class="lbl">Paid since ${esc(p.since)}</div><div class="v n">${esc(o.paid)}</div></div><div><div class="lbl">API value used</div><div class="v n">${esc(p.api_value)}</div></div><div><div class="lbl">Value multiple</div><div class="v n" style="color:${TONE.ok}">${esc(o.multiple)}</div></div></div>
     <div style="padding:12px 16px 4px">${bars}</div><div class="lbl" style="padding:8px 16px 0;line-height:1.5">${esc(p.note)}</div>
     <div style="padding:14px 16px 16px;margin-top:12px;border-top:1px solid var(--bd)">${blk}${wk}${mix}</div>`);
}

function side(d) {
  const w = d.week.days, top = Math.max(1, ...w.map((x) => x.value));
  const week = card('', 'Last 7 days', 'Spend per day', '', `<div class="week">${w.map((x) => `<div class="${x.today ? 'today' : ''}"><span class="n">${esc(x.label)}</span><i style="height:${Math.max(2, x.value / top * 66)}px"></i><span>${esc(x.day)}</span></div>`).join('')}</div>`);
  const pt = d.playtest, done = pt.items.filter((i) => i.done).length;
  const play = card('playtest', 'First playtest', pt.desc, badge(`${done} of ${pt.items.length}`),
    `<div class="body">${pt.items.map((i) => `<div class="row" style="display:flex;align-items:center;gap:10px;padding:8px 0"><span class="chk ${i.done ? 'on' : ''}"></span><span style="line-height:1.35;color:${i.done ? TONE.dim : TONE.text}">${esc(i.title)}</span></div>`).join('')}<div style="padding-top:8px"><button type="button" class="lnk" data-set="playtest_date" data-q="Playtest target date, YYYY-MM-DD (leave empty to clear)">set target date</button></div></div>`);
  const hl = card('builds', 'Health', '', '', `<div class="body">${d.health.map((h) => `<div class="row" style="display:flex;align-items:center;gap:10px;padding:8px 0">${dot(h.tone)}<div class="lbl" style="width:96px;min-width:96px">${esc(h.label)}</div><div>${esc(h.value)}</div></div>`).join('')}</div>`);
  const tips = d.tips.length ? `<section class="card tip"><b>Worth knowing</b>${d.tips.map((t) => `<p>${esc(t)}</p>`).join('')}</section>` : '';
  return week + play + hl + tips;
}

function render() {
  const d = S;
  $('#hdr').innerHTML = `${badge(d.header.unity.text, d.header.unity.tone)}${d.header.waiting ? badge(d.header.waiting + ' need you', 'warn') : ''}<span class="badge n" style="font-family:'Geist Mono',monospace;color:#a1a1aa">main ${esc(d.header.main)}</span><div class="n" style="text-align:right;line-height:1.2"><div style="font-size:14px;font-weight:500">${esc(d.header.clock)}</div><div class="lbl">${esc(d.header.date)}</div></div>${d.header.mock ? '<span class="mock">MOCK DATA</span>' : ''}`;
  const hints = (d.hints || []).map((h) => `<div class="card hint">${esc(h)}</div>`).join('');
  $('#app').innerHTML = `${hints}${kpis(d)}<div class="grid"><div class="col s8">${chain(d)}${runCost(d)}${replicate(d)}${runs(d)}</div><div class="col s4">${waiting(d)}${shipped(d)}</div></div><div class="grid"><div class="col s5">${road(d)}</div><div class="col s4">${plan(d)}</div><div class="col s3">${side(d)}</div></div>`;
}

async function load() {
  try { const r = await fetch('/api/state', {cache: 'no-store'}); if (!r.ok) throw new Error(r.status); S = await r.json(); render(); }
  catch (e) { if (!S) $('#app').innerHTML = `<div class="card hint">Cannot reach the cockpit server (${esc(e.message)}). Start it with cockpit-web.bat.</div>`; }
}
document.addEventListener('click', (e) => {
  const pb = e.target.closest('[data-plan]');
  if (pb) { planSel = pb.dataset.plan; try { localStorage.setItem('cockpit.plan', planSel); } catch (x) {} render(); return; }
  const sb = e.target.closest('[data-set]');
  if (sb) {
    const dlg = $('#dlg'); $('#dlgl').textContent = sb.dataset.q; $('#dlgi').value = ''; dlg.returnValue = ''; dlg.showModal();
    dlg.onclose = async () => {
      if (dlg.returnValue !== 'ok') return;
      let err = '';
      try {
        const r = await fetch('/api/set', {method: 'POST', headers: {'Content-Type': 'application/json', 'X-Cockpit': '1'}, body: JSON.stringify({key: sb.dataset.set, value: $('#dlgi').value})});
        if (!r.ok) err = ((await r.json().catch(() => ({}))).error) || ('HTTP ' + r.status);
      } catch (x) { err = 'server not reachable'; }
      await load();
      if (err && S) { S.hints = [...(S.hints || []), 'Not saved: ' + err]; render(); }
    };
  }
});
$('#dlgi').addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); $('#dlg').close('ok'); } });
load(); setInterval(load, 10000);

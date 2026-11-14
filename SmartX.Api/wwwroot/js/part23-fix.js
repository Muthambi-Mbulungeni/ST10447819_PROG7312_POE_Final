/* Smart-X dashboard fix. Loaded AFTER app.js.
 * Redefines the Part 1/2/3 functions so they call the endpoints that really exist
 * in Program.cs and read the real response fields. */

const SX_CMD_TYPES = ['TriggerActuator', 'ShutdownDevice', 'RestartDevice', 'AdjustThreshold', 'ManualOverride'];
const SX_CATS = ['Environmental', 'PowerConsumption', 'Actuator'];
const SX_ACTIONS = {
  ToggleRelay: 'TriggerActuator',
  TriggerPump: 'TriggerActuator',
  SetValveState: 'AdjustThreshold',
  EmergencyShutdown: 'ShutdownDevice',
  ResetNode: 'RestartDevice'
};
const SX_GATEWAY = 'GW:00:11:22:33:44';
let sxSensors = [];
let sxLinkCut = false;
let sxTopoInit = false;

// ---------- helpers ----------
async function sxGet(path) {
  try {
    const r = await fetch(API_BASE + path);
    if (!r.ok) return null;
    return await r.json();
  } catch (e) { console.error('GET', path, e); return null; }
}

async function sxPost(path, body) {
  try {
    const r = await fetch(API_BASE + path, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: body === undefined ? '{}' : JSON.stringify(body)
    });
    const text = await r.text();
    let data = null;
    try { data = JSON.parse(text); } catch { }
    return { ok: r.ok, status: r.status, data };
  } catch (e) { console.error('POST', path, e); return { ok: false, status: 0, data: null }; }
}

const sxEsc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const sxType = t => (typeof t === 'number' ? (SX_CMD_TYPES[t] ?? t) : t);
const sxCat = c => (typeof c === 'number' ? (SX_CATS[c] ?? c) : c);
const sxIso = d => d.toISOString().replace('Z', '');   // no "Z": server compares raw UTC ticks
const sxClock = ts => new Date(ts).toISOString().replace('T', ' ').slice(0, 19);

async function sxPickMac(cat) {
  if (!sxSensors.length) sxSensors = (await sxGet('/sensors')) || [];
  const s = sxSensors.find(x => sxCat(x.category) === cat);
  return s ? s.macAddress : null;
}

// ---------- OVERVIEW / REGISTER ----------
async function loadSensorsData() {
  sxSensors = (await sxGet('/sensors')) || [];
  const el = document.getElementById('card-registered-nodes');
  if (el) el.textContent = sxSensors.length;
  renderOverviewSensorsTable(sxSensors.slice(0, 12));
}

function renderOverviewSensorsTable(list) {
  const tbody = document.getElementById('overview-sensors-body');
  if (!tbody) return;
  const meta = {
    Environmental: ['badge-cat-env', 'ENVIRONMENTAL'],
    PowerConsumption: ['badge-cat-power', 'POWER'],
    Actuator: ['badge-cat-actuator', 'ACTUATOR']
  };
  tbody.innerHTML = (list || []).map(s => {
    const c = sxCat(s.category);
    const m = meta[c] || ['badge-cat-env', String(c).toUpperCase()];
    return `<tr>
      <td style="font-family:var(--font-mono);font-weight:700;color:#fff;">${sxEsc(s.macAddress)}</td>
      <td style="color:var(--text-secondary);">${sxEsc(s.zone)} · ${sxEsc(s.room)}</td>
      <td><span class="${m[0]}">${m[1]}</span></td>
      <td style="font-family:var(--font-mono);color:var(--text-muted);">TelemetryPacket&lt;float&gt;</td>
      <td style="color:var(--text-muted);font-size:0.78rem;">live</td>
    </tr>`;
  }).join('');
}

async function submitRegisterSensor() {
  const mac = document.getElementById('input-sensor-mac').value.trim().toUpperCase();
  const zone = document.getElementById('input-zone-id').value.trim();
  const facility = document.getElementById('select-facility').value;
  const r = await sxPost('/sensors/register', {
    macAddress: mac,
    zone: zone,
    room: facility,
    nodeId: 'NODE-' + mac.replace(/[^A-Z0-9]/g, '').slice(-6),
    category: Math.max(0, SX_CATS.indexOf(appState.selectedCategory))
  });
  if (r.ok) alert(`Sensor ${mac} registered.`);
  else if (r.status === 409) alert(`Sensor ${mac} is already registered.`);
  else alert(`Registration failed (HTTP ${r.status}).`);
  await loadSensorsData();
  switchNavTab('overview');
}

// ---------- PART 2: COMMAND STREAM ----------
async function loadCommandStreamData() {
  await Promise.all([
    fetchCommandStreamMetrics(),
    fetchCommandStreamDevices(document.getElementById('cs-search-input')?.value || ''),
    fetchRecommendations(),
    fetchProcessedPriorityAlerts(),
    fetchChronologicalTimeline(),
    fetchCommandHistory()
  ]);
}

async function fetchCommandStreamMetrics() {
  const [s, alerts, disc, hist] = await Promise.all([
    sxGet('/commands/stream'), sxGet('/alerts/active'), sxGet('/alerts/disconnected'), sxGet('/commands/history')
  ]);
  if (!s) return;
  const set = (id, v) => { const el = document.getElementById(id); if (el) el.textContent = v; };
  set('cs-stat-fifo', s.standardCount ?? 0);
  set('cs-stat-priority', s.criticalCount ?? 0);
  set('cs-stat-stack', hist ? hist.length : 0);
  set('cs-stat-errors', (alerts ? alerts.length : 0) + (disc ? disc.length : 0));
  set('cs-priority-status-text', `${s.criticalCount ?? 0} critical items waiting`);
}

async function fetchCommandStreamDevices(search = '') {
  const container = document.getElementById('cs-devices-container');
  if (!container) return;
  const cat = csCurrentCategory !== 'All' ? csCurrentCategory : '';
  let list = (await sxGet('/devices' + (cat ? `?category=${encodeURIComponent(cat)}` : ''))) || [];
  const disc = ((await sxGet('/alerts/disconnected')) || []).map(x => x.toLowerCase());
  if (search) {
    const t = search.toLowerCase();
    list = list.filter(d => d.mac.toLowerCase().includes(t) || d.nodeId.toLowerCase().includes(t) || d.category.toLowerCase().includes(t));
  }
  if (!list.length) {
    container.innerHTML = '<div style="font-size:0.78rem;color:var(--text-muted);font-style:italic;">No registered devices match current filter.</div>';
    return;
  }
  container.innerHTML = list.slice(0, 30).map(d => {
    const cut = disc.includes(d.mac.toLowerCase());
    const bad = cut || d.isAnomaly;
    const label = cut ? 'DISCONNECTED' : (d.isAnomaly ? 'ANOMALY' : 'NOMINAL');
    return `<div style="background:var(--bg-surface-elevated);border:1px solid var(--border-subtle);border-radius:var(--radius-sm);padding:0.6rem 0.8rem;display:flex;justify-content:space-between;align-items:center;">
      <div>
        <div style="font-family:var(--font-mono);font-weight:700;font-size:0.8rem;color:var(--amber-accent);">${sxEsc(d.mac)}
          <span style="font-size:0.68rem;margin-left:0.5rem;padding:0.15rem 0.4rem;border-radius:4px;background:${bad ? 'rgba(239,68,68,0.15)' : 'rgba(16,185,129,0.15)'};color:${bad ? '#ef4444' : 'var(--teal-accent)'};">${label}</span>
        </div>
        <div style="font-size:0.72rem;color:var(--text-secondary);margin-top:0.2rem;">${sxEsc(d.nodeId)}</div>
        <div style="font-size:0.68rem;color:var(--text-muted);margin-top:0.15rem;">Category: <strong>${sxEsc(d.category)}</strong> | ${Number(d.lastValue).toFixed(1)} ${sxEsc(d.unit)}</div>
      </div>
      <div style="display:flex;flex-direction:column;gap:0.3rem;">
        <button class="btn-ghost-sm" onclick="selectDeviceForOverride('${sxEsc(d.mac)}','${sxEsc(d.category)}')">Select MAC</button>
        <button class="btn-ghost-sm" style="font-size:0.68rem;color:#ef4444;" onclick="toggleDeviceDisconnect('${sxEsc(d.mac)}', ${!cut})">${cut ? 'Reconnect' : 'Simulate Cut'}</button>
      </div>
    </div>`;
  }).join('');
}

async function toggleDeviceDisconnect(mac, isCurrentlyActive) {
  const path = (isCurrentlyActive ? '/alerts/disconnect/' : '/alerts/reconnect/') + encodeURIComponent(mac);
  await sxPost(path);
  await fetchCommandStreamMetrics();
  await fetchCommandStreamDevices(document.getElementById('cs-search-input')?.value || '');
}

async function dispatchManualOverride() {
  const mac = document.getElementById('override-target-mac')?.value.trim();
  const action = document.getElementById('override-action-type')?.value;
  const desc = document.getElementById('override-description')?.value.trim();
  if (!mac) { alert('Target MAC address is required.'); return; }
  const r = await sxPost('/commands/send', {
    deviceId: mac,
    commandType: SX_ACTIONS[action] || 'ManualOverride',
    payload: `${action}: ${desc || 'no description'}`
  });
  if (r.ok) {
    await Promise.all([fetchCommandStreamMetrics(), fetchCommandHistory(), fetchRecommendations()]);
    alert(`Override pushed onto the command Stack for ${mac} (id ${r.data?.id}).`);
  } else alert(`Dispatch failed (HTTP ${r.status}).`);
}

async function undoLastOverride() {
  const r = await sxPost('/commands/undo');
  if (r.ok) {
    alert(r.data?.message || 'Done.');
    await Promise.all([fetchCommandStreamMetrics(), fetchCommandHistory()]);
  } else alert('Undo failed.');
}

async function fetchCommandHistory() {
  const tbody = document.getElementById('cs-command-history-tbody');
  if (!tbody) return;
  const hist = (await sxGet('/commands/history')) || [];
  if (!hist.length) {
    tbody.innerHTML = '<tr><td colspan="7" style="text-align:center;color:var(--text-muted);font-style:italic;">No commands issued yet.</td></tr>';
    return;
  }
  tbody.innerHTML = hist.map(c => `<tr>
    <td style="font-family:var(--font-mono);color:var(--text-muted);font-size:0.72rem;">${sxClock(c.issuedAt)}</td>
    <td style="font-family:var(--font-mono);color:var(--amber-accent);">${sxEsc(c.deviceId)}</td>
    <td><strong>${sxEsc(sxType(c.type))}</strong></td>
    <td><span style="font-family:var(--font-mono);color:var(--cyan-accent);">${sxEsc(c.id)}</span></td>
    <td style="color:var(--text-secondary);">${sxEsc(c.issuedBy)}</td>
    <td><span style="padding:0.15rem 0.4rem;border-radius:4px;font-size:0.68rem;font-weight:700;background:rgba(16,185,129,0.2);color:var(--teal-accent);">ACTIVE</span></td>
    <td style="color:var(--text-secondary);max-width:240px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">${sxEsc(c.payload)}</td>
  </tr>`).join('');
}

async function simulateCriticalMoistureDrop() {
  const mac = (await sxPickMac('Environmental')) || 'AA:BB:CC:DD:00:00';
  await sxPost('/commands/simulate-alert', { deviceId: mac, value: 12.4, unit: '%', category: 'Environmental' });
  await Promise.all([fetchCommandStreamMetrics(), fetchProcessedPriorityAlerts(), fetchRecommendations()]);
  alert('Critical reading sent. It went straight into the PriorityQueue.');
}

async function simulateSeverePowerSpike() {
  const mac = (await sxPickMac('PowerConsumption')) || 'AA:BB:CC:DD:00:01';
  await sxPost('/commands/simulate-alert', { deviceId: mac, value: 12450, unit: 'W', category: 'PowerConsumption' });
  await Promise.all([fetchCommandStreamMetrics(), fetchProcessedPriorityAlerts(), fetchRecommendations()]);
  alert('Power spike sent. It went straight into the PriorityQueue.');
}

async function processNextPriorityAlert() {
  const s = await sxGet('/commands/stream');
  const top = s && s.critical && s.critical[0];
  if (!top) { alert('Priority queue is currently clear.'); return; }
  alert(`Next out of the priority lane: ${top.nodeId} (${top.deviceId}) = ${Number(top.value).toFixed(1)} ${top.unit}, priority ${top.priority}`);
}

async function fetchProcessedPriorityAlerts() {
  const feed = document.getElementById('cs-priority-feed');
  if (!feed) return;
  const s = await sxGet('/commands/stream');
  const list = (s && s.critical) || [];
  if (!list.length) {
    feed.innerHTML = '<div style="font-size:0.78rem;color:var(--text-muted);font-style:italic;">No critical priority alerts in the queue.</div>';
    return;
  }
  feed.innerHTML = list.slice(0, 6).map(a => `
    <div style="background:rgba(239,68,68,0.12);border-left:3px solid #ef4444;padding:0.4rem 0.6rem;border-radius:3px;">
      <div style="display:flex;justify-content:space-between;font-size:0.72rem;">
        <strong style="color:#ef4444;">${sxEsc(a.category)} (priority ${a.priority})</strong>
        <span style="font-family:var(--font-mono);color:var(--text-muted);">${sxClock(a.receivedAt).slice(11)}</span>
      </div>
      <div style="font-size:0.7rem;color:var(--text-secondary);margin-top:0.15rem;">
        ${sxEsc(a.nodeId)} · <code>${sxEsc(a.deviceId)}</code> · ${Number(a.value).toFixed(1)} ${sxEsc(a.unit)}
      </div>
    </div>`).join('');
}

async function fetchChronologicalTimeline() {
  const container = document.getElementById('cs-timeline-container');
  const t = await sxGet('/devices/timeline?n=300');
  updateLiveGaugesAndChart();
  if (!container) return;
  if (!t || !t.length) {
    container.innerHTML = '<div style="color:var(--text-muted);font-style:italic;">No chronological logs yet.</div>';
    return;
  }
  container.innerHTML = t.slice(-15).map(e =>
    `<div><span style="color:var(--teal-accent);">[${sxClock(e.timestamp)}]</span> <strong style="color:var(--text-primary);">${sxEsc(e.nodeId)}</strong>: <span style="color:var(--amber-accent);font-weight:700;">${Number(e.value).toFixed(1)} ${sxEsc(e.unit)}</span></div>`
  ).join('');
}

async function updateLiveGaugesAndChart() {
  initLiveTelemetryChart();
  const powerMac = await sxPickMac('PowerConsumption');
  if (powerMac && liveTelemetryChartInstance) {
    const h = await sxGet('/telemetry/' + encodeURIComponent(powerMac));
    if (h && h.length) {
      const last = h.slice(-30);
      liveTelemetryChartInstance.data.labels = last.map(p => {
        const d = new Date(p.timestamp);
        return d.getMinutes() + ':' + String(d.getSeconds()).padStart(2, '0');
      });
      liveTelemetryChartInstance.data.datasets[0].data = last.map(p => p.value);
      liveTelemetryChartInstance.update();
    }
  }
  const devs = (await sxGet('/devices')) || [];
  const first = c => devs.find(d => d.category === c);
  const setG = (valId, barId, text, pct) => {
    const v = document.getElementById(valId), b = document.getElementById(barId);
    if (v) v.innerText = text;
    if (b) b.style.width = Math.max(0, Math.min(100, pct)) + '%';
  };
  const p = first('PowerConsumption'), e = first('Environmental'), a = first('Actuator');
  if (p) setG('gauge-power-val', 'gauge-power-bar', `${p.lastValue.toFixed(1)} W`, (p.lastValue / 500) * 100);
  if (e) setG('gauge-temp-val', 'gauge-temp-bar', `${e.lastValue.toFixed(1)} hPa`, ((e.lastValue - 950) / 130) * 100);
  if (a) setG('gauge-moisture-val', 'gauge-moisture-bar', `${a.lastValue.toFixed(2)} state`, a.lastValue * 50);
}

async function simulateLiveTelemetryPulse() {
  const mac = await sxPickMac('PowerConsumption');
  if (!mac) return;
  await sxPost('/telemetry/ingest', { deviceId: mac, value: Number((200 + Math.random() * 40).toFixed(1)), unit: 'W' });
  await Promise.all([fetchChronologicalTimeline(), fetchCommandStreamMetrics()]);
}

async function fetchRecommendations() {
  const list = document.getElementById('predictive-insights-list');
  if (!list) return;
  const recs = await sxGet('/predictions');
  if (!recs) return;
  if (!recs.length) {
    list.innerHTML = '<div style="font-size:0.78rem;color:var(--teal-accent);font-style:italic;">System operating nominally. No proactive insights.</div>';
    return;
  }
  list.innerHTML = recs.slice(0, 8).map(r => `
    <div class="sx-rec" style="background:var(--bg-surface-elevated);border:1px solid rgba(245,166,35,0.3);border-radius:var(--radius-sm);padding:0.6rem 0.8rem;display:flex;justify-content:space-between;align-items:center;gap:0.8rem;">
      <div>
        <div style="display:flex;align-items:center;gap:0.5rem;">
          <span style="font-size:0.68rem;font-weight:700;padding:0.15rem 0.4rem;border-radius:4px;${r.priority === 0 ? 'background:#ef4444;color:#fff;' : 'background:rgba(245,166,35,0.2);color:var(--amber-accent);'}">${sxEsc(r.insightType.toUpperCase())}</span>
          <strong style="font-size:0.82rem;color:var(--text-primary);">${sxEsc(r.title)}</strong>
        </div>
        <div style="font-size:0.72rem;color:var(--text-secondary);margin-top:0.25rem;">${sxEsc(r.description)}</div>
      </div>
      <div style="display:flex;gap:0.4rem;">
        <button class="btn-gold" style="font-size:0.72rem;padding:0.35rem 0.75rem;" onclick="sxTarget('${sxEsc(r.relatedDevice)}')">Target</button>
        <button class="btn-ghost-sm" style="font-size:0.72rem;" onclick="this.closest('.sx-rec').remove()">Dismiss</button>
      </div>
    </div>`).join('');
}

function sxTarget(rel) {
  const input = document.getElementById('override-target-mac');
  if (input && /^[0-9A-F]{2}(:[0-9A-F]{2}){5}$/i.test(rel)) input.value = rel;
}

// ---------- PART 3: TOPOLOGY ----------
async function initTopologyModule() {
  topologyState.canvas = document.getElementById('meshTopologyCanvas');
  if (topologyState.canvas) {
    topologyState.ctx = topologyState.canvas.getContext('2d');
    if (!sxTopoInit) { setupCanvasInteractions(); sxTopoInit = true; }
  }
  const now = new Date();
  const local = d => new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
  const fromEl = document.getElementById('avl-query-from');
  const toEl = document.getElementById('avl-query-to');
  if (fromEl && !fromEl.value) fromEl.value = local(new Date(now.getTime() - 2 * 60000));
  if (toEl && !toEl.value) toEl.value = local(new Date(now.getTime() + 60000));
  if (!sxSensors.length) sxSensors = (await sxGet('/sensors')) || [];
  await loadMeshTopologyData();
  await computeAndHighlightMst();
  await refreshAvlIndexedCount();
  startMeshCanvasLoop();
}

async function loadMeshTopologyData() {
  const d = await sxGet('/topology/mesh');
  if (!d) return;
  topologyState.nodes = (d.nodes || []).map(n => ({ ...n, y: n.y * 0.86 }));   // keep bottom row inside the canvas
  topologyState.edges = d.edges || [];
  const v = document.getElementById('topo-stat-vertices');
  const e = document.getElementById('topo-stat-edges');
  if (v) v.textContent = d.nodeCount;
  if (e) e.textContent = d.edgeCount;
  populateTopologyNodeSelectors();
}

function populateTopologyNodeSelectors() {
  const traceSel = document.getElementById('select-trace-node');
  const avlSel = document.getElementById('avl-query-mac');
  if (traceSel) {
    const prev = traceSel.value;
    traceSel.innerHTML = '<option value="">-- Select Node to Trace Route --</option>' +
      topologyState.nodes.filter(n => n.role !== 'Gateway').map(n =>
        `<option value="${sxEsc(n.macAddress)}">${sxEsc(n.deviceName)} (${sxEsc(n.macAddress)}) [${n.isOnline ? 'ONLINE' : 'OFFLINE'}]</option>`).join('');
    if (prev) traceSel.value = prev;
  }
  if (avlSel) {
    const prev = avlSel.value;
    avlSel.innerHTML = '<option value="">All Sensors (Global AVL Tree)</option>' +
      sxSensors.slice(0, 50).map(s => `<option value="${sxEsc(s.macAddress)}">${sxEsc(s.nodeId)} (${sxEsc(s.macAddress)})</option>`).join('');
    if (prev) avlSel.value = prev;
  }
}

async function computeAndHighlightMst() {
  const d = await sxGet('/topology/mst');
  if (!d) return;
  topologyState.mstEdges = d.edges || [];
  const cost = document.getElementById('topo-stat-mst-cost');
  if (cost) cost.textContent = `${Number(d.totalWeight).toFixed(1)} ms`;
  const feed = document.getElementById('traversal-results-feed');
  if (feed) {
    feed.innerHTML = `
      <div style="color:var(--teal-accent);font-weight:700;">Minimum Spanning Tree (Prim's algorithm, Min-Heap)</div>
      <div>Total cost: <strong>${Number(d.totalWeight).toFixed(1)} ms</strong> across ${topologyState.mstEdges.length} links covering ${d.nodeCount} online nodes.</div>
      ${d.isConnected ? '<div style="color:#10b981;">Spans every online node.</div>' : '<div style="color:#ef4444;">Network is partitioned: some online nodes cannot be reached.</div>'}`;
  }
}

async function runGraphTraversal(method) {
  const d = await sxGet(`/topology/${method.toLowerCase()}?startMac=${encodeURIComponent(SX_GATEWAY)}&onlineOnly=true`);
  const feed = document.getElementById('traversal-results-feed');
  if (!d || !feed) return;
  const visited = new Set(d.visitedOrder);
  const missing = topologyState.nodes.filter(n => !visited.has(n.macAddress));
  feed.innerHTML = `
    <div style="color:#38bdf8;font-weight:700;">${method} from the Gateway</div>
    <div>Reached <strong>${d.totalVisited}</strong> of ${topologyState.nodes.length} nodes:</div>
    <div style="color:var(--teal-accent);word-break:break-all;font-size:0.68rem;">${d.visitedOrder.map(sxEsc).join(' → ')}</div>
    ${missing.length ? `<div style="color:#ef4444;margin-top:0.3rem;">Unreachable: ${missing.map(n => sxEsc(n.deviceName)).join(', ')}</div>`
      : '<div style="color:#10b981;">Every node is reachable.</div>'}`;
}

async function traceRouteForNode(sourceMac) {
  const r = await sxPost('/topology/route', { sourceMac, destinationMac: SX_GATEWAY });
  const d = r.data;
  const el = document.getElementById('route-trace-details');
  if (!d || !el) return;
  topologyState.activeRouteHops = d.hops || [];
  if (!d.isReachable) {
    el.innerHTML = `<div style="color:#ef4444;font-weight:700;">Route unreachable</div>
      <div style="color:var(--text-secondary);margin-top:0.3rem;">${sxEsc(d.failureReason)}</div>`;
    return;
  }
  const rows = d.hops.map((mac, i) => {
    const n = topologyState.nodes.find(x => x.macAddress === mac) || {};
    return `<div style="display:flex;justify-content:space-between;padding:0.25rem 0;border-bottom:1px solid rgba(255,255,255,0.04);">
      <div><span style="color:var(--teal-accent);font-weight:700;">Hop ${i}:</span> <strong>${sxEsc(n.deviceName || mac)}</strong>
      <span style="font-size:0.68rem;color:var(--text-muted);">(${sxEsc(mac)})</span></div>
      <span style="font-size:0.68rem;color:${n.isOnline === false ? '#ef4444' : '#10b981'};">${n.isOnline === false ? 'Offline' : 'Online'}</span></div>`;
  }).join('');
  el.innerHTML = `<div style="display:flex;justify-content:space-between;margin-bottom:0.5rem;">
      <span style="color:#10b981;font-weight:700;">Active path found</span>
      <span style="color:var(--amber-accent);font-weight:700;">${d.hopCount} hops · ${Number(d.totalCost).toFixed(1)} ms</span></div>${rows}`;
  const node = topologyState.nodes.find(n => n.macAddress === sourceMac);
  const btn = document.getElementById('btn-toggle-node-power');
  if (btn && node) btn.textContent = node.isOnline ? 'Simulate Node Power Off' : 'Turn Node Back Online';
}

async function toggleSelectedNodePower() {
  const mac = topologyState.selectedNodeMac;
  if (!mac) { alert('Select a node first.'); return; }
  const node = topologyState.nodes.find(n => n.macAddress === mac);
  if (!node) return;
  await sxPost(`/topology/nodes/${encodeURIComponent(mac)}/status`, { isOnline: !node.isOnline });
  await loadMeshTopologyData();
  await computeAndHighlightMst();
  await traceRouteForNode(mac);
}

// Self-healing demo: cut the Gateway <-> Repeater Alpha link. Alpha's zone then reroutes
// through the other repeaters over the cross-links instead of being cut off.
async function simulateNodeFailureDemo() {
  const alpha = 'ESP:AA:01:02:03:04';
  const sensor = 'ESP:AA:10:20:30:40';
  const r = await sxPost('/topology/links/status', { sourceMac: SX_GATEWAY, destinationMac: alpha, isActive: sxLinkCut });
  if (!r.ok) {
    alert('The link endpoint is missing from Program.cs. Add the /api/topology/links/status block first.');
    return;
  }
  sxLinkCut = !sxLinkCut;
  await loadMeshTopologyData();
  await computeAndHighlightMst();
  topologyState.selectedNodeMac = sensor;
  const sel = document.getElementById('select-trace-node');
  if (sel) sel.value = sensor;
  await traceRouteForNode(sensor);
  const status = document.getElementById('mesh-canvas-status');
  if (status) status.textContent = sxLinkCut
    ? 'Gateway to Repeater Alpha link CUT. Zone A traffic now reroutes via the other repeaters.'
    : 'Link restored. Zone A is back on its direct path.';
}

// ---------- AVL HISTORICAL QUERIES ----------
async function refreshAvlIndexedCount() {
  const now = Date.now();
  const d = await sxGet(`/topology/historical/range?from=${sxIso(new Date(now - 60000))}&to=${sxIso(new Date(now + 60000))}`);
  const el = document.getElementById('topo-stat-avl-count');
  if (el) el.textContent = d ? d.count : 0;
}

async function executeAvlRangeQuery() {
  const mac = document.getElementById('avl-query-mac')?.value.trim();
  const fromVal = document.getElementById('avl-query-from')?.value;
  const toVal = document.getElementById('avl-query-to')?.value;
  const from = fromVal ? new Date(fromVal) : new Date(Date.now() - 120000);
  const to = toVal ? new Date(toVal) : new Date(Date.now() + 60000);
  let url = `/topology/historical/range?from=${sxIso(from)}&to=${sxIso(to)}`;
  if (mac) url += `&nodeId=${encodeURIComponent(mac)}`;
  const d = await sxGet(url);
  const badge = document.getElementById('avl-query-count-badge');
  const tbody = document.getElementById('avl-results-tbody');
  if (!d || !tbody) return;
  if (badge) badge.textContent = `${d.count} matching records${d.count > 200 ? ' (showing last 200)' : ''}`;
  if (!d.readings.length) {
    tbody.innerHTML = '<tr><td colspan="6" style="text-align:center;color:var(--text-muted);font-style:italic;">No readings in this window. Widen the From/To range.</td></tr>';
    return;
  }
  tbody.innerHTML = d.readings.slice(-200).map(r => `<tr>
    <td style="font-family:var(--font-mono);color:var(--teal-accent);">${sxClock(r.timestamp)}</td>
    <td style="font-family:var(--font-mono);font-weight:600;">${sxEsc(r.mac)}</td>
    <td style="font-weight:700;color:#fff;">${Number(r.value).toFixed(2)}</td>
    <td>${sxEsc(r.unit)}</td>
    <td><span class="badge" style="background:${r.isAnomaly ? 'rgba(239,68,68,0.15)' : 'rgba(16,185,129,0.15)'};color:${r.isAnomaly ? '#ef4444' : '#10b981'};">${r.isAnomaly ? 'ANOMALY' : 'OK'}</span></td>
    <td><span style="color:var(--teal-accent);font-size:0.72rem;">Indexed in AVL</span></td>
  </tr>`).join('');
}

async function seedDemoAvlTelemetryData() {
  const mac = document.getElementById('avl-query-mac')?.value || (await sxPickMac('PowerConsumption'));
  if (!mac) { alert('No registered sensor available.'); return; }
  for (let i = 0; i < 60; i++) {
    await sxPost('/telemetry/ingest', { deviceId: mac, value: Number((220 + Math.sin(i * 0.3) * 15).toFixed(2)), unit: 'W' });
  }
  const fromEl = document.getElementById('avl-query-from');
  if (fromEl) {
    const d = new Date(Date.now() - 2 * 60000);
    fromEl.value = new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
  }
  await refreshAvlIndexedCount();
  await executeAvlRangeQuery();
  alert(`Ingested 60 readings for ${mac}. Each one was inserted into the AVL tree.`);
}

// ---------- auto-refresh while the Command Stream tab is open ----------
setInterval(() => {
  if (appState.currentScreen !== 'workspace' || appState.currentTab !== 'commandstream') return;
  fetchCommandStreamMetrics();
  fetchProcessedPriorityAlerts();
  fetchChronologicalTimeline();
}, 3000);

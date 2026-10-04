import { api } from "../api.js";
import { $, qs, qsa, escapeHtml, toast, guard, options } from "../utils/dom.js";
import { parseMoney, todayISO } from "../utils/formatters.js";
import { state, loadRefs, STAGES, jobLabel } from "../state.js";

const TARGET_FIELDS = [
  { key: "name", label: "Nome do profissional" },
  { key: "linkedIn", label: "LinkedIn" },
  { key: "phone", label: "Contato / telefone" },
  { key: "currentCompany", label: "Empresa atual" },
  { key: "currentPosition", label: "Cargo atual" },
  { key: "source", label: "Fonte" },
  { key: "currentSalary", label: "Remuneração atual" },
  { key: "requestedSalary", label: "Expectativa / pretensão" },
  { key: "notes", label: "Observações (do processo)" },
];
const STATUSES = [
  { value: "Active", label: "Em andamento" },
  { value: "Rejected", label: "Reprovado / sem sucesso" },
  { value: "Declined", label: "Declínio" },
  { value: "Hired", label: "Contratado" },
];
const MATCH = {
  New: { label: "Novo", cls: "success" },
  ExactMatch: { label: "Já cadastrado", cls: "info" },
  Conflict: { label: "Conflito", cls: "warn" },
  DuplicateInFile: { label: "Duplicado no arquivo", cls: "neutral" },
  Invalid: { label: "Inválida", cls: "danger" },
};

let imp = null;
const reset = () => {
  imp = { workbook: null, fileName: "", sheetName: "", rows: [], firstRow: 1, headerIdx: -1, mapping: {}, start: 0, end: -1,
    meta: { empresa: "", posicao: "", data: "" }, targetJobs: [], extracted: [], preview: null, decisions: [], result: null };
};

const isBlank = (r) => !r || r.every((c) => String(c ?? "").trim() === "");
const sheetRow = (idx) => imp.firstRow + idx;

export function renderImport() {
  if (!imp) reset();
  const el = $("view-importar");
  el.innerHTML = `
    <div class="banner"><span>ℹ️</span><div>Traga uma planilha existente (como a base de prospecção) para popular candidatos e vinculá-los ao pipeline de uma vaga.
      Nada é gravado antes da confirmação, e nenhuma linha é descartada sem aviso.</div></div>
    <div class="import-steps">
      <span class="import-step ${imp.workbook ? "done" : "on"}">1. Arquivo</span>
      <span class="import-step ${imp.preview ? "done" : imp.workbook ? "on" : ""}">2. Vaga e colunas</span>
      <span class="import-step ${imp.result ? "done" : imp.preview ? "on" : ""}">3. Prévia e conflitos</span>
      <span class="import-step ${imp.result ? "on" : ""}">4. Resultado</span>
    </div>
    <div class="card">
      <div class="card-head"><h2>1. Selecione o arquivo</h2>${imp.workbook ? `<button class="btn btn-ghost btn-sm" id="impReset">Recomeçar</button>` : ""}</div>
      ${imp.workbook ? `<div class="muted" style="margin-bottom:10px;">Arquivo: <b>${escapeHtml(imp.fileName)}</b></div>` : `<input type="file" id="impFile" accept=".xlsx,.xls">`}
      <div id="impSheetPicker" style="margin-top:14px;"></div>
    </div>
    <div id="impMappingArea"></div>
    <div id="impPreviewArea"></div>
    <div id="impResultArea"></div>`;

  if ($("impReset")) $("impReset").addEventListener("click", () => { reset(); renderImport(); });
  if ($("impFile")) $("impFile").addEventListener("change", (e) => guard(async () => {
    const file = e.target.files[0];
    if (!file) return;
    const XLSX = await import("xlsx");
    imp.workbook = XLSX.read(await file.arrayBuffer(), { type: "array" });
    imp.XLSX = XLSX;
    imp.fileName = file.name;
    imp.sheetName = imp.workbook.SheetNames[0];
    loadSheet();
    renderImport();
  }));
  if (imp.workbook) renderSheetPicker();
  if (imp.workbook) renderMapping();
  if (imp.preview) renderPreview();
  if (imp.result) renderResult();
}

function renderSheetPicker() {
  $("impSheetPicker").innerHTML = `
    <label>Aba da planilha</label>
    <select id="impSheetSel">${imp.workbook.SheetNames.map((s) => `<option value="${escapeHtml(s)}" ${s === imp.sheetName ? "selected" : ""}>${escapeHtml(s)}</option>`).join("")}</select>`;
  $("impSheetSel").addEventListener("change", (e) => {
    imp.sheetName = e.target.value;
    loadSheet();
    renderImport();
  });
}

/** Lê a aba e detecta cliente/posição/data, cabeçalho, mapeamento e o intervalo de dados. */
function loadSheet() {
  const XLSX = imp.XLSX;
  const ws = imp.workbook.Sheets[imp.sheetName];
  imp.rows = XLSX.utils.sheet_to_json(ws, { header: 1, defval: "", blankrows: true, raw: true });
  imp.firstRow = ws["!ref"] ? XLSX.utils.decode_range(ws["!ref"]).s.r + 1 : 1;
  imp.preview = null;
  imp.result = null;

  // Cabeçalho "Cliente: … Posição: … Data: …" (em células separadas ou numa só).
  const top = imp.rows.slice(0, 8);
  const text = top.map((r) => r.filter((c) => String(c).trim()).join(" | ")).join("\n");
  const cliente = text.match(/Cliente:\s*(.+?)(?=\s+Posi[cç][aã]o:|\s+Data:|\s*\||\n|$)/i);
  const posicao = text.match(/Posi[cç][aã]o:\s*(.+?)(?=\s+Data:|\s*\||\n|$)/i);
  const data = text.match(/Data:\s*(\d{1,2}\/\d{1,2}\/\d{4}|\d{1,2}\/\d{4})/i);
  imp.meta = { empresa: cliente ? cliente[1].trim() : "", posicao: posicao ? posicao[1].trim() : "", data: parseHeaderDate(data?.[1]) };

  // Cabeçalho = linha com 3+ células que são exatamente títulos de coluna (uma linha de dados como
  // "linkedin.com/in/… | Empresa X" não conta).
  const HEADER_CELL = /^(#|n[ºo°]?|profissional|nome|linkedin|contato|telefone|celular|empresa|posi[cç][aã]o|cargo|indica[cç][oõ]es|fonte|origem|remunera[cç][aã]o|sal[aá]rio|pretens[aã]o|expectativa|observa[cç][oõ]es)$/i;
  imp.headerIdx = -1;
  for (let i = 0; i < Math.min(imp.rows.length, 12); i++) {
    const hits = imp.rows[i].filter((c) => HEADER_CELL.test(String(c).trim())).length;
    if (hits >= 3) { imp.headerIdx = i; break; }
  }
  let lastMeta = -1;
  top.forEach((r, i) => { if (/(Cliente|Posi[cç][aã]o|Data):/i.test(r.join(" "))) lastMeta = i; });
  imp.start = imp.headerIdx >= 0 ? imp.headerIdx + 1 : lastMeta + 1;
  while (imp.start < imp.rows.length && isBlank(imp.rows[imp.start])) imp.start++;

  // Fim do bloco: duas linhas vazias seguidas (evita importar listas que vêm depois, como empresas-alvo).
  let lastFilled = imp.start - 1, blankRun = 0;
  for (let i = imp.start; i < imp.rows.length; i++) {
    if (isBlank(imp.rows[i])) { if (++blankRun >= 2) break; } else { blankRun = 0; lastFilled = i; }
  }
  imp.end = lastFilled;
  imp.mapping = autoMapping();
}

function parseHeaderDate(s) {
  if (!s) return "";
  const p = s.split("/");
  if (p.length === 3) return `${p[2]}-${p[1].padStart(2, "0")}-${p[0].padStart(2, "0")}`;
  if (p.length === 2) return `${p[1]}-${p[0].padStart(2, "0")}-01`;
  return "";
}

function autoMapping() {
  const map = {};
  const header = imp.headerIdx >= 0 ? imp.rows[imp.headerIdx] : null;
  if (header) {
    header.forEach((h, i) => {
      const hl = String(h).toLowerCase();
      const set = (k) => { if (map[k] === undefined) map[k] = i; };
      if (hl.includes("profissional") || hl.includes("nome")) set("name");
      else if (hl.includes("linkedin")) set("linkedIn");
      else if (hl.includes("contato") || hl.includes("telefone") || hl.includes("celular")) set("phone");
      else if (hl.includes("empresa")) set("currentCompany");
      else if (hl.includes("posi") || hl.includes("cargo")) set("currentPosition");
      else if (hl.includes("indic") || hl.includes("fonte") || hl.includes("origem")) set("source");
      else if (hl.includes("pretens") || hl.includes("expectativa")) set("requestedSalary");
      else if (hl.includes("remun") || hl.includes("salár") || hl.includes("salar")) set("currentSalary");
      else if (hl.includes("observ") || hl.includes("coment")) set("notes");
    });
    return map;
  }
  // Sem cabeçalho: reconhece LinkedIn e contato pelo conteúdo; o nome costuma estar à esquerda do LinkedIn.
  const data = imp.rows.slice(imp.start, imp.end + 1).filter((r) => !isBlank(r));
  const nCols = Math.max(0, ...data.map((r) => r.length));
  const share = (col, re) => data.filter((r) => re.test(String(r[col] ?? ""))).length / Math.max(1, data.length);
  for (let c = 0; c < nCols; c++) {
    if (map.linkedIn === undefined && share(c, /linkedin\.com/i) > 0.5) map.linkedIn = c;
    else if (map.phone === undefined && share(c, /inmail|\d{4}.?\d{4}/i) > 0.5) map.phone = c;
  }
  if (map.linkedIn > 0) map.name = map.linkedIn - 1;
  return map;
}

function renderMapping() {
  const header = imp.headerIdx >= 0 ? imp.rows[imp.headerIdx] : null;
  const sample = imp.rows[imp.start] || [];
  const nCols = Math.max(1, ...imp.rows.slice(0, imp.end + 2).map((r) => r.length));
  const colLabel = (i) => {
    const h = header && String(header[i] ?? "").trim();
    const s = !h && String(sample[i] ?? "").trim();
    return `Coluna ${i + 1}${h ? " — " + h.slice(0, 24) : s ? " (ex.: " + s.slice(0, 20) + ")" : ""}`;
  };
  const openJobs = state.jobs.filter((j) => j.status === "Open" && !imp.targetJobs.includes(j.id));
  const companyGuess = state.companies.find((c) => c.name.toLowerCase() === imp.meta.empresa.toLowerCase());

  $("impMappingArea").innerHTML = `
    <div class="card">
      <div class="card-head"><h2>2. Vaga de destino</h2><span class="hint">escolha ou crie a vaga antes de importar</span></div>
      ${imp.meta.empresa || imp.meta.posicao ? `<div class="field-note" style="margin-bottom:10px;">Detectado na planilha: ${escapeHtml(imp.meta.empresa || "—")} · ${escapeHtml(imp.meta.posicao || "—")}${imp.meta.data ? " · " + escapeHtml(imp.meta.data) : ""}</div>` : ""}
      <div class="stack">
        <div>
          ${imp.targetJobs.length ? imp.targetJobs.map((id) => {
            const j = state.jobs.find((x) => x.id === id);
            return `<span class="tag info" style="margin:0 6px 6px 0;">${escapeHtml(j ? j.code + " — " + jobLabel(j) : id)} <button data-rm-target="${id}" title="Remover">✕</button></span>`;
          }).join("") : `<div class="banner warn" style="margin-bottom:0;">Nenhuma vaga escolhida ainda.</div>`}
        </div>
        <div class="grid">
          <div class="full"><label>Usar vaga existente</label>
            <div style="display:flex;gap:8px;"><select id="impJobSel">${options(openJobs, "", { label: (j) => `${j.code} — ${jobLabel(j)}`, empty: "Selecione…" })}</select>
            <button class="btn btn-soft btn-sm" id="impAddJob">Adicionar</button></div>
          </div>
        </div>
        <details ${imp.targetJobs.length ? "" : "open"}>
          <summary style="cursor:pointer;font-weight:600;font-size:13px;margin-bottom:10px;">Criar nova vaga</summary>
          <div class="grid">
            <div><label>Empresa (cliente)</label><input id="impNovaEmpresa" list="impEmpresas" value="${escapeHtml(companyGuess?.name || imp.meta.empresa)}">
              <datalist id="impEmpresas">${state.companies.map((c) => `<option value="${escapeHtml(c.name)}">`).join("")}</datalist></div>
            <div><label>Posição</label><input id="impNovaPosicao" value="${escapeHtml(imp.meta.posicao)}"></div>
            <div><label>Nível <span class="muted" style="font-weight:400;">(opcional — crie uma vaga por nível)</span></label><input id="impNovoNivel" placeholder="Ex.: Pleno"></div>
            <div><label>Data de abertura</label><input id="impNovaData" type="date" value="${imp.meta.data || todayISO()}"></div>
            <div class="full"><button class="btn btn-soft btn-sm" id="impCreateJob">Criar vaga e usar na importação</button></div>
          </div>
        </details>
      </div>
    </div>

    <div class="card">
      <div class="card-head"><h2>Colunas e intervalo</h2></div>
      <div class="field-note" style="margin-bottom:10px;">Cabeçalho detectado: ${imp.headerIdx >= 0 ? "linha " + sheetRow(imp.headerIdx) : "não encontrado — confira o mapeamento por posição de coluna"}</div>
      <div class="grid" style="margin-bottom:14px;">
        <div><label>Primeira linha de dados</label><input id="impStart" type="number" min="1" value="${sheetRow(imp.start)}"></div>
        <div><label>Última linha de dados</label><input id="impEnd" type="number" min="1" value="${sheetRow(imp.end)}"></div>
      </div>
      <div class="map-grid">
        ${TARGET_FIELDS.map((f) => `
          <div>
            <label>${f.label}</label>
            <select data-map="${f.key}">
              <option value="">— não importar —</option>
              ${Array.from({ length: nCols }).map((_, i) => `<option value="${i}" ${imp.mapping[f.key] === i ? "selected" : ""}>${escapeHtml(colLabel(i))}</option>`).join("")}
            </select>
          </div>`).join("")}
      </div>
      <div class="field-note" style="margin-top:8px;">Valores não numéricos em colunas de remuneração são preservados nas observações.</div>
      <div class="actions-row"><button class="btn btn-primary" id="btnPreviewImport">Gerar prévia</button></div>
    </div>`;

  qsa("[data-rm-target]").forEach((b) => b.addEventListener("click", () => {
    imp.targetJobs = imp.targetJobs.filter((id) => id !== b.dataset.rmTarget);
    imp.preview = null;
    renderImport();
  }));
  $("impAddJob").addEventListener("click", () => {
    const id = $("impJobSel").value;
    if (!id) return;
    imp.targetJobs.push(id);
    imp.preview = null;
    renderImport();
  });
  $("impCreateJob").addEventListener("click", () => guard(async () => {
    const empresa = $("impNovaEmpresa").value.trim();
    const posicao = $("impNovaPosicao").value.trim();
    if (!empresa || !posicao) { toast("Informe empresa e posição.", true); return; }
    let company = state.companies.find((c) => c.name.toLowerCase() === empresa.toLowerCase());
    if (!company) company = await api.post("/api/companies", { name: empresa });
    const job = await api.post("/api/jobs", {
      companyId: company.id, positionName: posicao, openedAt: $("impNovaData").value || todayISO(),
      level: $("impNovoNivel").value.trim() || null,
    });
    await loadRefs();
    imp.targetJobs.push(job.id);
    imp.preview = null;
    toast("Vaga criada: " + job.code);
    renderImport();
  }));
  qsa("[data-map]").forEach((s) => s.addEventListener("change", () => {
    imp.mapping[s.dataset.map] = s.value === "" ? undefined : Number(s.value);
  }));
  $("impStart").addEventListener("change", (e) => { imp.start = Math.max(0, Number(e.target.value) - imp.firstRow); });
  $("impEnd").addEventListener("change", (e) => { imp.end = Math.min(imp.rows.length - 1, Number(e.target.value) - imp.firstRow); });
  $("btnPreviewImport").addEventListener("click", () => guard(runPreview));
}

function cell(r, key) {
  const col = imp.mapping[key];
  return col === undefined ? "" : r[col];
}

function extractRows() {
  const start = Math.max(0, Number($("impStart").value) - imp.firstRow);
  const end = Math.min(imp.rows.length - 1, Number($("impEnd").value) - imp.firstRow);
  imp.start = start;
  imp.end = end;
  const out = [];
  for (let i = start; i <= end; i++) {
    const r = imp.rows[i];
    if (isBlank(r)) continue;
    const text = (key) => { const v = String(cell(r, key) ?? "").trim(); return v || null; };
    // Sem nome, LinkedIn e telefone a linha não identifica ninguém (ex.: numeração ou anotação solta): ignora.
    if (!text("name") && !text("linkedIn") && !text("phone")) continue;
    const notes = [text("notes")];
    const money = (key, label) => {
      const raw = cell(r, key);
      const n = parseMoney(raw);
      if (n === null && String(raw ?? "").trim()) notes.push(`${label} (planilha): ${String(raw).trim()}`);
      return n;
    };
    const currentSalary = money("currentSalary", "Remuneração");
    const requestedSalary = money("requestedSalary", "Expectativa");
    out.push({
      rowNumber: sheetRow(i),
      name: text("name"),
      linkedIn: text("linkedIn"),
      phone: text("phone"),
      currentCompany: text("currentCompany"),
      currentPosition: text("currentPosition"),
      source: text("source"),
      currentSalary,
      requestedSalary,
      notes: notes.filter(Boolean).join("\n") || null,
    });
  }
  return out;
}

async function runPreview() {
  if (!imp.targetJobs.length) { toast("Escolha ou crie a vaga de destino.", true); return; }
  if (imp.mapping.name === undefined) { toast("Mapeie a coluna do nome do profissional.", true); return; }
  imp.extracted = extractRows();
  if (!imp.extracted.length) { toast("Nenhuma linha com dados no intervalo informado.", true); return; }
  imp.preview = await api.post("/api/imports/preview", { jobIds: imp.targetJobs, rows: imp.extracted });
  imp.result = null;
  imp.decisions = imp.preview.rows.map((r) => ({
    include: ["New", "ExactMatch", "Conflict"].includes(r.match),
    resolution: null,
    jobId: imp.targetJobs[0],
    stage: "Acessado",
    status: "Active",
    offLimitsOverride: false,
  }));
  renderImport();
  $("impPreviewArea").scrollIntoView({ behavior: "smooth" });
}

function renderPreview() {
  const p = imp.preview;
  const jobs = imp.targetJobs.map((id) => state.jobs.find((j) => j.id === id)).filter(Boolean);
  const included = imp.decisions.filter((d) => d.include).length;
  const pending = p.rows.filter((r, i) => imp.decisions[i].include && r.match === "Conflict" && !imp.decisions[i].resolution);

  $("impPreviewArea").innerHTML = `
    <div class="card">
      <div class="card-head"><h2>3. Prévia (${p.rows.length} linha${p.rows.length === 1 ? "" : "s"})</h2>
        <span class="hint">${p.new} novos · ${p.exactMatch} já cadastrados · ${p.conflict} conflitos · ${p.duplicateInFile} duplicados · ${p.invalid} inválidas</span>
      </div>
      <div class="bulk-bar">
        <b>Linhas incluídas:</b>
        <label class="radio-inline">Etapa <select id="bulkStage"><option value="">—</option>${STAGES.map((s) => `<option>${s}</option>`).join("")}</select></label>
        <label class="radio-inline">Status <select id="bulkStatus"><option value="">—</option>${STATUSES.filter((s) => s.value !== "Hired").map((s) => `<option value="${s.value}">${s.label}</option>`).join("")}</select></label>
        ${jobs.length > 1 ? `<label class="radio-inline">Vaga <select id="bulkJob"><option value="">—</option>${jobs.map((j) => `<option value="${j.id}">${escapeHtml(j.code)}</option>`).join("")}</select></label>` : ""}
        <button class="btn btn-ghost btn-sm" id="bulkApply">Aplicar</button>
        <span style="flex:1"></span>
        <button class="btn btn-ghost btn-sm" id="bulkAll">Incluir todas</button>
        <button class="btn btn-ghost btn-sm" id="bulkNone">Excluir todas</button>
      </div>
      <div class="table-wrap">
        <table class="import-table">
          <thead><tr><th></th><th>Linha</th><th>Profissional</th><th>Contato</th><th>Situação</th>${jobs.length > 1 ? "<th>Vaga</th>" : ""}<th>Etapa</th><th>Status</th></tr></thead>
          <tbody>${p.rows.map((r, i) => rowHtml(r, i, jobs)).join("")}</tbody>
        </table>
      </div>
      ${pending.length ? `<div class="banner warn" style="margin-top:12px;">Resolva os conflitos antes de importar (linhas ${pending.map((r) => r.rowNumber).join(", ")}): escolha Atualizar ou Manter banco.</div>` : ""}
      <div class="actions-row">
        <button class="btn btn-primary" id="btnConfirmImport" ${included && !pending.length ? "" : "disabled"}>Importar ${included} linha(s)</button>
      </div>
    </div>`;

  const tbody = qs("#impPreviewArea tbody");
  tbody.addEventListener("change", (e) => {
    const t = e.target;
    const i = Number(t.dataset.i);
    if (isNaN(i)) return;
    const d = imp.decisions[i];
    if (t.dataset.f === "include") d.include = t.checked;
    else if (t.dataset.f === "override") d.offLimitsOverride = t.checked;
    else d[t.dataset.f] = t.value;
    renderPreview();
  });
  $("bulkApply").addEventListener("click", () => {
    const stage = $("bulkStage").value, status = $("bulkStatus").value, job = $("bulkJob")?.value;
    imp.decisions.forEach((d) => {
      if (!d.include) return;
      if (stage) d.stage = stage;
      if (status) d.status = status;
      if (job) d.jobId = job;
    });
    renderPreview();
  });
  $("bulkAll").addEventListener("click", () => { imp.decisions.forEach((d, i) => { if (p.rows[i].match !== "Invalid") d.include = true; }); renderPreview(); });
  $("bulkNone").addEventListener("click", () => { imp.decisions.forEach((d) => { d.include = false; }); renderPreview(); });
  $("btnConfirmImport").addEventListener("click", () => guard(confirmImport));
}

function rowHtml(r, i, jobs) {
  const d = imp.decisions[i];
  const m = MATCH[r.match];
  const job = jobs.find((j) => j.id === d.jobId);
  const offLimits = job && r.offLimitsCompanyIds.includes(job.companyId);
  const inJob = r.alreadyInJobIds.includes(d.jobId);
  const field = (f, html) => html.replace("<select", `<select data-i="${i}" data-f="${f}"`);

  let detail = "";
  if (r.match === "Conflict") {
    detail = `<ul class="diff-list">${r.differences.map((x) => `<li><b>${escapeHtml(x.label)}:</b> planilha “${escapeHtml(x.fileValue)}” × banco “${escapeHtml(x.dbValue)}”</li>`).join("")}</ul>
      <label class="radio-inline"><input type="radio" name="res${i}" data-i="${i}" data-f="resolution" value="Update" ${d.resolution === "Update" ? "checked" : ""}> Atualizar</label>
      <label class="radio-inline"><input type="radio" name="res${i}" data-i="${i}" data-f="resolution" value="KeepExisting" ${d.resolution === "KeepExisting" ? "checked" : ""}> Manter banco</label>`;
  }
  if (r.existingCandidateName && r.match !== "Conflict") detail += `<div class="muted">${escapeHtml(r.existingCandidateName)}</div>`;
  if (inJob) detail += `<div><span class="tag neutral">já está nesta vaga — será ignorada</span></div>`;
  if (offLimits) detail += `<div style="margin-top:4px;"><span class="tag danger">⛔ off-limits</span>
    <label class="radio-inline"><input type="checkbox" data-i="${i}" data-f="override" ${d.offLimitsOverride ? "checked" : ""}> prosseguir mesmo assim</label></div>`;

  return `<tr class="${d.include ? "" : "excluded"}">
    <td><input type="checkbox" data-i="${i}" data-f="include" ${d.include ? "checked" : ""} ${r.match === "Invalid" ? "disabled" : ""}></td>
    <td class="mono">${r.rowNumber}</td>
    <td><b>${escapeHtml(r.name || "—")}</b>${r.warnings.map((w) => `<div class="muted" style="font-size:11px;">⚠ ${escapeHtml(w)}</div>`).join("")}</td>
    <td>${r.linkedIn ? `<div class="mono" style="font-size:11px;">${escapeHtml(r.linkedIn)}</div>` : ""}<span class="muted">${escapeHtml(r.phone || "")}</span></td>
    <td><span class="tag ${m.cls}">${m.label}${r.duplicateOfRow ? " (linha " + r.duplicateOfRow + ")" : ""}</span>${detail}</td>
    ${jobs.length > 1 ? `<td>${field("jobId", `<select>${jobs.map((j) => `<option value="${j.id}" ${j.id === d.jobId ? "selected" : ""}>${escapeHtml(j.code)}</option>`).join("")}</select>`)}</td>` : ""}
    <td>${field("stage", `<select>${STAGES.map((s) => `<option ${s === d.stage ? "selected" : ""}>${s}</option>`).join("")}</select>`)}</td>
    <td>${field("status", `<select>${STATUSES.map((s) => `<option value="${s.value}" ${s.value === d.status ? "selected" : ""}>${s.label}</option>`).join("")}</select>`)}</td>
  </tr>`;
}

async function confirmImport() {
  const rows = imp.preview.rows.map((r, i) => {
    const d = imp.decisions[i];
    return {
      row: imp.extracted[i],
      include: d.include,
      resolution: r.match === "Conflict" ? d.resolution : null,
      jobId: d.jobId,
      stage: d.stage,
      status: d.status,
      offLimitsOverride: d.offLimitsOverride,
    };
  });
  imp.result = await api.post("/api/imports/confirm", { rows });
  imp.preview = null;
  await loadRefs();
  toast(`Importação concluída: ${imp.result.applicationsCreated} prospecção(ões) criada(s).`);
  renderImport();
  $("impResultArea").scrollIntoView({ behavior: "smooth" });
}

function renderResult() {
  const r = imp.result;
  const skipped = r.rows.filter((x) => x.outcome === "Skipped");
  $("impResultArea").innerHTML = `
    <div class="card">
      <div class="card-head"><h2>4. Resultado</h2><button class="btn btn-ghost btn-sm" id="impNew">Importar outra aba/arquivo</button></div>
      <div class="kpi-grid">
        <div class="kpi"><div class="eyebrow">Candidatos novos</div><div class="val">${r.candidatesCreated}</div></div>
        <div class="kpi"><div class="eyebrow">Candidatos atualizados</div><div class="val">${r.candidatesUpdated}</div></div>
        <div class="kpi"><div class="eyebrow">Prospecções criadas</div><div class="val">${r.applicationsCreated}</div></div>
        <div class="kpi"><div class="eyebrow">Linhas não importadas</div><div class="val">${r.skipped}</div></div>
      </div>
      ${skipped.length ? `<div class="table-wrap" style="margin-top:14px;"><table>
        <thead><tr><th>Linha</th><th>Motivo</th></tr></thead>
        <tbody>${skipped.map((x) => `<tr><td class="mono">${x.rowNumber}</td><td>${escapeHtml(x.reason)}</td></tr>`).join("")}</tbody>
      </table></div>` : ""}
    </div>`;
  $("impNew").addEventListener("click", () => {
    const { workbook, XLSX, fileName, sheetName } = imp;
    reset();
    Object.assign(imp, { workbook, XLSX, fileName, sheetName });
    loadSheet();
    renderImport();
  });
}

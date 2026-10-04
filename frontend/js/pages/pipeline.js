import { api } from "../api.js";
import { $, qs, qsa, escapeHtml, toast, guard, debounce, options } from "../utils/dom.js";
import { openModal, confirmDialog } from "../utils/modal.js";
import { chipPicker, wireChipPicker } from "../utils/chips.js";
import { fmtDateBR, fmtDateTimeBR, todayISO, STATUS_LABELS } from "../utils/formatters.js";
import { state, scope, STAGES, optionValues, loadRefs, jobLabel } from "../state.js";
import { renderCurrentView } from "../router.js";

// Termo da busca local do Kanban; sobrevive às re-renderizações (ex.: depois de mover um card).
let boardSearch = "";
const searchKey = (s) => String(s || "").normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase().trim();

/** Filtra os cards já renderizados pelo nome do candidato, sem chamar a API. */
function applyBoardSearch() {
  const term = searchKey(boardSearch);
  let visible = 0;
  qsa("#view-pipeline .kcol").forEach((col) => {
    const cards = qsa(".kcard", col);
    let shown = 0;
    cards.forEach((c) => {
      const match = !term || c.dataset.name.includes(term);
      c.hidden = !match;
      if (match) shown++;
    });
    visible += shown;
    qs(".kcol-count", col).textContent = term ? `${shown}/${cards.length}` : String(cards.length);
    const empty = qs(".kcol-empty", col);
    if (empty) empty.textContent = cards.length ? "Nenhum resultado" : "Vazio";
    if (empty) empty.hidden = cards.length > 0 && shown > 0;
  });
  const total = qsa("#view-pipeline .kcard").length;
  $("pipelineCount").textContent = term
    ? `${visible} de ${total} candidato(s) ativos correspondem à busca`
    : `${total} candidato(s) ativos no filtro atual`;
}

export async function renderPipeline() {
  const el = $("view-pipeline");
  const cards = await api.get("/api/pipeline", scope());

  el.innerHTML = `
    <div class="toolbar">
      <div class="toolbar-left">
        <input type="text" class="search-input" id="pipelineSearch" placeholder="Buscar candidato no pipeline..." value="${escapeHtml(boardSearch)}" style="min-width:280px !important;">
        <span class="hint muted" style="font-size:12.5px;" id="pipelineCount"></span>
      </div>
      <button class="btn btn-primary btn-sm" id="btnNovaProspGeral">+ Nova prospecção</button>
    </div>
    <div class="kanban">
      ${STAGES.map((et) => {
        const doEt = cards.filter((p) => p.stage === et);
        return `<div class="kcol">
          <div class="kcol-head"><b>${et}</b><span class="kcol-count">${doEt.length}</span></div>
          ${doEt.map((p) => `
            <div class="kcard" data-open-card="${p.id}" data-name="${escapeHtml(searchKey(p.candidateName))}">
              <div class="nm">${escapeHtml(p.candidateName)}</div>
              <div class="mt">${escapeHtml(p.jobCode)}</div>
              <div class="tags">
                ${p.source ? `<span class="tag neutral">${escapeHtml(p.source)}</span>` : ""}
                ${p.offLimitsOverride ? '<span class="tag danger">⛔ off-limits</span>' : ""}
              </div>
            </div>`).join("")}
          <div class="kcol-empty">Vazio</div>
        </div>`;
      }).join("")}
    </div>`;

  qsa("[data-open-card]").forEach((c) => c.addEventListener("click", () => guard(() => openApplicationModal(c.dataset.openCard))));
  qs("#btnNovaProspGeral").addEventListener("click", () => openNewApplicationModal(null));
  $("pipelineSearch").addEventListener("input", (e) => { boardSearch = e.target.value; applyBoardSearch(); });
  applyBoardSearch();
}

/** Modal "Nova prospecção" do protótipo, com busca de candidato e aviso de off-limits (D1). */
export function openNewApplicationModal(preselected) {
  const openJobs = state.jobs.filter((j) => j.status === "Open");
  let found = preselected ? [preselected] : [];

  openModal("Nova prospecção", `
    <form id="formNovaProspeccao" class="grid">
      <div class="full">
        <label>Candidato</label>
        ${preselected ? "" : `<input id="npBusca" placeholder="Buscar por nome, empresa, LinkedIn…" style="margin-bottom:6px;">`}
        <select id="npCandidato" required>${candidateOptions(found, preselected?.id)}</select>
      </div>
      <div class="full">
        <label>Vaga</label>
        <select id="npVaga" required>
          ${options(openJobs, state.filter.jobId, { label: (j) => `${j.code} — ${jobLabel(j)}`, empty: "Selecione…" })}
        </select>
      </div>
      <div class="full" id="npOffLimitsWarning" hidden>
        <div class="banner warn" style="margin-bottom:0;">
          <span>⛔</span>
          <div>
            <div id="npOffLimitsText"></div>
            <label style="margin-top:8px;display:flex;align-items:center;gap:6px;font-weight:500;">
              <input type="checkbox" id="npOffLimitsOverride"> Estou ciente do conflito e quero prosseguir mesmo assim
            </label>
          </div>
        </div>
      </div>
      <div>
        <label>Fonte do contato</label>
        <select id="npFonte">${options(optionValues("Source"), "", { value: (x) => x, label: (x) => x, empty: "—" })}</select>
      </div>
      <div>
        <label>Data de acesso</label>
        <input id="npData" type="date" value="${todayISO()}">
      </div>
      <div class="full actions-row" style="margin-top:0;">
        <button class="btn btn-primary" type="submit" id="npSubmitBtn">Adicionar ao pipeline</button>
      </div>
    </form>`, {
    onMount: (close) => {
      const selCand = $("npCandidato");
      const selVaga = $("npVaga");
      const warnBox = $("npOffLimitsWarning");
      const overrideChk = $("npOffLimitsOverride");
      const submitBtn = $("npSubmitBtn");

      function checkOffLimits() {
        const cand = found.find((c) => c.id === selCand.value);
        const vaga = state.jobs.find((j) => j.id === selVaga.value);
        const bloqueado = cand && vaga && (cand.offLimits || []).some((o) => o.id === vaga.companyId);
        warnBox.hidden = !bloqueado;
        overrideChk.checked = false;
        submitBtn.disabled = !!bloqueado;
        if (bloqueado) $("npOffLimitsText").textContent = `${cand.name} está marcado como off-limits para ${vaga.companyName} (conflito de interesse).`;
      }

      const busca = $("npBusca");
      if (busca) {
        const search = debounce(() => guard(async () => {
          const res = await api.get("/api/candidates", { search: busca.value.trim(), pageSize: 30 });
          found = res.items;
          selCand.innerHTML = candidateOptions(found, null);
          checkOffLimits();
        }), 250);
        busca.addEventListener("input", search);
        search();
        busca.focus();
      }

      selCand.addEventListener("change", checkOffLimits);
      selVaga.addEventListener("change", checkOffLimits);
      overrideChk.addEventListener("change", () => { submitBtn.disabled = warnBox.hidden ? false : !overrideChk.checked; });
      checkOffLimits();

      qs("#formNovaProspeccao").addEventListener("submit", (e) => {
        e.preventDefault();
        if (!selCand.value || !selVaga.value) return;
        if (!warnBox.hidden && !overrideChk.checked) return;
        guard(async () => {
          await api.post("/api/applications", {
            candidateId: selCand.value,
            jobId: selVaga.value,
            source: $("npFonte").value || null,
            accessedAt: $("npData").value || todayISO(),
            offLimitsOverride: !warnBox.hidden && overrideChk.checked,
          });
          toast("Candidato adicionado ao pipeline.");
          close();
          await loadRefs();
          renderCurrentView();
        });
      });
    },
  });
}

function candidateOptions(list, selectedId) {
  if (!list.length) return `<option value="">Nenhum candidato encontrado</option>`;
  return `<option value="">Selecione…</option>` + list.map((c) =>
    `<option value="${c.id}" ${c.id === selectedId ? "selected" : ""}>${escapeHtml(c.name)}${c.currentCompany ? " — " + escapeHtml(c.currentCompany) : ""}</option>`).join("");
}

/** Modal de etapa do protótipo. Ações mapeadas para move/resolve (D5, D6). */
async function openApplicationModal(id) {
  const p = await api.get(`/api/applications/${id}`);
  const idx = STAGES.indexOf(p.stage);
  const proxima = STAGES[idx + 1];
  const motivosSel = [...(p.rejectionReasons || [])];
  const avaliacao = proxima === "Aprovação" || p.stage === "Aprovação";
  const naProposta = p.stage === "Proposta";

  let extraCampos = "";
  if (avaliacao) {
    extraCampos = `
      <div class="full"><label>Resultado da avaliação</label>
        <select id="mpResultadoAprov">
          <option value="Aderente">Aderente — segue no processo</option>
          <option value="Não aderente">Não aderente — não avança (encerra como reprovado)</option>
        </select>
      </div>
      <div class="full" id="mpMotivosWrap"><label>Motivo (tags)</label>
        ${chipPicker("mpMotivos", optionValues("RejectionReason"), motivosSel)}
      </div>
      <div><label>Remuneração oferecida (R$)</label><input id="mpRemOferecida" type="number" min="0" step="100" value="${p.offeredSalary ?? ""}"></div>
      <div><label>Pretensão do candidato (R$)</label><input id="mpRemPretendida" type="number" min="0" step="100" value="${p.requestedSalary ?? ""}"></div>`;
  }
  if (naProposta) {
    extraCampos += `
      <div class="full"><label>Resultado da proposta</label>
        <select id="mpResultadoProposta">
          <option value="">— aguardando —</option>
          <option value="Aceite">Aceite (contratado — fecha a vaga)</option>
          <option value="Recusa">Recusa do candidato (declínio)</option>
        </select>
      </div>`;
  }

  const historyHtml = `<ul class="timeline">${p.history.map((h) => `
    <li><span class="when">${fmtDateTimeBR(h.enteredAt)}</span>
      <span><b>${escapeHtml(h.stage)}</b>${h.status !== "Active" ? ` · <span class="tag ${h.status === "Hired" ? "success" : h.status === "Declined" ? "warn" : "danger"}">${STATUS_LABELS[h.status]}</span>` : ""}
      ${h.actorName ? `<span class="muted"> · ${escapeHtml(h.actorName)}</span>` : ""}
      ${h.notes ? `<div class="muted">${escapeHtml(h.notes)}</div>` : ""}</span></li>`).join("")}</ul>`;

  openModal(p.candidate.name, `
    <div class="muted" style="font-size:12.5px;margin-bottom:14px;">${escapeHtml(p.positionName + " · " + p.companyName + " · " + p.jobCode)}
      · acessado em ${fmtDateBR(p.accessedAt)}${p.source ? " · " + escapeHtml(p.source) : ""}</div>
    ${p.offLimitsOverride ? `<div class="banner warn">⛔ Prospecção feita com override de off-limits${p.offLimitsOverrideBy ? " por " + escapeHtml(p.offLimitsOverrideBy) : ""} em ${fmtDateTimeBR(p.offLimitsOverrideAt)}.</div>` : ""}
    <div class="stack">
      <div><label>Etapa atual</label>
        <div style="display:flex;gap:8px;flex-wrap:wrap;">
          ${STAGES.map((et, i) => `<span class="tag ${i <= idx ? "success" : "neutral"}">${et}</span>`).join(" ")}
        </div>
      </div>
      <form id="formEtapa" class="grid">
        ${extraCampos}
        <div class="full">
          <label>Observações do processo</label>
          <textarea id="mpNotes">${escapeHtml(p.notes || "")}</textarea>
        </div>
        <div class="full">
          <label>Anotação desta movimentação <span class="muted" style="font-weight:400;">(vai para o histórico)</span></label>
          <input id="mpMoveNote" placeholder="Ex.: entrevista com a área em 29/07">
        </div>
        <div class="full actions-row" style="margin-top:0;">
          ${proxima ? `<button class="btn btn-primary" type="submit">Avançar para "${proxima}"</button>` : `<button class="btn btn-primary" type="submit">Salvar</button>`}
          ${idx > 0 ? `<button type="button" class="btn btn-ghost" id="mpVoltar">Voltar etapa</button>` : ""}
          <button type="button" class="btn-danger" id="mpDeclinio">Declínio do candidato</button>
          <button type="button" class="btn-danger" id="mpEncerrar">Encerrar sem sucesso</button>
        </div>
      </form>
      <div><label>Histórico</label>${historyHtml}</div>
    </div>`, {
    onMount: (close) => {
      if ($("mpMotivos")) wireChipPicker("mpMotivos", motivosSel);
      const salary = (elId) => ($(elId) && $(elId).value !== "" ? Number($(elId).value) : undefined);
      const done = async (msg) => { toast(msg); close(); await loadRefs(); renderCurrentView(); };

      async function saveProcessData() {
        await api.put(`/api/applications/${p.id}`, {
          source: p.source,
          notes: $("mpNotes").value.trim(),
          offeredSalary: salary("mpRemOferecida") ?? p.offeredSalary,
          requestedSalary: salary("mpRemPretendida") ?? p.requestedSalary,
        });
      }

      qs("#formEtapa").addEventListener("submit", (e) => {
        e.preventDefault();
        guard(async () => {
          await saveProcessData();
          const note = $("mpMoveNote").value.trim() || null;
          if ($("mpResultadoAprov") && $("mpResultadoAprov").value === "Não aderente") {
            if (!motivosSel.length && !(await confirmDialog("Encerrar como não aderente sem informar motivo?"))) return openApplicationModal(p.id);
            await api.post(`/api/applications/${p.id}/resolve`, { status: "Rejected", reasons: motivosSel, notes: note });
            return done("Candidato não aderente — prospecção encerrada.");
          }
          const proposta = $("mpResultadoProposta")?.value;
          if (proposta === "Aceite") {
            await api.post(`/api/applications/${p.id}/resolve`, { status: "Hired", notes: note });
            return done("Proposta aceita — vaga encerrada automaticamente.");
          }
          if (proposta === "Recusa") {
            await api.post(`/api/applications/${p.id}/resolve`, { status: "Declined", notes: note });
            return done("Recusa registrada como declínio.");
          }
          if (proxima) {
            await api.post(`/api/applications/${p.id}/move`, {
              targetStage: proxima, notes: note,
              offeredSalary: salary("mpRemOferecida"), requestedSalary: salary("mpRemPretendida"),
            });
            return done(`Movido para ${proxima}.`);
          }
          if (note) await api.post(`/api/applications/${p.id}/move`, { targetStage: p.stage, notes: note });
          return done("Atualizado.");
        });
      });

      const btnVoltar = $("mpVoltar");
      if (btnVoltar) btnVoltar.addEventListener("click", () => guard(async () => {
        await saveProcessData();
        await api.post(`/api/applications/${p.id}/move`, { targetStage: STAGES[idx - 1], notes: $("mpMoveNote").value.trim() || null });
        await done("Movido de volta para " + STAGES[idx - 1] + ".");
      }));

      $("mpEncerrar").addEventListener("click", () => guard(async () => {
        const reasons = [...motivosSel];
        const note = $("mpMoveNote").value.trim() || null;
        await saveProcessData();
        if (!(await confirmDialog("Encerrar esta prospecção sem sucesso? Use isto quando houver um motivo formal de reprovação.", { confirmLabel: "Encerrar", danger: true }))) return openApplicationModal(p.id);
        await api.post(`/api/applications/${p.id}/resolve`, { status: "Rejected", reasons, notes: note });
        await done("Prospecção encerrada.");
      }));

      $("mpDeclinio").addEventListener("click", () => guard(async () => {
        const note = $("mpMoveNote").value.trim() || null;
        await saveProcessData();
        if (!(await confirmDialog("Marcar como declínio? Use isto quando o candidato parou de responder ou desistiu por conta própria, sem uma reprovação formal.", { confirmLabel: "Marcar declínio", danger: true }))) return openApplicationModal(p.id);
        await api.post(`/api/applications/${p.id}/resolve`, { status: "Declined", notes: note });
        await done("Prospecção marcada como declínio.");
      }));
    },
  });
}

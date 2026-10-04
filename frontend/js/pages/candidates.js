import { api } from "../api.js";
import { $, qs, qsa, escapeHtml, toast, guard, debounce } from "../utils/dom.js";
import { confirmDialog } from "../utils/modal.js";
import { chipPicker, wireChipPicker, entityChipPicker, wireEntityChipPicker } from "../utils/chips.js";
import { normalizeLinkedin, normalizePhone, fmtDateBR, STATUS_LABELS } from "../utils/formatters.js";
import { state, optionValues } from "../state.js";
import { openNewApplicationModal } from "./pipeline.js";

const PAGE_SIZE = 25;
const local = { editingId: null, editing: null, search: "", uf: "", company: "", page: 1 };

/** Mesmos filtros da listagem, reaproveitados pela exportação. */
export function candidateFilterParams() {
  return { search: local.search, state: local.uf, company: local.company };
}

export async function renderCandidates() {
  const el = $("view-candidatos");
  // Os campos de filtro são recriados a cada busca; guarda o foco e o cursor para não perder a digitação.
  const active = document.activeElement;
  const activeId = active && ["candSearch", "candUf", "candCompany"].includes(active.id) ? active.id : null;
  const caret = activeId ? active.selectionStart : null;

  const list = await api.get("/api/candidates", { ...candidateFilterParams(), page: local.page, pageSize: PAGE_SIZE });
  const editing = local.editing;
  const selDiversidade = editing ? [...editing.diversityTags] : [];
  const selRestritivos = editing ? [...editing.restrictions] : [];
  const selOffLimits = editing ? editing.offLimits.map((o) => o.id) : [];
  const pages = Math.max(1, Math.ceil(list.total / PAGE_SIZE));

  el.innerHTML = `
    <div class="card">
      <div class="card-head"><h2>${editing ? "Editar candidato" : "Novo candidato"}</h2>
        ${editing ? `<button class="btn btn-ghost btn-sm" id="btnCancelEditCand">Cancelar edição</button>` : ""}
      </div>
      <form id="formCandidato" class="grid">
        <div><label>Nome</label><input id="fcNome" required value="${escapeHtml(editing?.name || "")}"></div>
        <div><label>Cargo atual</label><input id="fcCargo" value="${escapeHtml(editing?.currentPosition || "")}"></div>
        <div><label>Empresa atual</label><input id="fcEmpresaAtual" value="${escapeHtml(editing?.currentCompany || "")}"></div>
        <div><label>Remuneração atual (R$)</label><input id="fcRemuneracao" type="number" min="0" step="100" value="${editing?.salary ?? ""}"></div>
        <div><label>Cidade</label><input id="fcCidade" value="${escapeHtml(editing?.city || "")}"></div>
        <div><label>Estado</label><input id="fcEstado" maxlength="2" placeholder="Ex: RS" value="${escapeHtml(editing?.state || "")}"></div>
        <div><label>LinkedIn</label><input id="fcLinkedin" value="${escapeHtml(editing?.linkedIn || "")}"><div class="field-note" id="fcLinkedinNote"></div></div>
        <div><label>Telefone</label><input id="fcTelefone" value="${escapeHtml(editing?.phoneOriginal || editing?.phone || "")}"><div class="field-note" id="fcTelefoneNote"></div></div>
        <div class="full">
          <label>Diversidade <span class="muted" style="font-weight:400;">(dado sensível — não aparece em listagens)</span></label>
          ${chipPicker("fcDiversidade", optionValues("Diversity"), selDiversidade, { category: "Diversity", addLabel: "nova categoria" })}
        </div>
        <div class="full">
          <label>Pontos restritivos para evolução do candidato</label>
          ${chipPicker("fcRestritivos", optionValues("Restriction"), selRestritivos, { category: "Restriction", addLabel: "novo ponto" })}
        </div>
        <div class="full">
          <label>Empresas off-limits <span class="muted" style="font-weight:400;">(conflito de interesse — não indicar para vagas destas empresas)</span></label>
          ${entityChipPicker("fcOffLimits", state.companies, selOffLimits)}
        </div>
        <div class="full"><label>Observações</label><textarea id="fcObs">${escapeHtml(editing?.notes || "")}</textarea></div>
        ${editing && editing.applications.length ? `<div class="full"><label>Processos</label>
          <div style="display:flex;gap:6px;flex-wrap:wrap;">${editing.applications.map((a) => `<span class="tag ${a.status === "Active" ? "info" : "neutral"}" title="${escapeHtml(a.positionName + " · " + a.companyName)}">${escapeHtml(a.jobCode)} · ${escapeHtml(a.status === "Active" ? a.stage : STATUS_LABELS[a.status])}</span>`).join("")}</div></div>` : ""}
        <div class="full actions-row" style="margin-top:0;">
          <button class="btn btn-primary" type="submit">${editing ? "Atualizar candidato" : "Salvar candidato"}</button>
        </div>
      </form>
    </div>

    <div class="card">
      <div class="toolbar">
        <h2 style="margin:0;">Candidatos <span class="muted">(${list.total})</span></h2>
        <div class="toolbar-left">
          <input class="search-input" id="candSearch" placeholder="Buscar por nome, empresa, cidade…" value="${escapeHtml(local.search)}">
          <input id="candUf" maxlength="2" placeholder="UF" style="min-width:60px;width:60px;" value="${escapeHtml(local.uf)}">
          <input id="candCompany" placeholder="Empresa atual" value="${escapeHtml(local.company)}">
        </div>
      </div>
      <div class="table-wrap">
        <table>
          <thead><tr><th>Profissional</th><th>Empresa / Cargo</th><th>Cidade/UF</th><th>Off-limits</th><th>Contato</th><th></th></tr></thead>
          <tbody>
            ${list.items.length ? list.items.map((c) => `
              <tr>
                <td style="font-weight:600;">${escapeHtml(c.name)}${c.activeApplications ? `<div class="muted" style="font-size:11px;font-weight:400;">${c.activeApplications} processo(s) ativo(s)</div>` : ""}</td>
                <td>${escapeHtml(c.currentCompany || "—")}<div class="muted" style="font-size:11.5px;">${escapeHtml(c.currentPosition || "")}</div></td>
                <td>${escapeHtml(c.city || "—")}${c.state ? "/" + escapeHtml(c.state) : ""}</td>
                <td>${c.offLimits.map((o) => `<span class="tag danger">⛔ ${escapeHtml(o.name)}</span>`).join(" ") || "—"}</td>
                <td>${c.linkedIn ? `<a class="mono" href="https://${escapeHtml(c.linkedIn)}" target="_blank" rel="noopener" style="font-size:11.5px;">LinkedIn</a><br>` : ""}<span class="muted" style="font-size:11.5px;">${escapeHtml(c.phone || "")}</span></td>
                <td class="row-actions">
                  <button class="btn-ghost btn-sm" data-nova-prospeccao="${c.id}">+ Prospecção</button>
                  <button class="btn-ghost btn-sm" data-edit-cand="${c.id}">Editar</button>
                  <button class="btn-danger" data-del-cand="${c.id}">Excluir</button>
                </td>
              </tr>`).join("") : `<tr><td colspan="6"><div class="empty-state">Nenhum candidato encontrado.</div></td></tr>`}
          </tbody>
        </table>
      </div>
      ${pages > 1 ? `<div class="pager">
        <button class="btn btn-ghost btn-sm" id="candPrev" ${local.page <= 1 ? "disabled" : ""}>‹ Anterior</button>
        <span>Página ${local.page} de ${pages}</span>
        <button class="btn btn-ghost btn-sm" id="candNext" ${local.page >= pages ? "disabled" : ""}>Próxima ›</button>
      </div>` : ""}
    </div>`;

  const liveNote = (inputId, noteId, fn) => {
    const input = $(inputId);
    const update = () => { $(noteId).textContent = fn(input.value); };
    input.addEventListener("input", update);
    if (input.value) update();
  };
  liveNote("fcLinkedin", "fcLinkedinNote", (v) => (normalizeLinkedin(v) ? "Será salvo como: " + normalizeLinkedin(v) : ""));
  liveNote("fcTelefone", "fcTelefoneNote", (v) => normalizePhone(v).label);

  const rerender = () => renderCandidates();
  wireChipPicker("fcDiversidade", selDiversidade, { category: "Diversity", onAdded: rerender });
  wireChipPicker("fcRestritivos", selRestritivos, { category: "Restriction", onAdded: rerender });
  wireEntityChipPicker("fcOffLimits", selOffLimits);

  if (editing) qs("#btnCancelEditCand").addEventListener("click", () => { local.editingId = null; local.editing = null; renderCandidates(); });

  qs("#formCandidato").addEventListener("submit", (e) => {
    e.preventDefault();
    guard(async () => {
      const body = {
        name: $("fcNome").value.trim(),
        currentPosition: $("fcCargo").value.trim(),
        currentCompany: $("fcEmpresaAtual").value.trim(),
        salary: $("fcRemuneracao").value ? Number($("fcRemuneracao").value) : null,
        city: $("fcCidade").value.trim(),
        state: $("fcEstado").value.trim().toUpperCase(),
        linkedIn: $("fcLinkedin").value.trim(),
        phone: $("fcTelefone").value.trim(),
        diversityTags: selDiversidade,
        restrictions: selRestritivos,
        offLimitCompanyIds: selOffLimits,
        notes: $("fcObs").value.trim(),
      };
      if (editing) {
        await api.put(`/api/candidates/${editing.id}`, body);
        toast("Candidato atualizado.");
      } else {
        await api.post("/api/candidates", body);
        toast("Candidato salvo.");
      }
      local.editingId = null;
      local.editing = null;
      await renderCandidates();
    });
  });

  qsa("[data-edit-cand]").forEach((b) => b.addEventListener("click", () => guard(async () => {
    local.editing = await api.get(`/api/candidates/${b.dataset.editCand}`);
    local.editingId = local.editing.id;
    await renderCandidates();
    window.scrollTo({ top: 0, behavior: "smooth" });
  })));
  qsa("[data-del-cand]").forEach((b) => b.addEventListener("click", async () => {
    if (!(await confirmDialog("Excluir este candidato? Ele deixa de aparecer nas listagens, mas o histórico de prospecções é preservado.", { confirmLabel: "Excluir", danger: true }))) return;
    await guard(async () => {
      await api.del(`/api/candidates/${b.dataset.delCand}`);
      toast("Candidato excluído.");
      await renderCandidates();
    });
  }));
  qsa("[data-nova-prospeccao]").forEach((b) => b.addEventListener("click", () => {
    const c = list.items.find((x) => x.id === b.dataset.novaProspeccao);
    openNewApplicationModal(c);
  }));

  const onFilter = debounce(() => {
    local.search = $("candSearch").value;
    local.uf = $("candUf").value.trim().toUpperCase();
    local.company = $("candCompany").value;
    local.page = 1;
    guard(renderCandidates);
  }, 250);
  ["candSearch", "candUf", "candCompany"].forEach((id) => $(id).addEventListener("input", onFilter));
  if ($("candPrev")) $("candPrev").addEventListener("click", () => { local.page--; guard(renderCandidates); });
  if ($("candNext")) $("candNext").addEventListener("click", () => { local.page++; guard(renderCandidates); });

  if (activeId) {
    const input = $(activeId);
    input.focus();
    if (caret !== null) input.setSelectionRange(caret, caret);
  }
}

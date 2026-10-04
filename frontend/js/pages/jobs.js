import { api } from "../api.js";
import { $, qs, qsa, escapeHtml, toast, guard, debounce, options } from "../utils/dom.js";
import { confirmDialog } from "../utils/modal.js";
import { fmtDateBR, todayISO } from "../utils/formatters.js";
import { state, loadRefs, MODALIDADES } from "../state.js";
import { renderCurrentView, showJobPipeline } from "../router.js";

function filteredJobs() {
  const f = state.filter;
  return state.jobs.filter((j) => {
    if (f.jobId) return j.id === f.jobId;
    if (f.positionId) return j.positionId === f.positionId;
    if (f.companyId) return j.companyId === f.companyId;
    return true;
  });
}

export async function renderJobs() {
  const el = $("view-vagas");
  const jobs = filteredJobs();

  el.innerHTML = `
    <div class="card">
      <div class="card-head"><h2>Nova posição &amp; abertura de vaga</h2>
        <span class="hint">O código da vaga é gerado automaticamente</span>
      </div>
      <form id="formVaga" class="grid">
        <div>
          <label>Empresa</label>
          <select id="fvEmpresa" required>${options(state.companies, state.filter.companyId, { empty: "Selecione…" })}</select>
        </div>
        <div>
          <label>Posição existente (opcional)</label>
          <select id="fvPosicaoExistente"><option value="">— Nova posição —</option></select>
        </div>
        <div>
          <label>Nome da posição</label>
          <input id="fvPosicaoNome" placeholder="Ex: HR Business Partner" required>
        </div>
        <div>
          <label>Nível <span class="muted" style="font-weight:400;">(opcional — ex.: Pleno, Sênior)</span></label>
          <input id="fvNivel" placeholder="Use quando um processo tiver vários níveis">
        </div>
        <div class="full">
          <label>Requisitos da vaga</label>
          <textarea id="fvRequisitos" placeholder="O que o profissional precisa ter para estar aderente à posição"></textarea>
        </div>
        <div>
          <label>Modalidade</label>
          <select id="fvModalidade">${MODALIDADES.map((m) => `<option>${m}</option>`).join("")}</select>
        </div>
        <div>
          <label>Disponibilidade para viagens</label>
          <select id="fvViagem"><option value="nao">Não</option><option value="sim">Sim</option></select>
        </div>
        <div>
          <label>Data de abertura</label>
          <input id="fvDataAbertura" type="date" value="${todayISO()}" required>
        </div>
        <div>
          <label>Responsável</label>
          <input value="${escapeHtml(state.me.name)}" disabled>
        </div>
        <div class="full">
          <div class="field-note">Código previsto: <b class="mono" id="fvCodigoPreview">—</b></div>
        </div>
        <div class="full actions-row" style="margin-top:0;">
          <button class="btn btn-primary" type="submit">Abrir vaga</button>
        </div>
      </form>
    </div>

    <div class="card">
      <div class="toolbar">
        <h2 style="margin:0;">Vagas <span class="muted">(${jobs.length})</span></h2>
      </div>
      <div class="table-wrap">
        <table>
          <thead><tr><th>Código</th><th>Posição / Empresa</th><th>Aberta em</th><th>Modalidade</th><th>Status</th><th>Dias</th><th>Candidatos</th><th>Responsável</th><th></th></tr></thead>
          <tbody>
            ${jobs.length ? jobs.map((v) => `<tr>
                <td class="mono">${escapeHtml(v.code)}${v.reopenedFromCode ? `<div class="muted" style="font-size:10.5px;">reaberta de ${escapeHtml(v.reopenedFromCode)}</div>` : ""}</td>
                <td><div style="font-weight:600;">${escapeHtml(v.positionName)}${v.level ? ` <span class="tag neutral">${escapeHtml(v.level)}</span>` : ""}</div><div class="muted" style="font-size:11.5px;">${escapeHtml(v.companyName)}</div></td>
                <td>${fmtDateBR(v.openedAt)}</td>
                <td>${escapeHtml(v.modality || "—")}</td>
                <td>${v.status === "Closed" ? '<span class="tag success">Fechada</span>' : '<span class="tag info">Aberta</span>'}</td>
                <td class="mono">${v.daysOpen}</td>
                <td>${v.applicationsCount}</td>
                <td>${escapeHtml(v.ownerName || "—")}</td>
                <td class="row-actions">
                  <button class="btn-ghost btn-sm" data-ver-pipeline="${v.id}">Pipeline</button>
                  ${v.status !== "Closed" ? `<button class="btn-ghost btn-sm" data-fechar-vaga="${v.id}">Fechar</button>` : `<button class="btn-ghost btn-sm" data-reabrir-vaga="${v.id}">Reabrir</button>`}
                </td>
              </tr>`).join("") : `<tr><td colspan="9"><div class="empty-state">Nenhuma vaga aberta ainda.</div></td></tr>`}
          </tbody>
        </table>
      </div>
    </div>`;

  const selEmpresa = $("fvEmpresa");
  const selPosicao = $("fvPosicaoExistente");
  const nomeInput = $("fvPosicaoNome");
  const reqInput = $("fvRequisitos");
  const modInput = $("fvModalidade");
  const viagemInput = $("fvViagem");
  const dataInput = $("fvDataAbertura");
  const codigoPreview = $("fvCodigoPreview");

  function refreshPosicaoOptions() {
    const list = state.positions.filter((p) => p.companyId === selEmpresa.value);
    selPosicao.innerHTML = '<option value="">— Nova posição —</option>' +
      list.map((p) => `<option value="${p.id}">${escapeHtml(p.name)}</option>`).join("");
  }
  const refreshCodigoPreview = debounce(() => guard(async () => {
    if (!selEmpresa.value || !nomeInput.value.trim() || !dataInput.value) { codigoPreview.textContent = "—"; return; }
    const params = selPosicao.value
      ? { positionId: selPosicao.value, openedAt: dataInput.value }
      : { companyId: selEmpresa.value, positionName: nomeInput.value.trim(), openedAt: dataInput.value };
    codigoPreview.textContent = (await api.get("/api/jobs/code-preview", params)).code;
  }), 250);

  selEmpresa.addEventListener("change", () => { refreshPosicaoOptions(); refreshCodigoPreview(); });
  selPosicao.addEventListener("change", () => {
    const p = state.positions.find((x) => x.id === selPosicao.value);
    nomeInput.disabled = !!p;
    if (p) { nomeInput.value = p.name; reqInput.value = p.requirements || ""; modInput.value = p.modality || MODALIDADES[0]; viagemInput.value = p.travelAvailability ? "sim" : "nao"; }
    refreshCodigoPreview();
  });
  [nomeInput, dataInput].forEach((i) => i.addEventListener("input", refreshCodigoPreview));
  refreshPosicaoOptions();

  qs("#formVaga").addEventListener("submit", (e) => {
    e.preventDefault();
    guard(async () => {
      if (!selEmpresa.value) { toast("Selecione a empresa.", true); return; }
      const common = {
        requirements: reqInput.value.trim(),
        modality: modInput.value,
        travelAvailability: viagemInput.value === "sim",
        openedAt: dataInput.value,
        level: $("fvNivel").value.trim() || null,
      };
      const body = selPosicao.value
        ? { positionId: selPosicao.value, ...common }
        : { companyId: selEmpresa.value, positionName: nomeInput.value.trim(), ...common };
      const job = await api.post("/api/jobs", body);
      toast("Vaga aberta: " + job.code);
      await loadRefs();
      await renderCurrentView();
    });
  });

  qsa("[data-fechar-vaga]").forEach((b) => b.addEventListener("click", async () => {
    if (!(await confirmDialog("Fechar esta vaga sem contratação? As prospecções ativas continuam registradas.", { confirmLabel: "Fechar vaga" }))) return;
    await guard(async () => {
      await api.post(`/api/jobs/${b.dataset.fecharVaga}/close`);
      toast("Vaga encerrada.");
      await loadRefs();
      await renderCurrentView();
    });
  }));
  qsa("[data-reabrir-vaga]").forEach((b) => b.addEventListener("click", () => guard(async () => {
    const job = await api.post(`/api/jobs/${b.dataset.reabrirVaga}/reopen`);
    toast("Vaga reaberta com novo código: " + job.code);
    await loadRefs();
    await renderCurrentView();
  })));
  qsa("[data-ver-pipeline]").forEach((b) => b.addEventListener("click", () => showJobPipeline(b.dataset.verPipeline)));
}

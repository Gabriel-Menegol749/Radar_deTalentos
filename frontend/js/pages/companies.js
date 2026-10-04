import { api } from "../api.js";
import { $, qs, qsa, escapeHtml, toast, guard } from "../utils/dom.js";
import { confirmDialog } from "../utils/modal.js";
import { state, loadRefs } from "../state.js";
import { renderCurrentView } from "../router.js";

export async function renderCompanies() {
  const el = $("view-empresas");
  el.innerHTML = `
    <div class="card">
      <div class="card-head"><h2>Nova empresa</h2></div>
      <form id="formEmpresa" class="grid">
        <div class="full">
          <label>Nome da empresa</label>
          <input id="feNome" placeholder="Ex: Sicredi" required>
        </div>
        <div class="full actions-row" style="margin-top:0;">
          <button class="btn btn-primary" type="submit">Salvar empresa</button>
        </div>
      </form>
    </div>
    <div class="card">
      <div class="card-head">
        <h2>Empresas cadastradas <span class="muted">(${state.companies.length})</span></h2>
      </div>
      <div class="table-wrap">
        <table>
          <thead><tr><th>Empresa</th><th>Vagas</th><th>Candidatos acessados</th><th></th></tr></thead>
          <tbody>
            ${state.companies.length ? state.companies.map((e) => `<tr>
                <td style="font-weight:600;">${escapeHtml(e.name)}</td>
                <td>${e.jobsCount}</td>
                <td>${e.applicationsCount}</td>
                <td class="row-actions"><button class="btn-danger" data-del-empresa="${e.id}">Excluir</button></td>
              </tr>`).join("") : `<tr><td colspan="4"><div class="empty-state">Nenhuma empresa cadastrada ainda.</div></td></tr>`}
          </tbody>
        </table>
      </div>
    </div>`;

  qs("#formEmpresa").addEventListener("submit", (e) => {
    e.preventDefault();
    guard(async () => {
      const name = $("feNome").value.trim();
      if (!name) return;
      await api.post("/api/companies", { name });
      toast("Empresa cadastrada.");
      await loadRefs();
      await renderCurrentView();
    });
  });
  qsa("[data-del-empresa]").forEach((b) => b.addEventListener("click", async () => {
    const company = state.companies.find((c) => c.id === b.dataset.delEmpresa);
    const msg = company?.jobsCount
      ? "Esta empresa já tem vagas vinculadas. Ela será inativada: some das listas, mas vagas e histórico são preservados. Continuar?"
      : "Excluir esta empresa? Ela será inativada e deixará de aparecer nas listas.";
    if (!(await confirmDialog(msg, { confirmLabel: "Excluir", danger: true }))) return;
    await guard(async () => {
      await api.del(`/api/companies/${b.dataset.delEmpresa}`);
      toast("Empresa excluída.");
      await loadRefs();
      await renderCurrentView();
    });
  }));
}

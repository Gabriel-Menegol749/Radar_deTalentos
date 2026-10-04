import { $, qsa, escapeHtml, guard } from "./utils/dom.js";
import { state } from "./state.js";
import { renderDashboard } from "./pages/dashboard.js";
import { renderPipeline } from "./pages/pipeline.js";
import { renderCandidates } from "./pages/candidates.js";
import { renderJobs } from "./pages/jobs.js";
import { renderCompanies } from "./pages/companies.js";
import { renderImport } from "./pages/import.js";
import { renderAdmin } from "./pages/admin.js";

export const VIEW_META = {
  dashboard: { title: "Painel geral", sub: "Visão consolidada de todas as vagas e empresas", render: renderDashboard },
  pipeline: { title: "Pipeline de prospecção", sub: "Acompanhe cada candidato pelas etapas do processo", render: renderPipeline },
  candidatos: { title: "Candidatos", sub: "Base de profissionais já acionados", render: renderCandidates },
  vagas: { title: "Vagas & posições", sub: "Cadastro de posições e controle de aberturas", render: renderJobs },
  empresas: { title: "Empresas", sub: "Clientes atendidos pela consultoria", render: renderCompanies },
  importar: { title: "Importar planilha", sub: "Traga uma base existente (ex: planilha de prospecção) para o CRM", render: renderImport },
  admin: { title: "Administração", sub: "Usuários e vocabulários do sistema", render: renderAdmin },
};

// Telas onde o filtro global Empresa → Posição → Vaga se aplica.
const SCOPED_VIEWS = ["dashboard", "pipeline", "vagas"];

export function setView(view) {
  if (!VIEW_META[view]) view = "dashboard";
  state.view = view;
  qsa(".navlink").forEach((b) => b.classList.toggle("active", b.dataset.view === view));
  Object.keys(VIEW_META).forEach((v) => { $("view-" + v).hidden = v !== view; });
  $("topbarTitle").textContent = VIEW_META[view].title;
  $("topbarSub").textContent = VIEW_META[view].sub;
  renderCurrentView();
}

export function renderCurrentView() {
  populateGlobalFilters();
  const scoped = SCOPED_VIEWS.includes(state.view);
  $("topbarFilters").hidden = !scoped;
  $("btnExportExcel").hidden = !["dashboard", "pipeline", "vagas", "candidatos"].includes(state.view);
  return guard(() => VIEW_META[state.view].render());
}

/** Filtros cumulativos Empresa → Posição → Vaga (seção 5.2). */
export function populateGlobalFilters() {
  const f = state.filter;
  if (f.companyId && !state.companies.some((c) => c.id === f.companyId)) f.companyId = "";
  const positions = state.positions.filter((p) => !f.companyId || p.companyId === f.companyId);
  if (f.positionId && !positions.some((p) => p.id === f.positionId)) f.positionId = "";
  const jobs = state.jobs.filter((j) => (!f.companyId || j.companyId === f.companyId) && (!f.positionId || j.positionId === f.positionId));
  if (f.jobId && !jobs.some((j) => j.id === f.jobId)) f.jobId = "";

  $("filterEmpresaGlobal").innerHTML = '<option value="">Todas as empresas</option>' +
    state.companies.map((c) => `<option value="${c.id}" ${c.id === f.companyId ? "selected" : ""}>${escapeHtml(c.name)}</option>`).join("");
  $("filterPosicaoGlobal").innerHTML = '<option value="">Todas as posições</option>' +
    positions.map((p) => `<option value="${p.id}" ${p.id === f.positionId ? "selected" : ""}>${escapeHtml(p.name)}${f.companyId ? "" : " · " + escapeHtml(p.companyName)}</option>`).join("");
  $("filterVagaGlobal").innerHTML = '<option value="">Todas as vagas</option>' +
    jobs.map((j) => `<option value="${j.id}" ${j.id === f.jobId ? "selected" : ""}>${escapeHtml(j.code)}${j.level ? " — " + escapeHtml(j.level) : ""}</option>`).join("");
}

export function bindGlobalFilters() {
  $("filterEmpresaGlobal").addEventListener("change", (e) => {
    state.filter = { companyId: e.target.value, positionId: "", jobId: "" };
    renderCurrentView();
  });
  $("filterPosicaoGlobal").addEventListener("change", (e) => {
    state.filter.positionId = e.target.value;
    state.filter.jobId = "";
    renderCurrentView();
  });
  $("filterVagaGlobal").addEventListener("change", (e) => {
    state.filter.jobId = e.target.value;
    renderCurrentView();
  });
}

export function showJobPipeline(jobId) {
  const job = state.jobs.find((j) => j.id === jobId);
  state.filter = { companyId: job?.companyId || "", positionId: job?.positionId || "", jobId };
  setView("pipeline");
}

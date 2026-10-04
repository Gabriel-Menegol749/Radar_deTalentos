import { api } from "./api.js";

export const STAGES = ["Acessado", "Entrevista", "Aprovação", "Etapa interna", "Case", "Proposta"];
export const MODALIDADES = ["Presencial", "Home office", "Híbrida"];

/** Dados de referência (pequenos) mantidos em memória e recarregados após alterações. */
export const state = {
  me: null,
  companies: [],
  positions: [],
  jobs: [],
  options: { Diversity: [], Restriction: [], Source: [], RejectionReason: [] },
  filter: { companyId: "", positionId: "", jobId: "" },
  view: "dashboard",
  presentMode: false,
};

export async function loadRefs() {
  const [companies, positions, jobs, options] = await Promise.all([
    api.get("/api/companies"),
    api.get("/api/positions"),
    api.get("/api/jobs"),
    api.get("/api/options"),
  ]);
  state.companies = companies;
  state.positions = positions;
  state.jobs = jobs;
  const grouped = { Diversity: [], Restriction: [], Source: [], RejectionReason: [] };
  options.forEach((o) => (grouped[o.category] = grouped[o.category] || []).push(o));
  state.options = grouped;
}

export const optionValues = (category) => (state.options[category] || []).map((o) => o.value);

/** Recorte Empresa → Posição → Vaga enviado para pipeline, métricas e exportação. */
export function scope() {
  return { companyId: state.filter.companyId, positionId: state.filter.positionId, jobId: state.filter.jobId };
}

export const jobById = (id) => state.jobs.find((j) => j.id === id);
export const companyName = (id) => state.companies.find((c) => c.id === id)?.name || "—";
export const jobLabel = (j) => (j ? `${j.positionName}${j.level ? " (" + j.level + ")" : ""} · ${j.companyName}` : "—");

import { api } from "../api.js";
import { $, escapeHtml } from "../utils/dom.js";
import { scope } from "../state.js";

export async function renderDashboard() {
  const el = $("view-dashboard");
  const m = await api.get("/api/metrics", scope());

  const maxFunil = Math.max(1, m.funnel[0]?.count || 1);
  const maxFonte = Math.max(1, ...m.sources.map((x) => x.total));
  const maxMotivo = Math.max(1, ...m.rejectionReasons.map((x) => x.count));
  const rem = m.offerVsRequest;
  const remCategorias = { "Oferta abaixo da pretensão": rem.below, "Oferta compatível": rem.compatible, "Oferta acima da pretensão": rem.above };
  const maxRem = Math.max(1, ...Object.values(remCategorias));
  const pct = (r) => (r.rate ?? "—") + (r.rate !== null ? '<span class="unit">%</span>' : "");

  el.innerHTML = `
    <div class="kpi-grid">
      <div class="kpi"><div class="eyebrow">Candidatos acessados</div><div class="val">${m.totalAccessed}</div></div>
      <div class="kpi"><div class="eyebrow">Entrevistados</div><div class="val">${m.interviewed}</div></div>
      <div class="kpi"><div class="eyebrow">Vagas abertas</div><div class="val">${m.openJobs}</div></div>
      <div class="kpi"><div class="eyebrow">Tempo médio de fechamento</div><div class="val">${m.avgCloseTimeDays ?? "—"}${m.avgCloseTimeDays !== null ? '<span class="unit">dias</span>' : ""}</div></div>
    </div>
    <div class="kpi-grid" style="margin-top:14px;">
      <div class="kpi"><div class="eyebrow">Taxa de aceite de proposta</div><div class="val">${pct(m.acceptance)}</div><div class="delta">${m.acceptance.total ? m.acceptance.count + " de " + m.acceptance.total + " propostas" : "sem propostas respondidas ainda"}</div></div>
      <div class="kpi"><div class="eyebrow">Taxa de declínio</div><div class="val">${pct(m.decline)}</div><div class="delta">${m.decline.count} candidato(s) declinaram</div></div>
    </div>

    <div class="card">
      <div class="card-head"><h2>Funil de conversão</h2><span class="hint">candidatos que chegaram a cada etapa, e % que veio da etapa anterior</span></div>
      <div class="funnel">
        ${m.funnel.map((f) => `<div class="fun-row">
          <div class="fun-label">${escapeHtml(f.stage)}${f.conversion !== null ? `<div class="muted" style="font-size:10.5px;">${f.conversion}% da anterior</div>` : ""}</div>
          <div class="fun-track"><div class="fun-fill" style="width:${(f.count / maxFunil) * 100}%"></div></div>
          <div class="fun-val mono">${f.count}</div>
        </div>`).join("")}
      </div>
    </div>

    <div class="card">
      <div class="card-head"><h2>Efetividade por fonte</h2><span class="hint">candidatos acionados e % que viraram contratação (proposta aceita)</span></div>
      ${m.sources.length ? `<div class="barlist">
        ${m.sources.map((x) => `<div class="bar-row">
          <div>${escapeHtml(x.source)}</div>
          <div class="bar-track"><div class="bar-fill" style="width:${(x.total / maxFonte) * 100}%;background:var(--accent);"></div></div>
          <div class="mono">${x.total}</div>
        </div><div class="bar-subrow"><div></div><div>${x.pct}% viraram contratação (${x.hired}/${x.total})</div><div></div></div>`).join("")}
      </div>` : `<div class="empty-state">Registre a fonte do contato nas prospecções para ver esta métrica.</div>`}
    </div>

    <div class="card">
      <div class="card-head"><h2>Motivos de reprovação</h2><span class="hint">comparação com o mercado, na etapa de aprovação</span></div>
      ${m.rejectionReasons.some((x) => x.count) ? `<div class="barlist">
        ${m.rejectionReasons.map((x) => `<div class="bar-row">
          <div>${escapeHtml(x.reason)}</div>
          <div class="bar-track"><div class="bar-fill" style="width:${(x.count / maxMotivo) * 100}%;background:var(--accent-2);"></div></div>
          <div class="mono">${x.count}</div>
        </div>`).join("")}
      </div>` : `<div class="empty-state">Ainda sem reprovações registradas neste filtro.</div>`}
    </div>

    <div class="card">
      <div class="card-head"><h2>Oferta da vaga × pretensão do candidato</h2><span class="hint">calculado a partir dos valores informados na Aprovação</span></div>
      ${rem.total ? `<div class="barlist">
        ${Object.entries(remCategorias).map(([k, n]) => `<div class="bar-row">
          <div>${k}</div>
          <div class="bar-track"><div class="bar-fill" style="width:${(n / maxRem) * 100}%;background:var(--info);"></div></div>
          <div class="mono">${n}</div>
        </div>`).join("")}
      </div>` : `<div class="empty-state">Registre remuneração oferecida e pretendida na etapa de Aprovação para ver esta métrica.</div>`}
    </div>`;
}

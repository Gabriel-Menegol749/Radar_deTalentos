import { api } from "../api.js";
import { $, qs, qsa, escapeHtml, toast, guard } from "../utils/dom.js";
import { openModal, confirmDialog } from "../utils/modal.js";
import { isValidEmail } from "../utils/formatters.js";
import { state, loadRefs } from "../state.js";

const CATEGORIES = [
  { key: "Source", label: "Fontes de contato" },
  { key: "RejectionReason", label: "Motivos de reprovação" },
  { key: "Restriction", label: "Pontos restritivos" },
  { key: "Diversity", label: "Diversidade (dado sensível)" },
];

let activeTab = "usuarios";

export async function renderAdmin() {
  const el = $("view-admin");
  if (state.me.role !== "Admin") { el.innerHTML = `<div class="empty-state">Acesso restrito ao perfil Admin.</div>`; return; }

  el.innerHTML = `
    <div class="subtabs" role="tablist">
      <button class="subtab ${activeTab === "usuarios" ? "active" : ""}" data-tab="usuarios" role="tab">Usuários</button>
      <button class="subtab ${activeTab === "vocabularios" ? "active" : ""}" data-tab="vocabularios" role="tab">Vocabulários</button>
    </div>
    <div id="adminTabBody"></div>`;
  qsa("[data-tab]").forEach((b) => b.addEventListener("click", () => { activeTab = b.dataset.tab; guard(renderAdmin); }));

  if (activeTab === "usuarios") await renderUsers();
  else renderVocabularies();
}

/** Mostra o erro dentro do formulário (ex.: e-mail duplicado → 409) em vez de só um toast passageiro. */
function showFormError(id, message) {
  const box = $(id);
  box.textContent = message;
  box.hidden = !message;
}

async function renderUsers() {
  const users = await api.get("/api/users");
  $("adminTabBody").innerHTML = `
    <div class="card">
      <div class="card-head"><h2>Novo usuário</h2><span class="hint">Consultor visualiza e opera os dados; Admin também gerencia usuários e configurações</span></div>
      <form id="formUser" class="grid" novalidate>
        <div><label for="fuNome">Nome</label><input id="fuNome" required></div>
        <div><label for="fuEmail">E-mail</label><input id="fuEmail" type="email" required placeholder="nome@empresa.com"></div>
        <div><label for="fuSenha">Senha inicial</label><input id="fuSenha" type="password" minlength="8" required placeholder="Mínimo 8 caracteres"></div>
        <div><label for="fuRole">Perfil</label><select id="fuRole"><option>Consultor</option><option>Admin</option></select></div>
        <div class="full banner danger" id="fuError" hidden style="margin-bottom:0;"></div>
        <div class="full actions-row" style="margin-top:0;"><button class="btn btn-primary" type="submit">Criar usuário</button></div>
      </form>
    </div>
    <div class="card">
      <div class="card-head"><h2>Usuários <span class="muted">(${users.length})</span></h2></div>
      <div class="table-wrap">
        <table>
          <thead><tr><th>Nome</th><th>E-mail</th><th>Perfil</th><th>Situação</th><th></th></tr></thead>
          <tbody>
            ${users.map((u) => `<tr>
              <td style="font-weight:600;">${escapeHtml(u.name)}${u.id === state.me.id ? ' <span class="tag neutral">você</span>' : ""}</td>
              <td>${escapeHtml(u.email)}</td>
              <td>${escapeHtml(u.role)}</td>
              <td>${u.isActive ? '<span class="tag success">Ativo</span>' : '<span class="tag neutral">Inativo</span>'}</td>
              <td class="row-actions">${u.id === state.me.id ? "" : `<button class="btn btn-ghost btn-sm" data-edit-user="${u.id}">Editar</button>`}</td>
            </tr>`).join("")}
          </tbody>
        </table>
      </div>
    </div>`;

  qs("#formUser").addEventListener("submit", async (e) => {
    e.preventDefault();
    const name = $("fuNome").value.trim();
    const email = $("fuEmail").value.trim();
    const password = $("fuSenha").value;
    if (!name) return showFormError("fuError", "Informe o nome.");
    if (!isValidEmail(email)) return showFormError("fuError", "Informe um e-mail válido (ex.: nome@empresa.com).");
    if (password.length < 8) return showFormError("fuError", "A senha deve ter pelo menos 8 caracteres.");
    showFormError("fuError", "");
    try {
      await api.post("/api/users", { name, email, password, role: $("fuRole").value });
      toast("Usuário criado.");
      await renderUsers();
    } catch (ex) {
      const msg = ex.status === 409 ? `O e-mail ${email} já está cadastrado para outro usuário.` : ex.message;
      showFormError("fuError", msg);
      toast(msg, true);
    }
  });

  qsa("[data-edit-user]").forEach((b) => b.addEventListener("click", () => {
    const u = users.find((x) => x.id === b.dataset.editUser);
    openModal("Editar usuário", `
      <form id="formEditUser" class="grid">
        <div class="full"><label>Nome</label><input id="euNome" value="${escapeHtml(u.name)}" required></div>
        <div class="full"><label>E-mail</label><input value="${escapeHtml(u.email)}" disabled></div>
        <div><label>Perfil</label><select id="euRole"><option ${u.role === "Consultor" ? "selected" : ""}>Consultor</option><option ${u.role === "Admin" ? "selected" : ""}>Admin</option></select></div>
        <div><label>Situação</label><select id="euAtivo"><option value="1" ${u.isActive ? "selected" : ""}>Ativo</option><option value="0" ${u.isActive ? "" : "selected"}>Inativo</option></select></div>
        <div class="full"><label>Nova senha <span class="muted" style="font-weight:400;">(deixe em branco para manter — use para redefinir a senha do usuário)</span></label><input id="euSenha" type="password" minlength="8"></div>
        <div class="full actions-row" style="margin-top:0;"><button class="btn btn-primary" type="submit">Salvar</button></div>
      </form>`, {
      onMount: (close) => qs("#formEditUser").addEventListener("submit", (e) => {
        e.preventDefault();
        guard(async () => {
          await api.put(`/api/users/${u.id}`, { name: $("euNome").value.trim(), role: $("euRole").value, isActive: $("euAtivo").value === "1", password: $("euSenha").value || null });
          toast("Usuário atualizado.");
          close();
          await renderUsers();
        });
      }),
    });
  }));
}

function renderVocabularies() {
  $("adminTabBody").innerHTML = `
    <div class="card">
      <div class="card-head"><h2>Vocabulários</h2><span class="hint">opções usadas nos cadastros, no pipeline e nas métricas</span></div>
      <div class="stack" style="gap:18px;">
        ${CATEGORIES.map((c) => `<div>
          <label>${c.label}</label>
          <div class="chip-picker" style="margin-bottom:8px;">
            ${(state.options[c.key] || []).map((o) => `<span class="tag neutral">${escapeHtml(o.value)} <button title="Remover" data-del-option="${o.id}">✕</button></span>`).join("") || '<span class="muted" style="font-size:12px;">Nenhuma opção.</span>'}
          </div>
          <div style="display:flex;gap:6px;max-width:420px;">
            <input data-new-option="${c.key}" placeholder="Nova opção">
            <button class="btn btn-soft btn-sm" data-add-option="${c.key}">Adicionar</button>
          </div>
        </div>`).join("")}
      </div>
    </div>`;

  qsa("[data-add-option]").forEach((b) => b.addEventListener("click", () => guard(async () => {
    const input = qs(`[data-new-option="${b.dataset.addOption}"]`);
    if (!input.value.trim()) return;
    await api.post("/api/options", { category: b.dataset.addOption, value: input.value.trim() });
    await loadRefs();
    toast("Opção adicionada.");
    renderVocabularies();
  })));
  qsa("[data-new-option]").forEach((i) => i.addEventListener("keydown", (e) => {
    if (e.key === "Enter") { e.preventDefault(); qs(`[data-add-option="${i.dataset.newOption}"]`).click(); }
  }));
  qsa("[data-del-option]").forEach((b) => b.addEventListener("click", async () => {
    if (!(await confirmDialog("Remover esta opção? Registros que já a usam não são alterados.", { confirmLabel: "Remover", danger: true }))) return;
    await guard(async () => {
      await api.del(`/api/options/${b.dataset.delOption}`);
      await loadRefs();
      await renderAdmin();
    });
  }));
}

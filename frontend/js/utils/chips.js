import { $, escapeHtml, toast, guard } from "./dom.js";
import { api } from "../api.js";
import { state, loadRefs } from "../state.js";

/**
 * Seletor de chips do protótipo. `category` (opcional) habilita "+ nova opção",
 * que grava no vocabulário do sistema — somente Admin (D7).
 */
export function chipPicker(containerId, values, selected, { category, addLabel } = {}) {
  const canAdd = category && state.me?.role === "Admin";
  return `<div class="chip-picker" id="${containerId}">
    ${values.map((o) => `<span class="chip ${selected.includes(o) ? "on" : ""}" data-chip="${escapeHtml(o)}">${escapeHtml(o)}</span>`).join("")}
    ${canAdd ? `<span class="chip tag-add" id="${containerId}AddBtn">+ ${escapeHtml(addLabel || "nova opção")}</span>` : ""}
  </div>`;
}

/** Liga os cliques; `selected` é o array mutável de valores; `onAdded` re-renderiza após nova opção. */
export function wireChipPicker(containerId, selected, { category, onAdded } = {}) {
  const box = $(containerId);
  if (!box) return;
  box.querySelectorAll("[data-chip]").forEach((c) => c.addEventListener("click", () => {
    const val = c.dataset.chip;
    const idx = selected.indexOf(val);
    if (idx >= 0) selected.splice(idx, 1); else selected.push(val);
    c.classList.toggle("on");
  }));
  const addBtn = $(containerId + "AddBtn");
  if (!addBtn || !category) return;
  addBtn.addEventListener("click", () => {
    const wrap = document.createElement("span");
    wrap.style.display = "inline-flex";
    wrap.style.gap = "4px";
    wrap.innerHTML = `<input type="text" style="width:140px;padding:4px 8px;font-size:12px;" placeholder="Nova opção">
      <button type="button" class="btn btn-soft btn-sm" style="padding:4px 9px;">OK</button>`;
    addBtn.replaceWith(wrap);
    const input = wrap.querySelector("input");
    input.focus();
    const commit = () => guard(async () => {
      const v = input.value.trim();
      if (!v) return;
      await api.post("/api/options", { category, value: v });
      await loadRefs();
      selected.push(v);
      toast("Opção adicionada.");
      if (onAdded) onAdded();
    });
    wrap.querySelector("button").addEventListener("click", commit);
    input.addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); commit(); } });
  });
}

/** Chips por entidade (id ≠ rótulo) — usado para empresas off-limits. */
export function entityChipPicker(containerId, entities, selectedIds) {
  if (!entities.length) return `<div class="field-note">Cadastre uma empresa primeiro para poder marcá-la como off-limits.</div>`;
  return `<div class="chip-picker" id="${containerId}">
    ${entities.map((e) => `<span class="chip ${selectedIds.includes(e.id) ? "on" : ""}" data-chip-id="${e.id}">${escapeHtml(e.name)}</span>`).join("")}
  </div>`;
}

export function wireEntityChipPicker(containerId, selectedIds) {
  const box = $(containerId);
  if (!box) return;
  box.querySelectorAll("[data-chip-id]").forEach((c) => c.addEventListener("click", () => {
    const val = c.dataset.chipId;
    const idx = selectedIds.indexOf(val);
    if (idx >= 0) selectedIds.splice(idx, 1); else selectedIds.push(val);
    c.classList.toggle("on");
  }));
}

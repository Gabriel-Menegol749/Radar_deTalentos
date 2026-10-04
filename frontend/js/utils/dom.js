export const $ = (id) => document.getElementById(id);
export const qs = (sel, root) => (root || document).querySelector(sel);
export const qsa = (sel, root) => Array.from((root || document).querySelectorAll(sel));

export function escapeHtml(s) {
  return String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

export function debounce(fn, ms) {
  let t;
  return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), ms || 200); };
}

export function toast(msg, isError) {
  const t = $("toast");
  if (!t) return;
  t.textContent = msg;
  t.classList.toggle("error", !!isError);
  t.classList.add("show");
  clearTimeout(toast._h);
  toast._h = setTimeout(() => t.classList.remove("show"), isError ? 4200 : 2400);
}

/** Executa uma ação assíncrona mostrando o erro da API em toast, sem quebrar a tela. */
export async function guard(fn) {
  try {
    return await fn();
  } catch (e) {
    toast(e.message || "Erro inesperado.", true);
    return undefined;
  }
}

export function options(list, selected, { value = (x) => x.id, label = (x) => x.name, empty } = {}) {
  return (empty !== undefined ? `<option value="">${escapeHtml(empty)}</option>` : "") +
    list.map((x) => `<option value="${escapeHtml(value(x))}" ${String(value(x)) === String(selected ?? "") ? "selected" : ""}>${escapeHtml(label(x))}</option>`).join("");
}

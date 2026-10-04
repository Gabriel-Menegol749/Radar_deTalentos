import { $, escapeHtml } from "./dom.js";

/** Modal do protótipo: fecha no ✕, clique fora ou Esc. */
export function openModal(title, bodyHtml, opts) {
  opts = opts || {};
  const root = $("modalRoot");
  root.innerHTML = `
    <div class="modal-overlay" id="modalOverlay">
      <div class="modal" role="dialog" aria-modal="true" ${opts.wide ? 'style="max-width:860px;"' : ""}>
        <div class="modal-head">
          <h3>${escapeHtml(title)}</h3>
          <button class="modal-close" id="modalCloseBtn" aria-label="Fechar">✕</button>
        </div>
        <div id="modalBody">${bodyHtml}</div>
      </div>
    </div>`;
  const overlay = $("modalOverlay");
  const onKey = (e) => { if (e.key === "Escape") close(); };
  function close() {
    document.removeEventListener("keydown", onKey);
    root.innerHTML = "";
    if (opts.onClose) opts.onClose();
  }
  $("modalCloseBtn").addEventListener("click", close);
  overlay.addEventListener("click", (e) => { if (e.target === overlay) close(); });
  document.addEventListener("keydown", onKey);
  if (opts.onMount) opts.onMount(close);
  return close;
}

/** Substitui o confirm() nativo por um modal no padrão visual do sistema. */
export function confirmDialog(message, { confirmLabel = "Confirmar", danger = false } = {}) {
  return new Promise((resolve) => {
    let answered = false;
    openModal("Confirmar", `
      <p style="margin:0 0 18px;">${escapeHtml(message)}</p>
      <div class="actions-row" style="margin-top:0;">
        <button class="btn ${danger ? "btn-ghost" : "btn-primary"}" id="cdOk" ${danger ? 'style="color:var(--danger);border-color:var(--danger);"' : ""}>${escapeHtml(confirmLabel)}</button>
        <button class="btn btn-ghost" id="cdCancel">Cancelar</button>
      </div>`, {
      onMount: (close) => {
        $("cdOk").addEventListener("click", () => { answered = true; close(); resolve(true); });
        $("cdCancel").addEventListener("click", () => close());
        $("cdOk").focus();
      },
      onClose: () => { if (!answered) resolve(false); },
    });
  });
}

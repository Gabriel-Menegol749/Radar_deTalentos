import { api } from "./api.js";
import { $, qsa, toast, guard } from "./utils/dom.js";
import { getToken, goToLogin, saveSession, clearSession } from "./utils/auth.js";
import { openModal } from "./utils/modal.js";
import { state, loadRefs, scope } from "./state.js";
import { setView, bindGlobalFilters } from "./router.js";
import { candidateFilterParams } from "./pages/candidates.js";
import { mountPresentationBackground, unmountPresentationBackground } from "./presentation.js";

async function download(includeSensitive) {
  await guard(async () => {
    if (state.view === "candidatos") {
      await api.download("/api/exports/candidates", { ...candidateFilterParams(), includeSensitive }, "radar_candidatos.xlsx");
    } else {
      await api.download("/api/exports/pipeline", { ...scope(), includeSensitive }, "radar_pipeline.xlsx");
    }
    toast("Planilha exportada.");
  });
}

/** Exporta com os filtros da tela atual. Dados sensíveis só para Admin (D9). */
function exportExcel() {
  if (state.me.role !== "Admin") return download(false);
  openModal("Exportar Excel", `
    <p style="margin:0 0 6px;">A planilha respeita os filtros aplicados na tela atual.</p>
    <p class="muted" style="margin:0 0 18px;font-size:12.5px;">Dados sensíveis (diversidade e remunerações) só devem sair do sistema quando necessário (LGPD).</p>
    <div class="actions-row" style="margin-top:0;">
      <button class="btn btn-primary" id="expPlain">Exportar sem dados sensíveis</button>
      <button class="btn btn-ghost" id="expSensitive">Incluir dados sensíveis</button>
    </div>`, {
    onMount: (close) => {
      $("expPlain").addEventListener("click", () => { close(); download(false); });
      $("expSensitive").addEventListener("click", () => { close(); download(true); });
    },
  });
}

function togglePresentation() {
  state.presentMode = !state.presentMode;
  document.body.dataset.mode = state.presentMode ? "apresentacao" : "";
  $("btnPresentMode").textContent = state.presentMode ? "Sair da apresentação" : "Modo apresentação";
  if (state.presentMode) mountPresentationBackground(); else unmountPresentationBackground();
  setView("dashboard");
}

function bindStaticUI() {
  qsa(".navlink").forEach((b) => b.addEventListener("click", () => setView(b.dataset.view)));
  bindGlobalFilters();
  $("btnExportExcel").addEventListener("click", exportExcel);
  $("btnPresentMode").addEventListener("click", togglePresentation);
  $("btnLogout").addEventListener("click", () => { clearSession(); window.location.replace("/login.html"); });
}

async function init() {
  if (!getToken()) return goToLogin();
  try {
    state.me = await api.get("/api/auth/me");
    saveSession({ token: getToken(), user: state.me });
    await loadRefs();
  } catch (e) {
    if (e.status !== 401) toast(e.message, true);
    return;
  }
  $("userName").textContent = state.me.name;
  $("userRole").textContent = state.me.role;
  $("navAdmin").hidden = state.me.role !== "Admin";
  $("app").hidden = false;
  bindStaticUI();
  setView("dashboard");
}

init();

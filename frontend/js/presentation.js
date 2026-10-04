import { $ } from "./utils/dom.js";

// Fundo cinematográfico do Modo Apresentação (idêntico ao protótipo).
export function mountPresentationBackground() {
  if ($("pmFundo")) return;
  const fundo = document.createElement("div");
  fundo.id = "pmFundo";
  fundo.innerHTML = `
    <div class="mancha d-a" style="width:640px;height:640px;top:-160px;left:-120px;background:radial-gradient(circle, rgba(111,168,255,.5), transparent 70%);"></div>
    <div class="mancha d-b" style="width:560px;height:560px;bottom:-140px;right:-100px;background:radial-gradient(circle, rgba(255,112,96,.42), transparent 70%);"></div>
    <div class="mancha d-c" style="width:480px;height:480px;top:38%;left:52%;background:radial-gradient(circle, rgba(79,220,156,.34), transparent 70%);"></div>
  `;
  const grao = document.createElement("div");
  grao.id = "pmGrao";
  const vinheta = document.createElement("div");
  vinheta.id = "pmVinheta";
  document.body.prepend(vinheta);
  document.body.prepend(grao);
  document.body.prepend(fundo);
}

export function unmountPresentationBackground() {
  ["pmFundo", "pmGrao", "pmVinheta"].forEach((id) => { const el = $(id); if (el) el.remove(); });
}

// docs/37 CR4 (14 November 2026): preview documents in the page, without downloading.
// A button [data-preview-url][data-preview-kind][data-preview-name] (Views/Shared/_PreviewButton.cshtml) opens a full-screen modal:
//   * pdf:   PDF.js (lib/pdfjs, served by GRSMS itself), page by page, with previous / next, zoom in / out and fit to width;
//   * image: the picture, with zoom in / out and rotate;
//   * docx:  the HTML GRSMS makes from the Word file (sanitised on the server).
// The file comes from the same download address with ?view=inline (or ?view=html), so the same permission checks and the
// "viewed" log apply. Text is inserted with textContent; only the server's sanitised Word HTML is inserted as HTML.
(function () {
    "use strict";
    var base = document.body.dataset.pathBase || "";
    var modalEl = null, modal = null, state = null, pdfjs = null;

    function el(tag, cls, text) {
        var e = document.createElement(tag);
        if (cls) e.className = cls;
        if (text !== undefined) e.textContent = text;
        return e;
    }

    function withView(url, view) { return url + (url.indexOf("?") < 0 ? "?" : "&") + "view=" + view; }

    function button(icon, label, onClick) {
        var b = el("button", "btn btn-sm btn-outline-secondary");
        b.type = "button";
        b.setAttribute("aria-label", label);
        b.title = label;
        b.appendChild(el("i", "fa-solid " + icon));
        b.addEventListener("click", onClick);
        return b;
    }

    function build() {
        modalEl = el("div", "modal fade doc-preview");
        modalEl.tabIndex = -1;
        modalEl.setAttribute("aria-hidden", "true");
        modalEl.innerHTML =
            '<div class="modal-dialog modal-fullscreen-lg-down modal-xl modal-dialog-scrollable"><div class="modal-content">' +
            '<div class="modal-header gap-2 flex-wrap"><h2 class="modal-title h6 text-truncate me-auto" data-dp-title></h2>' +
            '<div class="d-flex flex-wrap align-items-center gap-1" data-dp-tools></div>' +
            '<a class="btn btn-sm btn-outline-primary" data-dp-download><i class="fa-solid fa-download me-1"></i>Download</a>' +
            '<button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button></div>' +
            '<div class="modal-body doc-preview-body" data-dp-body></div></div></div>';
        document.body.appendChild(modalEl);
        modal = new bootstrap.Modal(modalEl);
        modalEl.addEventListener("hidden.bs.modal", function () {
            if (state && state.pdf) state.pdf.destroy();
            state = null;
            modalEl.querySelector("[data-dp-body]").replaceChildren();
        });
    }

    function message(body, text, isError) {
        body.replaceChildren(el("div", "text-center p-5 " + (isError ? "text-danger" : "text-body-secondary"), text));
    }

    // ---- PDF
    async function renderPage() {
        var s = state;
        var page = await s.pdf.getPage(s.page);
        var fit = (s.body.clientWidth - 32) / page.getViewport({ scale: 1 }).width;
        var scale = (s.fitWidth ? fit : s.scale) * (window.devicePixelRatio || 1);
        var viewport = page.getViewport({ scale: scale });
        var canvas = el("canvas", "doc-preview-canvas");
        canvas.width = viewport.width;
        canvas.height = viewport.height;
        canvas.style.width = (viewport.width / (window.devicePixelRatio || 1)) + "px";
        await page.render({ canvasContext: canvas.getContext("2d"), viewport: viewport }).promise;
        if (state !== s) return;
        s.body.replaceChildren(canvas);
        s.pageLabel.textContent = "Page " + s.page + " of " + s.pdf.numPages;
        if (!s.fitWidth) s.scaleNow = s.scale; else s.scaleNow = fit;
    }

    async function openPdf(url, tools, body) {
        message(body, "Loading…");
        if (!pdfjs) {
            pdfjs = await import(base + "/lib/pdfjs/pdf.min.mjs");
            pdfjs.GlobalWorkerOptions.workerSrc = base + "/lib/pdfjs/pdf.worker.min.mjs";
        }
        var pdf = await pdfjs.getDocument({ url: url, isEvalSupported: false, withCredentials: true }).promise;
        state = { pdf: pdf, page: 1, scale: 1, fitWidth: true, body: body, pageLabel: el("span", "small text-nowrap px-1") };
        tools.append(
            button("fa-chevron-left", "Previous page", function () { if (state.page > 1) { state.page--; renderPage(); } }),
            state.pageLabel,
            button("fa-chevron-right", "Next page", function () { if (state.page < state.pdf.numPages) { state.page++; renderPage(); } }),
            button("fa-magnifying-glass-minus", "Zoom out", function () { state.scale = Math.max(0.3, (state.scaleNow || 1) / 1.25); state.fitWidth = false; renderPage(); }),
            button("fa-magnifying-glass-plus", "Zoom in", function () { state.scale = Math.min(5, (state.scaleNow || 1) * 1.25); state.fitWidth = false; renderPage(); }),
            button("fa-arrows-left-right", "Fit to width", function () { state.fitWidth = true; renderPage(); }));
        await renderPage();
    }

    // ---- Image
    function openImage(url, tools, body) {
        var img = el("img", "doc-preview-image");
        img.alt = "";
        img.src = url;
        var view = { scale: 1, rotate: 0 };
        function apply() { img.style.transform = "scale(" + view.scale + ") rotate(" + view.rotate + "deg)"; }
        img.addEventListener("error", function () { message(body, "The picture could not be shown. Download it instead.", true); });
        tools.append(
            button("fa-magnifying-glass-minus", "Zoom out", function () { view.scale = Math.max(0.2, view.scale / 1.25); apply(); }),
            button("fa-magnifying-glass-plus", "Zoom in", function () { view.scale = Math.min(6, view.scale * 1.25); apply(); }),
            button("fa-rotate-right", "Rotate", function () { view.rotate = (view.rotate + 90) % 360; apply(); }),
            button("fa-expand", "Actual size", function () { view.scale = 1; view.rotate = 0; apply(); }));
        var holder = el("div", "doc-preview-image-holder");
        holder.appendChild(img);
        body.replaceChildren(holder);
    }

    // ---- Word
    async function openDocx(url, body) {
        message(body, "Loading…");
        var r = await fetch(url, { credentials: "same-origin" });
        if (!r.ok) throw new Error(r.status);
        var page = el("div", "doc-preview-docx");
        page.innerHTML = await r.text(); // HTML made and sanitised by GRSMS (Infrastructure/Documents/DocxHtmlPreview)
        var note = el("p", "small text-body-secondary text-center mb-2", "Word document shown as text: the layout may differ from the original. Download it for the exact layout.");
        body.replaceChildren(note, page);
    }

    async function open(btn) {
        if (!modalEl) build();
        var url = btn.dataset.previewUrl, kind = btn.dataset.previewKind;
        modalEl.querySelector("[data-dp-title]").textContent = btn.dataset.previewName || "Document";
        modalEl.querySelector("[data-dp-download]").href = url;
        var tools = modalEl.querySelector("[data-dp-tools]");
        var body = modalEl.querySelector("[data-dp-body]");
        tools.replaceChildren();
        modal.show();
        try {
            if (kind === "pdf") await openPdf(withView(url, "inline"), tools, body);
            else if (kind === "image") openImage(withView(url, "inline"), tools, body);
            else if (kind === "docx") await openDocx(withView(url, "html"), body);
        } catch (e) {
            message(body, "This document could not be shown here. Use Download to open it.", true);
        }
    }

    document.addEventListener("click", function (e) {
        var btn = e.target.closest && e.target.closest("[data-preview-url]");
        if (!btn) return;
        e.preventDefault();
        open(btn);
    });

    window.addEventListener("resize", function () { if (state && state.fitWidth) renderPage(); });
})();

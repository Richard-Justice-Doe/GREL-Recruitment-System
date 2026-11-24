// docs/36: the "Draft with AI" panels. A panel (.ai-panel) names its endpoint (data-ai-endpoint) and the fields of the page's form
// to send along (data-ai-collect, comma-separated names). Its own inputs (data-ai-field) are sent too. The answer (Controllers/
// AiController) is shown as a preview; "Use this draft" copies the values into the form's fields, which the person then edits and
// saves as usual. Text is always inserted with textContent / value, never as HTML.
(function () {
    "use strict";

    function el(tag, cls, text) {
        var e = document.createElement(tag);
        if (cls) e.className = cls;
        if (text !== undefined) e.textContent = text;
        return e;
    }

    document.querySelectorAll(".ai-panel[data-ai-endpoint]").forEach(function (panel) {
        var form = panel.closest("form");
        var status = panel.querySelector("[data-ai-status]");
        var result = panel.querySelector("[data-ai-result]");
        var accept = panel.querySelector("[data-ai-accept]");
        var runButtons = panel.querySelectorAll("[data-ai-run]");
        var last = null;

        function setBusy(busy) {
            runButtons.forEach(function (b) { b.disabled = busy; });
            panel.classList.toggle("is-busy", busy);
            if (busy) {
                status.replaceChildren(el("span", "spinner-border spinner-border-sm me-2", ""), document.createTextNode("Drafting… this can take up to a minute."));
            }
        }

        function show(answer) {
            result.replaceChildren();
            if (!answer.ok) {
                status.replaceChildren(el("span", "text-danger", answer.problem || "AI is not available right now."));
                if (accept) accept.hidden = true;
                return;
            }
            var badges = el("div", "d-flex flex-wrap gap-2 align-items-center mb-2");
            badges.appendChild(el("span", "badge rounded-pill badge-soft-primary", "AI draft"));
            if (answer.confidence) {
                var tone = answer.confidence === "High" ? "success" : answer.confidence === "Medium" ? "warning" : "danger";
                var b = el("span", "badge rounded-pill badge-soft-" + tone, "Confidence: " + answer.confidence);
                if (answer.confidenceNote) b.title = answer.confidenceNote;
                badges.appendChild(b);
            }
            if (answer.fromCache) badges.appendChild(el("span", "badge rounded-pill badge-soft-secondary", "Same as last time (saved answer)"));
            result.appendChild(badges);
            if (answer.confidenceNote) result.appendChild(el("p", "small text-body-secondary mb-2", answer.confidenceNote));
            (answer.sections || []).forEach(function (s) {
                var box = el("details", "ai-section");
                box.open = true;
                box.appendChild(el("summary", "fw-semibold small", s.title));
                box.appendChild(el("pre", "ai-text", s.text));
                result.appendChild(box);
            });
            status.replaceChildren(el("span", "text-body-secondary", accept ? "Check the draft, then use it. Nothing is saved until you save the form." : (panel.dataset.aiDoneNote || "")));
            if (accept) accept.hidden = false;
        }

        function run(regenerate) {
            var data = new FormData();
            var token = form && form.querySelector("input[name=__RequestVerificationToken]");
            if (token) data.append("__RequestVerificationToken", token.value);
            (panel.dataset.aiCollect || "").split(",").filter(Boolean).forEach(function (name) {
                var field = form && form.elements[name.trim()];
                if (field) data.append(name.trim(), field.value);
            });
            panel.querySelectorAll("[data-ai-field]").forEach(function (f) { data.append(f.dataset.aiField, f.value); });
            Object.keys(panel.dataset).filter(function (k) { return k.indexOf("aiParam") === 0; }).forEach(function (k) {
                var name = k.substring(7);
                data.append(name.charAt(0).toLowerCase() + name.slice(1), panel.dataset[k]);
            });
            data.append("regenerate", regenerate ? "true" : "false");
            setBusy(true);
            fetch(panel.dataset.aiEndpoint, { method: "POST", body: data, credentials: "same-origin", headers: { "X-Requested-With": "fetch" } })
                .then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); })
                .then(function (answer) { last = answer; show(answer); })
                .catch(function () { show({ ok: false, problem: "AI is not available right now. You can write it yourself, or try again later." }); })
                .finally(function () { setBusy(false); });
        }

        runButtons.forEach(function (b) {
            b.addEventListener("click", function () { run(b.hasAttribute("data-ai-regenerate")); });
        });

        // data-ai-auto: run once when the page opens (e.g. the executive summary, usually answered from the cache).
        if (panel.hasAttribute("data-ai-auto")) run(false);

        if (accept) accept.addEventListener("click", function () {
            if (!last || !last.ok || !form) return;
            var names = Object.keys(last.fields || {});
            var filled = names.filter(function (n) { var f = form.elements[n]; return f && f.value && f.value.trim() !== "" && f.value !== last.fields[n]; });
            if (filled.length && !window.confirm("This replaces what is already in: " + filled.length + " field(s). Continue?")) return;
            names.forEach(function (n) {
                var f = form.elements[n];
                if (!f) return;
                f.value = last.fields[n];
                f.dispatchEvent(new Event("input", { bubbles: true }));
                f.dispatchEvent(new Event("change", { bubbles: true }));
                var holder = f.closest("details");
                if (holder) holder.open = true;
                f.classList.add("ai-filled");
            });
            status.replaceChildren(el("span", "text-success", "Copied into the form (highlighted). Edit anything, then save."));
            accept.hidden = true;
        });
    });
})();

// docs/36: forms that ask AI on the server (e.g. the interview guide) show that they are working and cannot be sent twice.
(function () {
    document.querySelectorAll("form[data-ai-busy]").forEach(function (form) {
        form.addEventListener("submit", function () {
            setTimeout(function () {
                form.querySelectorAll("button").forEach(function (b) { b.disabled = true; });
                var note = document.createElement("div");
                note.className = "small text-body-secondary w-100 mt-1";
                note.innerHTML = "<span class=\"spinner-border spinner-border-sm me-2\"></span>";
                note.appendChild(document.createTextNode("Drafting\u2026 this can take up to a minute."));
                form.appendChild(note);
            }, 0);
        });
    });
})();

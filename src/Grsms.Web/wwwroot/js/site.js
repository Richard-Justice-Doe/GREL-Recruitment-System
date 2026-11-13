// =========================================================================
// GRSMS page behaviour (loaded on every page, after jQuery and Bootstrap).
//   1. Light / dark mode toggle (choice remembered in this browser)
//   2. Mobile sidebar open/close
//   3. "Are you sure?" confirmation for buttons marked data-confirm, and the messages after an action,
//      both shown with SweetAlert2 (wwwroot/lib/sweetalert2, served locally for the Content-Security-Policy)
//   4. DataTables (search, sort, paging) on any <table class="datatable">
//   5. "Copy" buttons: <button data-copy-target="id-of-textarea"> (job advert texts)
//   6. Print buttons: <button data-print> (offer letters)
//   7. Filters that submit on change: <select data-auto-submit>
//   8. Numbers that count up on the dashboard tiles (the rest of the motion is CSS: css/motion.css)
// No inline scripts or onclick="..." anywhere: the Content-Security-Policy (Phase 12) only runs files from /js and /lib.
// =========================================================================

(function () {
    "use strict";

    function escapeHtml(s) {
        return s.replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; });
    }

    // SweetAlert2 with GRSMS's look: Bootstrap buttons, and the page's own light / dark colours (see site.css).
    var GrsmsSwal = window.Swal ? window.Swal.mixin({
        buttonsStyling: false,
        reverseButtons: true,
        customClass: { popup: "grsms-swal", confirmButton: "btn btn-primary", cancelButton: "btn btn-outline-secondary", actions: "gap-2" }
    }) : null;

    // ---- 1. Theme toggle ------------------------------------------------
    // The saved theme is applied by a tiny script in <head> before the page
    // draws (so it never flashes white); this only handles the button.
    function setTheme(theme) {
        document.documentElement.setAttribute("data-bs-theme", theme);
        try { localStorage.setItem("grsms-theme", theme); } catch (e) { /* private mode: ignore */ }
        document.querySelectorAll("[data-theme-icon]").forEach(function (icon) {
            icon.className = theme === "dark" ? "fa-solid fa-sun" : "fa-solid fa-moon";
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        setTheme(document.documentElement.getAttribute("data-bs-theme") || "light");

        document.querySelectorAll("[data-theme-toggle]").forEach(function (btn) {
            btn.addEventListener("click", function () {
                var current = document.documentElement.getAttribute("data-bs-theme");
                // Cross-fade the colours for a moment (motion.css, section 8), unless less motion was asked for.
                var root = document.documentElement;
                if (!reduceMotion()) {
                    root.classList.add("theme-switching");
                    setTimeout(function () { root.classList.remove("theme-switching"); }, 400);
                }
                setTheme(current === "dark" ? "light" : "dark");
            });
        });

        // ---- 2. Mobile sidebar ------------------------------------------
        document.querySelectorAll("[data-sidebar-toggle]").forEach(function (btn) {
            btn.addEventListener("click", function () { document.body.classList.toggle("sidebar-open"); });
        });

        // ---- 2b. Menu groups (Hire, Onboard, ...) remember being opened or closed in this browser.
        // The group holding the current page always stays open (data-nav-current="yes").
        var navState = {};
        try { navState = JSON.parse(localStorage.getItem("grsms-nav-groups") || "{}") || {}; } catch (e) { navState = {}; }
        document.querySelectorAll("details[data-nav-group]").forEach(function (group) {
            var key = group.getAttribute("data-nav-group");
            if (group.getAttribute("data-nav-current") !== "yes" && typeof navState[key] === "boolean") group.open = navState[key];
            group.addEventListener("toggle", function () {
                navState[key] = group.open;
                try { localStorage.setItem("grsms-nav-groups", JSON.stringify(navState)); } catch (e) { /* private mode: ignore */ }
            });
        });

        // ---- 3. Confirmation prompts (SweetAlert2) -----------------------
        // <button data-confirm="Deactivate this department?"> asks before submitting its form. The form is sent with the
        // same button, so name="decision" value="Approve" style buttons keep working. A red (danger) button gets a red
        // "Yes" in the dialog. Without SweetAlert (script blocked) the browser's own confirm box is used.
        document.addEventListener("submit", function (e) {
            var form = e.target;
            var btn = e.submitter;
            var message = (btn && btn.getAttribute("data-confirm")) || form.getAttribute("data-confirm");
            if (!message) return;
            if (form.dataset.confirmed === "yes") { delete form.dataset.confirmed; return; } // the user already said yes

            e.preventDefault();
            if (!window.Swal) {
                if (window.confirm(message)) { form.dataset.confirmed = "yes"; form.requestSubmit(btn || undefined); }
                return;
            }
            var danger = !!btn && /\bbtn(-outline)?-danger\b/.test(btn.className);
            GrsmsSwal.fire({
                icon: danger ? "warning" : "question",
                title: "Are you sure?",
                text: message,
                showCancelButton: true,
                // Button texts are HTML in SweetAlert, so the label copied from the page is escaped first.
                confirmButtonText: btn && btn.textContent.trim() ? escapeHtml(btn.textContent.trim()) : "Yes, continue",
                cancelButtonText: "Cancel",
                focusCancel: danger,
                // customClass replaces the shared one (not merged), so it is given in full.
                customClass: { popup: "grsms-swal", actions: "gap-2", confirmButton: danger ? "btn btn-danger" : "btn btn-primary", cancelButton: "btn btn-outline-secondary" }
            }).then(function (answer) {
                if (answer.isConfirmed) { form.dataset.confirmed = "yes"; form.requestSubmit(btn || undefined); }
            });
        });

        // ---- 3b. Messages after an action (SweetAlert2) -------------------
        // _Alerts.cshtml marks each message with data-flash="success|info|error". Success and info appear as a small
        // notice in the top corner that closes itself; errors open a dialog that must be closed, so they are not missed.
        if (window.Swal) {
            document.querySelectorAll("[data-flash]").forEach(function (banner) {
                var kind = banner.getAttribute("data-flash");
                var text = banner.getAttribute("data-flash-message");
                banner.classList.add("d-none");
                if (kind === "error") {
                    GrsmsSwal.fire({ icon: "error", title: "That didn't work", text: text, confirmButtonText: "OK" });
                } else {
                    GrsmsSwal.fire({
                        // titleText, never title: SweetAlert treats "title" as HTML, and messages can contain names people typed.
                        toast: true, position: "top-end", icon: kind, titleText: text,
                        showConfirmButton: false, showCloseButton: true, timer: 5000, timerProgressBar: true
                    });
                }
            });
        }

        // ---- 4. DataTables ----------------------------------------------
        if (window.DataTable) {
            document.querySelectorAll("table.datatable").forEach(function (table) {
                new DataTable(table, {
                    pageLength: 25,
                    stateSave: true, stateDuration: -1, // docs/32: page, search and sort kept for this browser session
                    order: [],                       // keep the server's order until the user clicks a column
                    columnDefs: [{ targets: "no-sort", orderable: false, searchable: false }],
                    language: { search: "", searchPlaceholder: "Search..." }
                });
            });
        }

        // ---- 6. Print buttons ---------------------------------------------
        document.querySelectorAll("[data-print]").forEach(function (btn) {
            btn.addEventListener("click", function () { window.print(); });
        });

        // ---- 7. Drop-downs that apply a filter as soon as they change -----
        // (Phase 12: replaces inline onchange="..." handlers, which the Content-Security-Policy does not allow.)
        document.querySelectorAll("[data-auto-submit]").forEach(function (field) {
            field.addEventListener("change", function () { if (field.form) field.form.submit(); });
        });

        // ---- 5. Copy buttons --------------------------------------------
        document.querySelectorAll("[data-copy-target]").forEach(function (btn) {
            btn.addEventListener("click", function () {
                var box = document.getElementById(btn.getAttribute("data-copy-target"));
                if (!box) return;
                var done = function () {
                    var old = btn.innerHTML;
                    btn.innerHTML = '<i class="fa-solid fa-check me-1"></i>Copied';
                    setTimeout(function () { btn.innerHTML = old; }, 1500);
                };
                if (navigator.clipboard && window.isSecureContext) {
                    navigator.clipboard.writeText(box.value).then(done);
                } else {
                    box.select();
                    document.execCommand("copy");   // fallback for plain-http addresses
                    done();
                }
            });
        });

        // ---- 8. Numbers count up (motion.css has the rest of the motion) ---
        // The figures on dashboard tiles count from 0 to their value when they first come into view. The text the
        // server wrote is restored exactly at the end, prefixes and suffixes ("86%", "1,234") included.
        // Skipped when the person asked their device for less motion.
        if (!reduceMotion() && "IntersectionObserver" in window) {
            var seen = new IntersectionObserver(function (entries) {
                entries.forEach(function (entry) {
                    if (!entry.isIntersecting) return;
                    seen.unobserve(entry.target);
                    countUp(entry.target);
                });
            }, { threshold: 0.4 });
            document.querySelectorAll(".kpi-value, .ats-tile-value").forEach(function (el) { seen.observe(el); });
        }
    });

    function reduceMotion() {
        return window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    }

    function countUp(el) {
        var original = el.textContent;
        var m = /^(\s*[^\d\-]*)(-?[\d,]*\.?\d+)(.*)$/s.exec(original);
        if (!m) return;
        var target = parseFloat(m[2].replace(/,/g, ""));
        if (!isFinite(target) || target === 0) return;
        var decimals = (m[2].split(".")[1] || "").length;
        var grouped = m[2].indexOf(",") >= 0;
        var format = new Intl.NumberFormat("en-GB", { minimumFractionDigits: decimals, maximumFractionDigits: decimals, useGrouping: grouped });
        var duration = 700, start = null, finished = false;
        function finish() { finished = true; el.textContent = original; } // always end on exactly what the server wrote
        function frame(now) {
            if (finished) return;
            if (start === null) start = now;
            var t = Math.min(1, (now - start) / duration);
            var eased = 1 - Math.pow(1 - t, 3); // ease-out
            el.textContent = m[1] + format.format(target * eased) + m[3];
            if (t < 1) requestAnimationFrame(frame); else finish();
        }
        requestAnimationFrame(frame);
        // Browsers slow or pause animation frames (background tabs, battery saving): the real value still appears on time.
        setTimeout(finish, duration + 150);
    }
})();

// docs/32 (11 November 2026): keep the user's place.
//   a. Each list page (one path segment, e.g. /Vacancies?tab=open&search=Engineer&page=7) is remembered for the browser
//      session. On record pages (two or more segments, e.g. /Vacancies/Details/12) a plain link back to that list
//      ("All vacancies", "Back") goes to the list as it was left. Menu, top-bar and dashboard links stay as written.
//   b. When a form is posted, the scroll position and the open sections (Bootstrap collapses, <details>) are saved; when
//      the answer is the same page again within a minute (Approve, Save, Return, ... come back to the record), they are
//      restored. If the page came back with validation errors, the first error is scrolled into view instead. A link to a
//      spot on the page (#doc-12) always wins. Forms marked data-keep-place="off" are left alone.
// Session storage only; if the browser blocks it, pages behave as before.
(function () {
    "use strict";
    var store;
    try { store = window.sessionStorage; store.setItem("grsms-probe", "1"); store.removeItem("grsms-probe"); } catch (e) { return; }
    var POS = "grsms-pos", LIST = "grsms-list:", MAX_AGE_MS = 60000;
    var base = ((document.body && document.body.getAttribute("data-path-base")) || "").replace(/\/+$/, "").toLowerCase();

    function segments(pathname) {
        var p = pathname.toLowerCase();
        if (base && p.indexOf(base) === 0) p = p.substring(base.length);
        return p.replace(/\/index\/?$/, "").split("/").filter(Boolean);
    }
    function listKey(pathname) { return LIST + segments(pathname)[0]; }
    function here() { return location.pathname + location.search; }

    document.addEventListener("DOMContentLoaded", function () {
        var depth = segments(location.pathname).length;

        // a. Lists
        if (depth === 1) {
            store.setItem(listKey(location.pathname), here());
        } else if (depth >= 2) {
            var main = document.querySelector("main") || document.body;
            main.querySelectorAll("a[href]").forEach(function (a) {
                if (a.origin !== location.origin || a.search || a.hash || a.hasAttribute("data-fresh")) return;
                if (segments(a.pathname).length !== 1) return;
                var saved = store.getItem(listKey(a.pathname));
                if (saved && saved !== a.pathname) a.setAttribute("href", saved);
            });
        }

        // b. Place on the page, after the other scripts have laid the page out (topic tabs, tables, steps).
        var raw = store.getItem(POS);
        store.removeItem(POS);
        setTimeout(function () {
            var error = document.querySelector(".validation-summary-errors, .field-validation-error, .input-validation-error");
            if (error) { error.scrollIntoView({ block: "center" }); return; }
            if (!raw || location.hash) return;
            var saved;
            try { saved = JSON.parse(raw); } catch (e) { return; }
            if (!saved || saved.page !== here() || Date.now() - saved.t > MAX_AGE_MS) return;
            (saved.collapses || []).forEach(function (id) {
                var el = document.getElementById(id);
                if (!el || el.classList.contains("show")) return;
                el.classList.add("show");
                document.querySelectorAll('[data-bs-target="#' + id + '"], a[href="#' + id + '"]').forEach(function (t) {
                    t.classList.remove("collapsed");
                    t.setAttribute("aria-expanded", "true");
                });
            });
            var details = document.querySelectorAll("main details");
            (saved.details || []).forEach(function (i) { if (details[i]) details[i].open = true; });
            var max = Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
            window.scrollTo({ top: Math.min(saved.y, max), left: 0, behavior: "instant" });
        }, 0);

        // Registered here, after the "Are you sure?" handler (part 3 above), so a cancelled confirmation saves nothing.
        document.addEventListener("submit", function (e) {
            var form = e.target;
            if (e.defaultPrevented || !(form instanceof HTMLFormElement) || form.getAttribute("data-keep-place") === "off") return;
            if ((form.getAttribute("method") || "get").toLowerCase() !== "post") return;
            var open = [];
            document.querySelectorAll("main details").forEach(function (d, i) { if (d.open) open.push(i); });
            var collapses = [];
            document.querySelectorAll("main .collapse.show[id]").forEach(function (c) { collapses.push(c.id); });
            try { store.setItem(POS, JSON.stringify({ page: here(), y: window.scrollY, t: Date.now(), collapses: collapses, details: open })); } catch (err) { /* full */ }
        });
    });
})();

// Usability U2 (10 November 2026): before uploading a document, show which file was chosen, with a small preview for photos
// (read as a data: URL, which the Content-Security-Policy allows). Works on any <input type="file" data-upload-preview>.
(function () {
    document.addEventListener("change", function (e) {
        var input = e.target;
        if (!(input instanceof HTMLInputElement) || input.type !== "file" || !input.hasAttribute("data-upload-preview")) return;
        var holder = input.closest("form");
        if (!holder) return;
        var old = holder.querySelector(".doc-upload-preview, .doc-upload-name");
        if (old) old.remove();
        var file = input.files && input.files[0];
        if (!file) return;
        if (file.type.indexOf("image/") === 0 && file.size < 15 * 1024 * 1024) {
            var reader = new FileReader();
            reader.onload = function () {
                var img = document.createElement("img");
                img.className = "doc-upload-preview";
                img.alt = "Preview of " + file.name;
                img.src = String(reader.result);
                holder.appendChild(img);
            };
            reader.readAsDataURL(file);
        } else {
            var note = document.createElement("div");
            note.className = "doc-upload-name small text-body-secondary mt-1";
            note.textContent = "Chosen: " + file.name + " (" + Math.max(1, Math.round(file.size / 1024)) + " KB)";
            holder.appendChild(note);
        }
    });
})();

// docs/36: AI settings page: show the Azure OpenAI fields only when Azure OpenAI is chosen.
(function () {
    var select = document.querySelector("[data-ai-provider]");
    if (!select) return;
    select.addEventListener("change", function () {
        document.querySelectorAll(".ai-azure-only").forEach(function (el) { el.hidden = select.value !== "AzureOpenAI"; });
    });
})();

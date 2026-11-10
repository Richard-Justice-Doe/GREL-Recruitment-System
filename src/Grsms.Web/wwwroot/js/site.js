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

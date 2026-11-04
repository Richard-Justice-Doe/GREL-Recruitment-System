// =========================================================================
// GRSMS page behaviour (loaded on every page, after jQuery and Bootstrap).
//   1. Light / dark mode toggle (choice remembered in this browser)
//   2. Mobile sidebar open/close
//   3. "Are you sure?" confirmation for buttons marked data-confirm
//   4. DataTables (search, sort, paging) on any <table class="datatable">
//   5. "Copy" buttons: <button data-copy-target="id-of-textarea"> (job advert texts)
//   6. Print buttons: <button data-print> (offer letters)
// =========================================================================

(function () {
    "use strict";

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
                setTheme(current === "dark" ? "light" : "dark");
            });
        });

        // ---- 2. Mobile sidebar ------------------------------------------
        document.querySelectorAll("[data-sidebar-toggle]").forEach(function (btn) {
            btn.addEventListener("click", function () { document.body.classList.toggle("sidebar-open"); });
        });

        // ---- 3. Confirmation prompts ------------------------------------
        // <button data-confirm="Deactivate this department?"> asks before submitting its form.
        document.addEventListener("submit", function (e) {
            var btn = e.submitter;
            var message = (btn && btn.getAttribute("data-confirm")) || e.target.getAttribute("data-confirm");
            if (message && !window.confirm(message)) e.preventDefault();
        });

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
    });
})();

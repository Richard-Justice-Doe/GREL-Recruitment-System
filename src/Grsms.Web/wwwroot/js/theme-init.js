// Loaded in <head> BEFORE the page is drawn: applies the saved light/dark choice
// (or the computer's own setting the first time) so the page never flashes the wrong colours.
(function () {
    var theme = null;
    try { theme = localStorage.getItem("grsms-theme"); } catch (e) { /* storage blocked: ignore */ }
    if (theme !== "light" && theme !== "dark") {
        theme = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
    }
    document.documentElement.setAttribute("data-bs-theme", theme);
})();

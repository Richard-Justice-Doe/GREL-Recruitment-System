// 14 November 2026: the onboarding document checklist (Views/Shared/_EmployeeDocuments.cshtml).
//   * The chips filter the documents (all / needs your action / waiting for review / rejected / missing / verified); groups with
//     nothing to show are hidden, the others opened.
//   * After an upload the page comes back with #doc-12 in the address: that document and its group are opened and scrolled to.
(function () {
    "use strict";
    document.querySelectorAll("[data-doc-checklist]").forEach(function (list) {
        var chips = list.querySelectorAll("[data-doc-filter]");
        var none = list.querySelector("[data-doc-none]");

        function apply(filter) {
            var shown = 0;
            list.querySelectorAll("[data-doc-group]").forEach(function (group) {
                var visible = 0;
                group.querySelectorAll(".doc-item").forEach(function (item) {
                    var match = filter === "all" || (filter === "mine" ? item.dataset.docMine === "true" : item.dataset.docState === filter);
                    item.hidden = !match;
                    if (match) visible++;
                });
                group.hidden = visible === 0;
                if (filter !== "all" && visible > 0) group.open = true;
                shown += visible;
            });
            if (none) none.classList.toggle("d-none", shown > 0);
            chips.forEach(function (c) {
                var on = c.dataset.docFilter === filter;
                c.classList.toggle("is-active", on);
                c.setAttribute("aria-pressed", on ? "true" : "false");
            });
        }

        chips.forEach(function (c) { c.addEventListener("click", function () { apply(c.dataset.docFilter); }); });

        if (location.hash && location.hash.indexOf("#doc-") === 0) {
            var target = list.querySelector(location.hash.replace(/[^#\w-]/g, ""));
            if (target) {
                target.open = true;
                var group = target.closest("[data-doc-group]");
                if (group) group.open = true;
                target.classList.add("is-flash");
                target.scrollIntoView({ block: "center" });
            }
        }
    });
})();

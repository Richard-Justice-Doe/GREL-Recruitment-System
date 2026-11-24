// 17 November 2026: "Suggest objectives and dates with AI" on the probation's initial meeting form (Views/Probation/Details.cshtml).
// Asks AiController.ProbationPlan, then replaces the empty objective / development plan rows with the suggestions, using the
// repeater's own "Add" buttons (js/repeater.js) so the rows post like hand-typed ones. Nothing is saved until the Head of
// Department edits, types their name and clicks "Save and sign".
(function () {
    "use strict";

    function rowIsEmpty(row) {
        return Array.prototype.every.call(row.querySelectorAll("input:not([type=hidden])"), function (i) { return !i.value; });
    }

    function fill(repeater, items, setters) {
        if (!repeater || !items.length) return 0;
        var list = repeater.querySelector("[data-repeater-list]");
        var add = repeater.querySelector("[data-repeater-add]");
        Array.prototype.forEach.call(list.querySelectorAll("[data-repeater-row]"), function (row) { if (rowIsEmpty(row)) row.remove(); });
        items.forEach(function (item) {
            add.click();
            var rows = list.querySelectorAll("[data-repeater-row]");
            var row = rows[rows.length - 1];
            Object.keys(setters).forEach(function (suffix) {
                var input = row.querySelector('input[name$=".' + suffix + '"]');
                if (input) { input.value = setters[suffix](item) || ""; input.classList.add("ai-filled"); }
            });
        });
        return items.length;
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("[data-probation-ai]").forEach(function (panel) {
            var form = panel.closest("form");
            var button = panel.querySelector("[data-probation-ai-run]");
            var status = panel.querySelector("[data-probation-ai-status]");
            button.addEventListener("click", function () {
                var data = new FormData();
                var token = form && form.querySelector("input[name=__RequestVerificationToken]");
                if (token) data.append("__RequestVerificationToken", token.value);
                button.disabled = true;
                status.textContent = "Working on suggestions…";
                fetch(panel.dataset.probationAi, { method: "POST", body: data, credentials: "same-origin", headers: { "X-Requested-With": "fetch" } })
                    .then(function (r) { if (!r.ok) throw new Error("HTTP " + r.status); return r.json(); })
                    .then(function (res) {
                        if (!res.ok) { status.textContent = res.problem || "No suggestions this time."; return; }
                        var repeaters = form.querySelectorAll("[data-repeater]");
                        var objectives = Array.prototype.find.call(repeaters, function (r) { return r.querySelector('[name$=".SuccessIndicators"]') || r.querySelector("template") && r.querySelector("template").innerHTML.indexOf(".SuccessIndicators") >= 0; });
                        var plan = Array.prototype.find.call(repeaters, function (r) { return r.querySelector("template") && r.querySelector("template").innerHTML.indexOf(".TrainingNeed") >= 0; });
                        var n = fill(objectives, res.objectives || [], {
                            Objective: function (o) { return o.objective; },
                            SuccessIndicators: function (o) { return o.successIndicators; },
                            DueDate: function (o) { return o.dueDate; }
                        });
                        var m = fill(plan, res.plan || [], {
                            TrainingNeed: function (d) { return d.trainingNeed; },
                            DevelopmentNeed: function (d) { return d.developmentNeed; },
                            Timeline: function (d) { return d.timeline; }
                        });
                        status.textContent = n + " objective(s) and " + m + " development line(s) suggested. Check and edit them before signing.";
                    })
                    .catch(function () { status.textContent = "AI is not available right now. Please write them yourself."; })
                    .finally(function () { button.disabled = false; });
            });
        });
    });
})();

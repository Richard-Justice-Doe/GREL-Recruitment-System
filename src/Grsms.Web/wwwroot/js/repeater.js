// Repeating rows on a form (e.g. children, languages, beneficiaries on the Personal Record Declaration).
//
// Markup:
//   <div data-repeater>
//     <div data-repeater-list> ...rows, each <div data-repeater-row> with a hidden "List.Index" input... </div>
//     <template data-repeater-template> one row, with __key__ wherever the row key goes </template>
//     <button type="button" data-repeater-add>Add</button>
//   </div>
// Each row carries <input type="hidden" name="Children.Index" value="KEY"> and fields named Children[KEY].FullName, so
// ASP.NET Core binds the rows whatever their keys are; removing a row in the middle leaves no gap. Without JavaScript, the
// page shows the saved rows plus one blank row, and the server ignores rows left completely empty.
//
// Also: [data-percent-total] shows the sum of the inputs marked [data-percent] inside the same [data-repeater].
(function () {
    "use strict";

    var counter = 0;

    function updateTotals(root) {
        var target = root.querySelector("[data-percent-total]");
        if (!target) return;
        var sum = 0;
        root.querySelectorAll("[data-percent]").forEach(function (i) {
            var v = parseFloat(i.value);
            if (!isNaN(v)) sum += v;
        });
        sum = Math.round(sum * 100) / 100;
        target.textContent = sum.toString();
        var ok = sum === 100;
        target.parentElement.classList.toggle("text-success", ok);
        target.parentElement.classList.toggle("text-danger", !ok);
    }

    function init(root) {
        var list = root.querySelector("[data-repeater-list]");
        var template = root.querySelector("template[data-repeater-template]");
        var add = root.querySelector("[data-repeater-add]");
        if (!list || !template || !add) return;

        add.addEventListener("click", function () {
            var key = "n" + Date.now().toString(36) + (counter++);
            var html = template.innerHTML.replace(/__key__/g, key);
            var holder = document.createElement("div");
            holder.innerHTML = html.trim();
            var row = holder.firstElementChild;
            list.appendChild(row);
            var first = row.querySelector("input:not([type=hidden]), select, textarea");
            if (first) first.focus();
            updateTotals(root);
        });

        root.addEventListener("click", function (e) {
            var remove = e.target.closest("[data-repeater-remove]");
            if (!remove) return;
            var row = remove.closest("[data-repeater-row]");
            if (row) row.remove();
            updateTotals(root);
        });

        root.addEventListener("input", function () { updateTotals(root); });
        updateTotals(root);
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("[data-repeater]").forEach(init);

        // Show "describe the disability" only when the box is ticked.
        var disability = document.querySelector("[data-toggle-disability]");
        var details = document.getElementById("disabilityDetailsGroup");
        function toggle() { if (disability && details) details.style.display = disability.checked ? "" : "none"; }
        if (disability) { disability.addEventListener("change", toggle); toggle(); }
    });
})();

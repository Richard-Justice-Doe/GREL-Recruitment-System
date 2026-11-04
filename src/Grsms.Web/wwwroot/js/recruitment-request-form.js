// Recruitment request form behaviour:
//   1. When the department changes, reload its units into the Unit drop-down.
//   2. Show the "Employee being replaced" box only when the reason is "Replacement".
(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        var dept = document.getElementById("DepartmentId");
        var unit = document.getElementById("UnitId");
        var reason = document.querySelector("[data-toggle-replacement]");
        var replacedGroup = document.getElementById("replacedEmployeeGroup");

        // ---- 1. Units follow the department
        if (dept && unit) {
            dept.addEventListener("change", function () {
                var first = unit.options[0];          // keep the "None" option
                unit.innerHTML = "";
                unit.appendChild(first);
                if (!dept.value) return;

                fetch(dept.dataset.unitsUrl + "?departmentId=" + encodeURIComponent(dept.value), { credentials: "same-origin" })
                    .then(function (r) { return r.ok ? r.json() : []; })
                    .then(function (items) {
                        items.forEach(function (item) {
                            var o = document.createElement("option");
                            o.value = item.value;
                            o.textContent = item.text;   // textContent, never innerHTML: safe for any unit name
                            unit.appendChild(o);
                        });
                    });
            });
        }

        // ---- 2. Replacement details
        function toggleReplacement() {
            if (!reason || !replacedGroup) return;
            // The enum's numeric value for "Replacement" is rendered in the option; match on its text instead.
            var text = reason.options[reason.selectedIndex] ? reason.options[reason.selectedIndex].text : "";
            replacedGroup.style.display = text.indexOf("Replacement") === 0 ? "" : "none";
        }
        if (reason) {
            reason.addEventListener("change", toggleReplacement);
            toggleReplacement();
        }
    });
})();

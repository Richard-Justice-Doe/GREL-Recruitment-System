// Recruitment request form (QF-81-11) behaviour:
//   1. When the department changes, reload its units into the Unit drop-down.
//   2. Show "Employee being replaced" only for the purpose "Replacement", and "Other purpose" only for "Other".
//   3. Show "Licence class" only when a driving licence is desirable or mandatory.
(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        var dept = document.getElementById("DepartmentId");
        var unit = document.getElementById("UnitId");

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

        // ---- 2. Boxes that belong to one purpose (data-show-for-purpose="Replacement" / "Other")
        var purposes = document.querySelectorAll("[data-purpose]");
        var purposeBoxes = document.querySelectorAll("[data-show-for-purpose]");
        function togglePurpose() {
            var chosen = "";
            purposes.forEach(function (r) { if (r.checked) chosen = r.value; });
            purposeBoxes.forEach(function (box) {
                box.style.display = box.getAttribute("data-show-for-purpose") === chosen ? "" : "none";
            });
        }
        purposes.forEach(function (r) { r.addEventListener("change", togglePurpose); });
        togglePurpose();

        // ---- 3. Licence class
        var licences = document.querySelectorAll("[data-licence]");
        var licenceGroup = document.getElementById("licenceClassGroup");
        function toggleLicence() {
            var chosen = "NotNeeded";
            licences.forEach(function (r) { if (r.checked) chosen = r.value; });
            if (licenceGroup) licenceGroup.style.display = chosen === "NotNeeded" ? "none" : "";
        }
        licences.forEach(function (r) { r.addEventListener("change", toggleLicence); });
        toggleLicence();
    });
})();

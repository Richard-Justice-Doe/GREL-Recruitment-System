// Usability U2/U4 (10 November 2026): a long form shown one section at a time.
//
//   <form data-steps> ... <div data-step="About you">...</div> <div data-step="Bank account">...</div> ...
//                         <div data-steps-finish> (the save button) </div> </form>
//
// Shows "Step 2 of 6", a progress bar, and Back / Next buttons; the save button appears on the last step. The fields are still one
// form, saved together, so every server rule is unchanged. Without JavaScript, or when the server sent the form back with
// errors, every section is shown at once (nothing can be hidden from the person who has to fix it).
(function () {
    "use strict";

    function setUp(form) {
        var steps = Array.prototype.slice.call(form.querySelectorAll("[data-step]"));
        if (steps.length < 2 || form.querySelector(".validation-summary-errors, .input-validation-error")) return;
        var finish = form.querySelector("[data-steps-finish]");
        var current = 0;

        var head = document.createElement("div");
        head.className = "form-steps-head mb-3";
        head.innerHTML = '<div class="small text-body-secondary" data-steps-count></div>' +
            '<div class="h5 mb-2" data-steps-title></div>' +
            '<div class="progress" style="height: 6px" role="progressbar" aria-label="Progress through the form"><div class="progress-bar" data-steps-bar></div></div>';
        var top = steps[0].closest("form > *") || steps[0]; // the first step, or the row it sits in
        top.parentNode.insertBefore(head, top);

        var nav = document.createElement("div");
        nav.className = "form-steps-nav d-flex justify-content-between gap-2 my-3";
        nav.innerHTML = '<button type="button" class="btn btn-outline-secondary" data-steps-back><i class="fa-solid fa-arrow-left me-1"></i>Back</button>' +
            '<button type="button" class="btn btn-primary ms-auto" data-steps-next>Next <i class="fa-solid fa-arrow-right ms-1"></i></button>';
        if (finish) finish.parentNode.insertBefore(nav, finish); else form.appendChild(nav);

        function show(index) {
            current = Math.max(0, Math.min(steps.length - 1, index));
            steps.forEach(function (s, i) { s.hidden = i !== current; });
            head.querySelector("[data-steps-count]").textContent = "Step " + (current + 1) + " of " + steps.length;
            head.querySelector("[data-steps-title]").textContent = steps[current].getAttribute("data-step");
            var bar = head.querySelector("[data-steps-bar]");
            bar.style.width = Math.round((current + 1) * 100 / steps.length) + "%";
            nav.querySelector("[data-steps-back]").hidden = current === 0;
            nav.querySelector("[data-steps-next]").hidden = current === steps.length - 1;
            if (finish) finish.hidden = current !== steps.length - 1;
        }

        nav.addEventListener("click", function (e) {
            var button = e.target.closest("button");
            if (!button) return;
            show(current + (button.hasAttribute("data-steps-next") ? 1 : -1));
            head.scrollIntoView({ behavior: "smooth", block: "start" });
        });
        show(0);
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("form[data-steps]").forEach(setUp);
    });
})();

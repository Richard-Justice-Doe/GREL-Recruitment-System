// docs/36 AI-6 (14 November 2026): the onboarding assistant chat (Views/Shared/_OnboardingAssistant.cshtml).
// Sends the question, the onboarding (or the new employee's application) and the last few turns to AiController.Onboarding, and
// shows the answer as plain text with up to three buttons. The button URLs come from GRSMS; only same-site paths are accepted.
// The chat lives in the page only (nothing stored); it is gone when the page is left.
(function () {
    "use strict";

    function setUp(box) {
        var form = box.querySelector(".assistant-form");
        var input = form.querySelector("input[name=question]");
        var button = form.querySelector("button[type=submit]");
        var log = box.querySelector(".assistant-log");
        var token = form.querySelector("input[name=__RequestVerificationToken]");
        var history = [];
        var busy = false;

        function bubble(cls, text) {
            var div = document.createElement("div");
            div.className = "assistant-msg " + cls;
            div.textContent = text;
            log.appendChild(div);
            div.scrollIntoView({ block: "nearest" });
            return div;
        }

        function safeUrl(url) {
            return typeof url === "string" && url.charAt(0) === "/" && url.charAt(1) !== "/" && url.charAt(1) !== "\\";
        }

        function ask(question) {
            question = (question || "").trim();
            if (!question || busy) return;
            busy = true;
            button.disabled = true;
            bubble("from-user", question);
            var waiting = bubble("from-ai is-waiting", "Thinking…");
            var data = new FormData();
            data.append("question", question);
            if (box.dataset.applicationId) data.append("applicationId", box.dataset.applicationId);
            if (box.dataset.onboardingId) data.append("onboardingId", box.dataset.onboardingId);
            data.append("history", JSON.stringify(history.slice(-6)));
            if (token) data.append("__RequestVerificationToken", token.value);
            fetch(box.dataset.assistantEndpoint, { method: "POST", body: data, credentials: "same-origin", headers: { "X-Requested-With": "fetch" } })
                .then(function (r) { if (!r.ok) throw new Error("HTTP " + r.status); return r.json(); })
                .then(function (res) {
                    waiting.classList.remove("is-waiting");
                    if (!res.ok) {
                        waiting.classList.add("is-problem");
                        waiting.textContent = res.problem || "The assistant is not available. Please use the page or ask the Recruitment Officer.";
                        return;
                    }
                    waiting.textContent = res.answer;
                    var actions = (res.actions || []).filter(function (a) { return a && safeUrl(a.url); });
                    if (actions.length) {
                        var row = document.createElement("div");
                        row.className = "assistant-actions";
                        actions.forEach(function (a) {
                            var link = document.createElement("a");
                            link.className = "btn btn-sm btn-outline-primary";
                            link.href = a.url;
                            link.textContent = a.label;
                            row.appendChild(link);
                        });
                        waiting.appendChild(row);
                    }
                    history.push({ u: true, t: question }, { u: false, t: res.answer });
                })
                .catch(function () {
                    waiting.classList.remove("is-waiting");
                    waiting.classList.add("is-problem");
                    waiting.textContent = "The assistant is not available right now. Please use the page or ask the Recruitment Officer.";
                })
                .finally(function () { busy = false; button.disabled = false; input.focus(); });
        }

        form.addEventListener("submit", function (e) {
            e.preventDefault();
            var q = input.value;
            input.value = "";
            ask(q);
        });
        box.querySelectorAll("[data-assistant-ask]").forEach(function (b) {
            b.addEventListener("click", function () { ask(b.getAttribute("data-assistant-ask")); });
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("[data-assistant]").forEach(setUp);
    });
})();

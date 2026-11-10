// Usability U3 (10 November 2026): a busy record page shown one topic at a time.
//
//   <div data-section-tabs> ... <div data-section="Documents">...</div> <div data-section="Checklist">...</div> ... </div>
//
// Builds a row of tabs ("Everything" plus one per topic, in page order) above the container and shows only the chosen topic's
// sections; anything without data-section (e.g. the "Next step" panel) always stays visible. The choice is remembered for the
// page during the browser session. Without JavaScript every section is shown, as before.
(function () {
    "use strict";

    function setUp(container) {
        var sections = Array.prototype.slice.call(container.querySelectorAll("[data-section]"));
        var names = [];
        sections.forEach(function (s) {
            var n = s.getAttribute("data-section");
            if (names.indexOf(n) < 0) names.push(n);
        });
        if (names.length < 2) return;

        var key = "grsms-tab:" + location.pathname;
        var saved = null;
        try { saved = sessionStorage.getItem(key); } catch (e) { /* storage blocked: start on the first topic */ }
        var all = "Everything";
        // The topic last chosen on this page (so saving a form keeps you where you were); on a first visit, the topic the page
        // says is current (e.g. the hiring case opens on its stage), else the first one.
        var preferred = container.getAttribute("data-section-default");
        var current = saved && (saved === all || names.indexOf(saved) >= 0) ? saved
            : preferred && names.indexOf(preferred) >= 0 ? preferred : names[0];

        var nav = document.createElement("ul");
        nav.className = "nav nav-pills mb-3 flex-nowrap overflow-auto pb-1";
        nav.setAttribute("role", "tablist");
        names.concat([all]).forEach(function (n) {
            var li = document.createElement("li");
            li.className = "nav-item";
            var b = document.createElement("button");
            b.type = "button";
            b.className = "nav-link text-nowrap";
            b.textContent = n;
            b.setAttribute("role", "tab");
            b.setAttribute("data-tab", n);
            li.appendChild(b);
            nav.appendChild(li);
        });
        container.parentNode.insertBefore(nav, container);

        function show(name) {
            current = name;
            sections.forEach(function (s) { s.hidden = name !== all && s.getAttribute("data-section") !== name; });
            nav.querySelectorAll("[data-tab]").forEach(function (b) {
                var on = b.getAttribute("data-tab") === name;
                b.classList.toggle("active", on);
                b.setAttribute("aria-selected", on ? "true" : "false");
            });
            try { sessionStorage.setItem(key, name); } catch (e) { /* not remembered: fine */ }
        }

        nav.addEventListener("click", function (e) {
            var b = e.target.closest("[data-tab]");
            if (b) show(b.getAttribute("data-tab"));
        });
        // A link to something inside a hidden section (e.g. #doc-12, or the next-step button's #send-offer) opens that section.
        function sectionOf(hash) {
            var target = hash && hash.length > 1 ? document.getElementById(hash.substring(1)) : null;
            var holder = target && target.closest("[data-section]");
            return holder && container.contains(holder) ? holder.getAttribute("data-section") : null;
        }
        if (sectionOf(location.hash)) current = sectionOf(location.hash);
        document.addEventListener("click", function (e) {
            var a = e.target.closest('a[href^="#"]');
            var name = a && sectionOf(a.getAttribute("href"));
            if (name && name !== current && current !== all) show(name);
        });
        show(current);
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("[data-section-tabs]").forEach(setUp);
    });
})();

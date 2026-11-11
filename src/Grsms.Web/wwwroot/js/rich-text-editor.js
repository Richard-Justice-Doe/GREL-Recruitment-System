// A small rich-text editor for long descriptions (e.g. "Description of the post" on a recruitment request).
//
// Markup (see Views/RecruitmentRequests/Form.cshtml):
//   <div data-rich-text data-max-chars="10000">
//     <div class="rte-toolbar" hidden> <button data-cmd="bold"> ... </div>
//     <div class="rte-editor" contenteditable="true" hidden></div>
//     <textarea class="rte-source"> (the field that is actually posted) </textarea>
//     <span data-rte-count></span>
//   </div>
//
// Without JavaScript the plain textarea is used. With it, the textarea is hidden and kept in step with the editor.
// The formatting is a small, safe subset (paragraphs, headings, bold, italic, underline, lists). The server cleans it
// again with an HTML sanitiser before saving (Infrastructure/Security/RichTextSanitizer.cs), so nothing here is trusted.
//
// The character counter counts text only (not formatting), exactly as the server does (RichText.CharacterCount).
(function () {
    "use strict";

    function textLength(el) {
        return (el.textContent || "").replace(/[\r\n]/g, "").length;
    }

    function isEmpty(el) {
        return textLength(el) === 0 && !el.querySelector("li");
    }

    function init(root) {
        var toolbar = root.querySelector(".rte-toolbar");
        var editor = root.querySelector(".rte-editor");
        var source = root.querySelector(".rte-source");
        var counter = root.querySelector("[data-rte-count]");
        var footer = counter ? counter.parentElement : null;
        var max = parseInt(root.getAttribute("data-max-chars"), 10) || 0;
        if (!toolbar || !editor || !source) return;

        // Show the editor, hide the plain box. Plain text (typed without the editor) becomes paragraphs.
        var initial = source.value || "";
        if (initial && !/<[a-z\/][^>]*>/i.test(initial)) {
            editor.innerHTML = "";
            initial.split(/\r?\n/).forEach(function (line) {
                if (!line.trim()) return;
                var p = document.createElement("p");
                p.textContent = line;      // textContent: the text is never interpreted as HTML
                editor.appendChild(p);
            });
        } else {
            editor.innerHTML = initial;    // already cleaned by the server's sanitiser when it was saved
        }
        toolbar.hidden = false;
        editor.hidden = false;
        source.hidden = true;
        source.setAttribute("aria-hidden", "true");
        source.tabIndex = -1;

        try { document.execCommand("defaultParagraphSeparator", false, "p"); } catch (e) { /* older browsers */ }

        function sync() {
            source.value = isEmpty(editor) ? "" : editor.innerHTML;
            var n = textLength(editor);
            if (counter) counter.textContent = n.toLocaleString("en-GB");
            var over = max > 0 && n > max;
            if (footer) {
                footer.classList.toggle("text-danger", over);
                footer.classList.toggle("text-body-secondary", !over);
            }
            editor.classList.toggle("is-invalid", over);
            editor.setAttribute("aria-invalid", over ? "true" : "false");
        }

        function refreshButtons() {
            toolbar.querySelectorAll("[data-cmd]").forEach(function (b) {
                var cmd = b.getAttribute("data-cmd");
                if (["bold", "italic", "underline", "insertUnorderedList", "insertOrderedList"].indexOf(cmd) < 0) return;
                var on = false;
                try { on = document.queryCommandState(cmd); } catch (e) { on = false; }
                b.classList.toggle("active", on);
                b.setAttribute("aria-pressed", on ? "true" : "false");
            });
        }

        toolbar.addEventListener("mousedown", function (e) {
            // Keep the text selection in the editor when a toolbar button is clicked.
            if (e.target.closest("[data-cmd]")) e.preventDefault();
        });
        toolbar.addEventListener("click", function (e) {
            var b = e.target.closest("[data-cmd]");
            if (!b) return;
            editor.focus();
            var cmd = b.getAttribute("data-cmd");
            var value = b.getAttribute("data-value");
            document.execCommand(cmd, false, value ? "<" + value + ">" : null);
            sync();
            refreshButtons();
        });

        // Pasting keeps the text and its line breaks but drops outside formatting (Word styles, fonts, colours, links).
        editor.addEventListener("paste", function (e) {
            var data = e.clipboardData || window.clipboardData;
            if (!data) return;
            e.preventDefault();
            document.execCommand("insertText", false, data.getData("text/plain"));
        });
        // Dropped files or HTML would bypass the toolbar; keep it to typed and pasted text.
        editor.addEventListener("drop", function (e) { e.preventDefault(); });

        editor.addEventListener("input", sync);
        editor.addEventListener("keyup", refreshButtons);
        editor.addEventListener("mouseup", refreshButtons);
        var form = root.closest("form");
        if (form) form.addEventListener("submit", sync);
        sync();
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("[data-rich-text]").forEach(init);
    });
})();

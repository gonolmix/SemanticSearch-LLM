(function () {
    "use strict";

    // Защита на уровне файла: второй <script> не зарегистрирует второй обработчик
    if (window.__ssSiteJsLoaded) return;
    window.__ssSiteJsLoaded = true;

    document.addEventListener("DOMContentLoaded", function () {
        collapseLongAbstracts(700);
        highlightQueryWords();
        animateCards();
    });

    function escapeHtml(s) {
        return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    }

    function wordsOf(query) {
        return (query || "").toLowerCase().split(/[^a-z0-9-]+/)
            .filter(function (w) { return w.length > 3; });
    }

    function highlightElement(p, words) {
        var html = escapeHtml(p.textContent);
        words.forEach(function (w) {
            html = html.replace(new RegExp("(" + w + ")", "gi"), "<mark>$1</mark>");
        });
        p.innerHTML = html;
    }

    function highlightQueryWords() {
        var container = document.getElementById("results-container");
        if (!container) return;
        var words = wordsOf(container.dataset.query);
        if (!words.length) return;
        container.querySelectorAll(".result-text").forEach(function (p) {
            highlightElement(p, words);
        });
    }

    function collapseLongAbstracts(limit) {
        var container = document.getElementById("results-container");
        if (!container) return;
        var words = wordsOf(container.dataset.query);

        container.querySelectorAll(".result-text").forEach(function (p) {
            if (p.dataset.collapsed === "1") return;   // идемпотентность: уже свёрнут — не трогаем
            var full = p.textContent;
            if (full.length <= limit) return;

            var short = full.slice(0, limit).trimEnd() + "…";
            p.textContent = short;
            p.dataset.collapsed = "1";                 // помечаем параграф
            p.dataset.full = full;                     // запоминаем оригинал

            var btn = document.createElement("span");
            btn.className = "toggle-abstract ms-2";
            btn.textContent = "Show more ▾";
            btn.addEventListener("click", function () {
                var expanded = btn.dataset.expanded === "1";
                p.textContent = expanded ? short : p.dataset.full;
                highlightElement(p, words);
                btn.textContent = expanded ? "Show more ▾" : "Collapse ▴";
                btn.dataset.expanded = expanded ? "0" : "1";
            });
            p.parentNode.insertBefore(btn, p.nextSibling);
        });
    }

    function animateCards() {
        document.querySelectorAll(".result-card").forEach(function (card, i) {
            card.style.opacity = "0";
            card.style.transform = "translateY(8px)";
            setTimeout(function () {
                card.style.transition = "opacity .3s ease, transform .3s ease";
                card.style.opacity = "1";
                card.style.transform = "translateY(0)";
            }, 80 * i);
        });
    }
})();
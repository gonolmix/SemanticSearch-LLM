(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        if (window.__ssSiteJsLoaded) return;
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

    // Подсветка слов запроса в тексте результата
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

    // Сворачивание длинных аннотаций
    function collapseLongAbstracts(limit) {
        var container = document.getElementById("results-container");
        if (!container) return;
        var words = wordsOf(container.dataset.query);

        container.querySelectorAll(".result-text").forEach(function (p) {
            if (p.dataset.full) return; 
            var full = p.textContent;
            if (full.length <= limit) return;

            var short = full.slice(0, limit).trimEnd() + "…";
            p.textContent = short;

            var btn = document.createElement("span");
            btn.className = "toggle-abstract ms-2";
            btn.textContent = "Show more ▾";
            btn.addEventListener("click", function () {
                var expanded = btn.dataset.expanded === "1";
                p.textContent = expanded ? short : full;
                highlightElement(p, words);
                btn.textContent = expanded ? "Show more ▾" : "Collapse ▴";
                btn.dataset.expanded = expanded ? "0" : "1";
            });
            p.parentNode.insertBefore(btn, p.nextSibling);
        });
    }

    // Плавное появление карточек
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
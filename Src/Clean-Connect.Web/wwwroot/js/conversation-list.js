document.addEventListener("DOMContentLoaded", function () {
    var searchInput = document.getElementById("threadSearchInput");
    var clearButton = document.getElementById("threadSearchClear");
    var tabs = Array.from(document.querySelectorAll(".cc-messenger-filters [data-filter]"));
    var rows = Array.from(document.querySelectorAll("[data-thread-item]"));
    var empty = document.getElementById("filterEmptyState");
    var activeFilter = "all";

    try {
        rows.forEach(function (row) {
            var preview = row.querySelector("[data-thread-preview]");
            var cached = localStorage.getItem("cc_msg_preview_" + row.dataset.threadBookingId);
            if (!cached || !preview) return;
            var data = JSON.parse(cached);
            if (data && data.text) preview.textContent = (data.direction === "sent" ? "You: " : "") + data.text;
        });
    } catch (_) { }

    function applyFilters() {
        var query = (searchInput?.value || "").trim().toLowerCase();
        if (clearButton) clearButton.hidden = !query;
        var visible = 0;
        rows.forEach(function (row) {
            var matchesFilter = activeFilter === "all" || row.dataset.filterCategory === activeFilter;
            var matchesSearch = !query || (row.dataset.searchCorpus || "").includes(query);
            row.hidden = !(matchesFilter && matchesSearch);
            if (!row.hidden) visible++;
        });
        if (empty) empty.hidden = visible > 0;
    }

    searchInput?.addEventListener("input", applyFilters);
    clearButton?.addEventListener("click", function () { searchInput.value = ""; searchInput.focus(); applyFilters(); });
    tabs.forEach(function (tab) {
        tab.addEventListener("click", function () {
            activeFilter = tab.dataset.filter || "all";
            tabs.forEach(function (item) {
                var selected = item === tab;
                item.classList.toggle("is-active", selected);
                item.setAttribute("aria-selected", String(selected));
            });
            applyFilters();
        });
    });
});

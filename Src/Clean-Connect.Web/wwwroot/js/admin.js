// ==========================================================================
// CLEAN CONNECT ADMIN UI - JAVASCRIPT CONTROLLER
// Handles Sidebar, Live Search/Filters, Modals, Empty/Skeleton States, Settings
// ==========================================================================

document.addEventListener("DOMContentLoaded", function () {

    // --------------------------------------------------------------------------
    // 1. Mobile Sidebar Toggle & Overlay
    // --------------------------------------------------------------------------
    const sidebarToggleBtn = document.getElementById("adminSidebarToggle");
    const sidebar = document.getElementById("adminSidebar");
    const overlay = document.getElementById("sidebarOverlay");

    if (sidebarToggleBtn && sidebar && overlay) {
        sidebarToggleBtn.addEventListener("click", function () {
            sidebar.classList.toggle("show");
            overlay.classList.toggle("show");
        });

        overlay.addEventListener("click", function () {
            sidebar.classList.remove("show");
            overlay.classList.remove("show");
        });
    }

    // --------------------------------------------------------------------------
    // 2. Global Search Shortcut (Cmd+K / Ctrl+K)
    // --------------------------------------------------------------------------
    const globalSearch = document.getElementById("globalAdminSearch");
    window.addEventListener("keydown", function (e) {
        if ((e.ctrlKey || e.metaKey) && e.key === "k") {
            e.preventDefault();
            if (globalSearch) {
                globalSearch.focus();
                globalSearch.select();
            }
        }
    });

    // --------------------------------------------------------------------------
    // 3. Live Table Filter & Search Engine
    // --------------------------------------------------------------------------
    const searchInputs = document.querySelectorAll("[data-table-search]");
    const filterSelects = document.querySelectorAll("[data-table-filter]");

    function filterTable(tableId) {
        const table = document.getElementById(tableId);
        if (!table) return;

        const tbody = table.querySelector("tbody");
        if (!tbody) return;

        const searchInput = document.querySelector(`[data-table-search="${tableId}"]`);
        const query = searchInput ? searchInput.value.trim().toLowerCase() : "";

        const filters = Array.from(document.querySelectorAll(`[data-table-filter="${tableId}"]`));

        const rows = Array.from(tbody.querySelectorAll("tr:not(.skeleton-row)"));
        let visibleCount = 0;

        rows.forEach(row => {
            const textContent = row.textContent.toLowerCase();
            let matchesQuery = !query || textContent.includes(query);
            let matchesFilters = true;

            filters.forEach(filter => {
                const val = filter.value;
                if (!val || val === "all") return;

                const colIndex = parseInt(filter.getAttribute("data-filter-col") || "-1", 10);
                if (colIndex >= 0 && row.children[colIndex]) {
                    const cellText = row.children[colIndex].textContent.trim().toLowerCase();
                    if (!cellText.includes(val.toLowerCase())) {
                        matchesFilters = false;
                    }
                }
            });

            if (matchesQuery && matchesFilters) {
                row.style.display = "";
                visibleCount++;
            } else {
                row.style.display = "none";
            }
        });

        // Update counter if present
        const counter = document.getElementById(`${tableId}-counter`);
        if (counter) {
            counter.textContent = `Showing ${visibleCount} of ${rows.length} records`;
        }

        // Auto-show empty state if 0 matches
        const inlineEmpty = document.getElementById(`${tableId}-no-results`);
        if (inlineEmpty) {
            inlineEmpty.style.display = (visibleCount === 0 && rows.length > 0) ? "table-row" : "none";
        }

        // Reset to page 1 and re-paginate among the filtered rows
        const paginationWrapper = document.querySelector(`[data-paginate="${tableId}"]`);
        if (paginationWrapper && paginationWrapper.__ccPage) {
            paginationWrapper.__ccPage.current = 1;
            paginationWrapper.__ccPage.render();
        }
    }

    searchInputs.forEach(input => {
        const tableId = input.getAttribute("data-table-search");
        input.addEventListener("input", () => filterTable(tableId));
    });

    filterSelects.forEach(select => {
        const tableId = select.getAttribute("data-table-filter");
        select.addEventListener("change", () => filterTable(tableId));
    });

    // --------------------------------------------------------------------------
    // 4. Reset Filters Button
    // --------------------------------------------------------------------------
    document.querySelectorAll("[data-reset-filter]").forEach(btn => {
        btn.addEventListener("click", function () {
            const tableId = this.getAttribute("data-reset-filter");
            const searchInput = document.querySelector(`[data-table-search="${tableId}"]`);
            if (searchInput) searchInput.value = "";

            document.querySelectorAll(`[data-table-filter="${tableId}"]`).forEach(sel => {
                sel.value = "all";
            });

            filterTable(tableId);
        });
    });

    // --------------------------------------------------------------------------
    // 5. Empty State & Skeleton Loading Demo Toggles
    // --------------------------------------------------------------------------
    document.querySelectorAll("[data-toggle-empty-state]").forEach(btn => {
        btn.addEventListener("click", function () {
            const containerId = this.getAttribute("data-toggle-empty-state");
            const dataContainer = document.getElementById(`${containerId}-data`);
            const emptyContainer = document.getElementById(`${containerId}-empty`);

            if (dataContainer && emptyContainer) {
                const isCurrentlyEmpty = emptyContainer.classList.contains("d-none");
                if (isCurrentlyEmpty) {
                    dataContainer.classList.add("d-none");
                    emptyContainer.classList.remove("d-none");
                    this.innerHTML = '<i class="fa-solid fa-table me-1"></i> View Data';
                } else {
                    emptyContainer.classList.add("d-none");
                    dataContainer.classList.remove("d-none");
                    this.innerHTML = '<i class="fa-solid fa-folder-open me-1"></i> Demo Empty State';
                }
            }
        });
    });

    document.querySelectorAll("[data-toggle-skeleton]").forEach(btn => {
        btn.addEventListener("click", function () {
            const tableId = this.getAttribute("data-toggle-skeleton");
            const table = document.getElementById(tableId);
            if (!table) return;

            const tbody = table.querySelector("tbody");
            if (!tbody) return;

            const realRows = tbody.querySelectorAll("tr:not(.skeleton-row)");
            const skeletonRows = tbody.querySelectorAll(".skeleton-row");

            realRows.forEach(r => r.classList.add("d-none"));
            skeletonRows.forEach(r => r.classList.remove("d-none"));

            btn.disabled = true;
            btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Loading...';

            setTimeout(() => {
                skeletonRows.forEach(r => r.classList.add("d-none"));
                realRows.forEach(r => r.classList.remove("d-none"));
                btn.disabled = false;
                btn.innerHTML = '<i class="fa-solid fa-spinner me-1"></i> Demo Skeleton State';
            }, 1200);
        });
    });

    // --------------------------------------------------------------------------
    // 6. Real Client-Side Pagination
    // --------------------------------------------------------------------------
    document.querySelectorAll("[data-paginate]").forEach(wrapper => {
        const table = document.getElementById(wrapper.getAttribute("data-paginate"));
        if (!table) return;
        const tbody = table.querySelector("tbody");
        if (!tbody) return;
        const pageSize = parseInt(wrapper.getAttribute("data-page-size") || "6", 10);

        const pag = {
            current: 1,
            render: function renderPagination() {
                const rows = Array.from(tbody.querySelectorAll("tr")).filter(r =>
                    !r.classList.contains("skeleton-row") &&
                    r.id !== (table.id + "-no-results") &&
                    !r.hidden &&
                    r.style.display !== "none"
                );

                const count = rows.length;
                const totalPages = Math.max(1, Math.ceil(count / pageSize));
                if (pag.current > totalPages) pag.current = totalPages;

                const start = (pag.current - 1) * pageSize;
                rows.forEach((row, i) => {
                    row.style.display = (i >= start && i < start + pageSize) ? "" : "none";
                });

                const info = wrapper.querySelector("[data-pagination-info]");
                if (info) {
                    info.textContent = count === 0
                        ? "Showing 0 records"
                        : totalPages === 1 ? `Showing all ${count} records`
                        : `Showing ${start + 1}\u2013${Math.min(start + pageSize, count)} of ${count} records`;
                }

                const list = wrapper.querySelector("[data-pagination-list]");
                if (!list || count === 0) {
                    if (list) list.innerHTML = "";
                    return;
                }

                list.innerHTML = "";

                function addButton(label, page, opts) {
                    const li = document.createElement("li");
                    const btn = document.createElement("button");
                    btn.type = "button";
                    btn.className = "page-btn" + (opts.active ? " active" : "");
                    btn.disabled = !!opts.disabled;
                    btn.innerHTML = label;
                    btn.addEventListener("click", function () {
                        if (btn.disabled) return;
                        pag.current = page;
                        pag.render();
                    });

                    li.appendChild(btn);
                    list.appendChild(li);
                }

                addButton('<i class="fa-solid fa-chevron-left"></i>', pag.current - 1, { disabled: pag.current === 1 });

                const pages = [];
                for (let p = 1; p <= totalPages; p++) {
                    if (p === 1 || p === totalPages || (p >= pag.current - 1 && p <= pag.current + 1)) {
                        pages.push(p);
                    } else if (pages[pages.length - 1] !== "ellipsis") {
                        pages.push("ellipsis");
                    }
                }
                pages.forEach(p => {
                    if (p === "ellipsis") {
                        addButton("\u2026", pag.current, { disabled: true });
                    } else {
                        addButton(String(p), p, { active: p === pag.current });
                    }
                });

                addButton('<i class="fa-solid fa-chevron-right"></i>', pag.current + 1, { disabled: pag.current === totalPages });
            }
        };

        wrapper.__ccPage = pag;
        pag.render();
    });

    // --------------------------------------------------------------------------
    // 7. Admin Notifications Bell (live feed from real platform data)
    // --------------------------------------------------------------------------
    const adminNotifList = document.getElementById("adminNotifList");
    if (adminNotifList) {
        const badge = document.getElementById("adminNotifBadge");
        const indicator = document.getElementById("notifDropdown") && document.getElementById("notifDropdown").querySelector(".notification-indicator");

        const variantClass = {
            emerald: "bg-success-subtle text-success",
            sky: "bg-info-subtle text-info",
            amber: "bg-warning-subtle text-warning",
            rose: "bg-danger-subtle text-danger"
        };

        function renderEmpty(msg) {
            adminNotifList.innerHTML = '<div class="notif-empty text-center py-3"><i class="fa-solid fa-bell-slash text-muted"></i><div class="small text-muted mt-1">' + msg + "</div></div>";
        }

        fetch("/Admin/Notifications/Feed", { headers: { "X-Requested-With": "XMLHttpRequest" } })
            .then(r => {
                if (!r.ok) throw new Error("feed failed");
                return r.json();
            })
            .then(items => {
                if (!items || items.length === 0) {
                    renderEmpty("No activity yet on the platform.");
                    return;
                }

                adminNotifList.innerHTML = "";
                if (indicator) indicator.style.display = "block";
                if (badge) {
                    badge.textContent = items.length + " new";
                    badge.classList.remove("d-none");
                }

                items.forEach(item => {
                    const a = document.createElement("a");
                    a.href = item.href;
                    a.className = "notif-item unread";

                    const icon = document.createElement("div");
                    icon.className = "notif-icon " + (variantClass[item.variant] || variantClass.sky);
                    icon.innerHTML = '<i class="fa-solid ' + (item.icon || "fa-bell") + '"></i>';

                    const content = document.createElement("div");
                    content.className = "notif-content";

                    const title = document.createElement("div");
                    title.className = "notif-title";
                    title.textContent = item.title;

                    const desc = document.createElement("div");
                    desc.className = "notif-desc";
                    desc.textContent = item.message;

                    const time = document.createElement("div");
                    time.className = "notif-time";
                    time.textContent = item.timeAgo;

                    content.appendChild(title);
                    content.appendChild(desc);
                    content.appendChild(time);
                    a.appendChild(icon);
                    a.appendChild(content);
                    adminNotifList.appendChild(a);
                });
            })
            .catch(() => renderEmpty("Could not load notifications."));
    }

    // --------------------------------------------------------------------------
    // 7. Settings Tab Switching
    // --------------------------------------------------------------------------
    const settingsNavItems = document.querySelectorAll(".settings-nav-item");
    const settingsPanels = document.querySelectorAll(".settings-panel");

    if (settingsNavItems.length > 0 && settingsPanels.length > 0) {
        settingsNavItems.forEach(item => {
            item.addEventListener("click", function (e) {
                e.preventDefault();
                const targetId = this.getAttribute("data-target");

                settingsNavItems.forEach(n => n.classList.remove("active"));
                this.classList.add("active");

                settingsPanels.forEach(panel => {
                    if (panel.id === targetId) {
                        panel.classList.remove("d-none");
                    } else {
                        panel.classList.add("d-none");
                    }
                });
            });
        });
    }

    // --------------------------------------------------------------------------
    // 8. Mock Form Save Feedback
    // --------------------------------------------------------------------------
    document.querySelectorAll("[data-save-feedback]").forEach(btn => {
        btn.addEventListener("click", function () {
            const originalHtml = this.innerHTML;
            this.disabled = true;
            this.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span> Saving...';

            setTimeout(() => {
                this.disabled = false;
                this.innerHTML = '<i class="fa-solid fa-check me-2"></i> Saved Successfully!';
                this.classList.replace("btn-admin-primary", "btn-success");

                setTimeout(() => {
                    this.innerHTML = originalHtml;
                    this.classList.replace("btn-success", "btn-admin-primary");
                }, 2200);
            }, 700);
        });
    });

});

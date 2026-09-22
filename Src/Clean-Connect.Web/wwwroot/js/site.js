// ==========================================
// THEME TOGGLE (Light / Dark)
// ==========================================

(function () {

    var STORAGE_KEY = "clean-connect-theme";

    function getStoredTheme() {
        return localStorage.getItem(STORAGE_KEY);
    }

    function setStoredTheme(theme) {
        localStorage.setItem(STORAGE_KEY, theme);
    }

    function getPreferredTheme() {
        var stored = getStoredTheme();
        if (stored) return stored;
        return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
    }

    function applyTheme(theme) {
        document.documentElement.setAttribute("data-theme", theme);
        var icon = document.getElementById("themeIcon");
        var text = document.getElementById("themeText");
        if (icon) {
            icon.className = theme === "dark" ? "fa-solid fa-sun" : "fa-solid fa-moon";
        }
        if (text) {
            text.textContent = theme === "dark" ? "Light" : "Dark";
        }
    }

    function toggleTheme() {
        var current = document.documentElement.getAttribute("data-theme") || "light";
        var next = current === "dark" ? "light" : "dark";
        setStoredTheme(next);
        applyTheme(next);
    }

document.addEventListener("DOMContentLoaded", function () {
        applyTheme(getPreferredTheme());

        var toggle = document.getElementById("themeToggle");
        if (toggle) {
            toggle.addEventListener("click", toggleTheme);
        }

        // ── Navbar scroll glass effect ──
        var navbar = document.querySelector(".navbar");
        if (navbar) {
            function onScroll() {
                if (window.scrollY > 20) {
                    navbar.classList.add("is-scrolled");
                } else {
                    navbar.classList.remove("is-scrolled");
                }
            }
            window.addEventListener("scroll", onScroll, { passive: true });
            onScroll(); // run once on load in case page is already scrolled

            // ── Solid background while the mobile menu is open ──
            var collapseEl = navbar.querySelector(".navbar-collapse");
            if (collapseEl) {
                collapseEl.addEventListener("show.bs.collapse", function () {
                    navbar.classList.add("navbar--menu-open");
                    navbar.classList.remove("is-scrolled");
                });
                collapseEl.addEventListener("hidden.bs.collapse", function () {
                    navbar.classList.remove("navbar--menu-open");
                    onScroll();
                });
            }
        }

        // ── Active nav link highlight ──
        var currentPath = window.location.pathname.toLowerCase();
        document.querySelectorAll(".navbar .nav-link").forEach(function (link) {
            var href = link.getAttribute("href");
            if (!href) return;
            var linkPath = href.toLowerCase().split("?")[0];
            // Exact match, or starts with the path (for sub-routes)
            // But avoid marking "/" active for every page
            if (linkPath === "/" ? currentPath === "/" : currentPath === linkPath || currentPath.startsWith(linkPath + "/")) {
                link.classList.add("active");
                link.setAttribute("aria-current", "page");
            }
        });
    });

document.addEventListener("DOMContentLoaded", function () {
    if (!window.signalR) {
        return;
    }

    updateNotificationBadge();

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/notifications")
        .withAutomaticReconnect()
        .build();

    connection.on("bookingNotification", function (payload) {
        showRealtimeNotification(payload);

        var badge = document.getElementById("notificationBadge");
        if (badge && parseInt(badge.dataset.count || "0", 10) >= 0) {
            bumpNotificationBadge(1);
        }
    });

    connection.start().catch(function (error) {
        console.debug("Realtime notifications unavailable.", error);
    });
});

// ==========================================
// NOTIFICATION MARK-AS-READ (AJAX)
// ==========================================

document.addEventListener("DOMContentLoaded", function () {
    if (!document.querySelector(".notifications-page")) {
        return;
    }

    var token = document.querySelector('input[name="__RequestVerificationToken"]');

    function updateCountsAfterRead() {
        updateNotificationBadge();
    }

    function markRead(id) {
        var body = new URLSearchParams();
        if (token) body.append("__RequestVerificationToken", token.value);
        return fetch("/Notifications/MarkAsRead?id=" + encodeURIComponent(id), {
            method: "POST",
            headers: { "X-Requested-With": "XMLHttpRequest" },
            body: body
        });
    }

    function handleRowClick(row) {
        var url = row.dataset.actionUrl;
        var id = row.dataset.notificationId;
        var isUnread = row.classList.contains("is-unread");

        if (!isUnread || !id) {
            if (url) window.location.href = url;
            return;
        }

        markRead(id).then(function () {
            row.classList.remove("needs-attention", "is-unread");
            row.classList.add("is-read");
            updateCountsAfterRead();
            if (url) window.location.href = url;
        }).catch(function (err) {
            console.debug("Mark read failed.", err);
            if (url) window.location.href = url;
        });
    }

    document.querySelectorAll(".notification-row").forEach(function (row) {
        row.addEventListener("click", function () {
            handleRowClick(row);
        });
        row.addEventListener("keydown", function (e) {
            if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                handleRowClick(row);
            }
        });
    });

    var markAllForm = document.querySelector("#markAllReadBtn")?.closest("form");
    if (markAllForm) {
        markAllForm.addEventListener("submit", function (e) {
            e.preventDefault();
            fetch(markAllForm.action, {
                method: "POST",
                body: new URLSearchParams(new FormData(markAllForm)),
                headers: { "X-Requested-With": "XMLHttpRequest" }
            }).then(function (res) {
                if (!res.ok) throw new Error("mark all read failed");
                document.querySelectorAll(".notification-row").forEach(function (row) {
                    row.classList.remove("needs-attention", "is-unread");
                    row.classList.add("is-read");
                });
                markAllForm.remove();
                updateCountsAfterRead();
            }).catch(function (err) {
                console.debug("Mark all as read failed.", err);
            });
        });
    }
});

// ==========================================
// NOTIFICATION BADGE (UNREAD COUNT)
// ==========================================

function renderNotificationBadge(count) {
    var badge = document.getElementById("notificationBadge");
    if (!badge) return;

    badge.dataset.count = count;
    badge.textContent = count > 99 ? "99+" : count;

    if (count > 0) {
        badge.classList.add("is-visible");
    } else {
        badge.classList.remove("is-visible");
    }
}

function updateNotificationBadge() {
    fetch("/Notifications/UnreadCount", { headers: { "X-Requested-With": "XMLHttpRequest" } })
        .then(function (res) { return res.json(); })
        .then(function (data) {
            renderNotificationBadge(parseInt(data.count || "0", 10));
        })
        .catch(function () {
            /* badge stays hidden if the endpoint is unavailable */
        });
}

function bumpNotificationBadge(by) {
    var badge = document.getElementById("notificationBadge");
    if (!badge) return;
    var current = parseInt(badge.dataset.count || "0", 10);
    renderNotificationBadge(current + by);
}

function showRealtimeNotification(payload) {
    const notification = document.createElement("div");
    notification.className = `realtime-toast realtime-toast--${payload.type || "info"}`;

    const title = document.createElement("strong");
    title.textContent = payload.title || "Clean Connect";

    const message = document.createElement("span");
    message.textContent = payload.message || "You have a new update.";

    notification.appendChild(title);
    notification.appendChild(message);

    if (payload.url) {
        const link = document.createElement("a");
        link.href = payload.url;
        link.textContent = "View";
        notification.appendChild(link);
    }

    document.body.appendChild(notification);

    requestAnimationFrame(function () {
        notification.classList.add("is-visible");
    });

    setTimeout(function () {
        notification.classList.remove("is-visible");
        setTimeout(function () {
            notification.remove();
        }, 250);
    }, 6500);
}

})();

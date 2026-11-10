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

        // ── Navbar scroll glass effect & responsive resize handling ──
        var navbar = document.querySelector(".navbar");
        if (navbar) {
            var MOBILE_BREAKPOINT = 992;
            var collapseEl = navbar.querySelector(".navbar-collapse");
            var toggler = navbar.querySelector(".navbar-toggler");

            function onScroll() {
                if (window.scrollY > 20) {
                    navbar.classList.add("is-scrolled");
                } else {
                    navbar.classList.remove("is-scrolled");
                }
            }
            window.addEventListener("scroll", onScroll, { passive: true });
            onScroll(); // run once on load in case page is already scrolled

            // Helper to close mobile menu cleanly
            function closeMobileMenu() {
                if (!collapseEl) return;
                if (window.bootstrap && window.bootstrap.Collapse) {
                    var bsCollapse = window.bootstrap.Collapse.getInstance(collapseEl);
                    if (bsCollapse) {
                        bsCollapse.hide();
                    }
                }
                collapseEl.classList.remove("show", "collapsing");
                collapseEl.style.height = "";
                if (toggler) {
                    toggler.classList.add("collapsed");
                    toggler.setAttribute("aria-expanded", "false");
                }
                navbar.classList.remove("navbar--menu-open");
                onScroll();
            }

            // ── Solid background while the mobile menu is open ──
            if (collapseEl) {
                collapseEl.addEventListener("show.bs.collapse", function () {
                    navbar.classList.add("navbar--menu-open");
                    navbar.classList.remove("is-scrolled");
                });
                collapseEl.addEventListener("hidden.bs.collapse", function () {
                    navbar.classList.remove("navbar--menu-open");
                    onScroll();
                });

                // Close mobile menu when nav links are clicked
                collapseEl.querySelectorAll(".nav-link:not(.dropdown-toggle), .btn-login, .btn-register").forEach(function (el) {
                    el.addEventListener("click", function () {
                        if (window.innerWidth < MOBILE_BREAKPOINT && collapseEl.classList.contains("show")) {
                            closeMobileMenu();
                        }
                    });
                });

                // Close mobile menu when clicking outside navbar
                document.addEventListener("click", function (e) {
                    if (window.innerWidth < MOBILE_BREAKPOINT && collapseEl.classList.contains("show")) {
                        if (!navbar.contains(e.target)) {
                            closeMobileMenu();
                        }
                    }
                });
            }

            // ── Responsive window resize handler ──
            var resizeTimer = null;
            function onWindowResize() {
                // Prevent transition flicker/jank during resize
                document.body.classList.add("is-resizing");
                clearTimeout(resizeTimer);
                resizeTimer = setTimeout(function () {
                    document.body.classList.remove("is-resizing");
                }, 120);

                // If resized to desktop breakpoint or wider, cleanly close mobile dropdown state
                if (window.innerWidth >= MOBILE_BREAKPOINT) {
                    if (collapseEl && (collapseEl.classList.contains("show") || collapseEl.classList.contains("collapsing") || navbar.classList.contains("navbar--menu-open"))) {
                        closeMobileMenu();
                    }
                }
            }

            window.addEventListener("resize", onWindowResize, { passive: true });
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
    updateMessagesBadge();

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/notifications")
        .withAutomaticReconnect()
        .build();

    window.__ccSignalRConnection = connection;

    connection.on("bookingNotification", function (payload) {
        showRealtimeNotification(payload);

        var badge = document.getElementById("notificationBadge");
        if (badge && parseInt(badge.dataset.count || "0", 10) >= 0) {
            bumpNotificationBadge(1);
        }
    });

    const processedMessageNotificationIds = new Set();

    connection.on("messageNotification", function (payload) {
        if (!payload) return;

        var msgId = payload.messageId || (payload.bookingId + "_" + (payload.createdAt || Date.now()));
        if (processedMessageNotificationIds.has(msgId)) {
            return;
        }
        processedMessageNotificationIds.add(msgId);
        if (processedMessageNotificationIds.size > 200) {
            var first = processedMessageNotificationIds.values().next().value;
            processedMessageNotificationIds.delete(first);
        }

        // Exclude notification toast if user is currently inside this active booking chat
        var activeChatEl = document.querySelector("[data-booking-chat-page][data-booking-id='" + payload.bookingId + "']");
        if (activeChatEl) {
            return;
        }

        showRealtimeMessageToast(payload);

        if (typeof payload.unreadCount === "number") {
            renderMessagesBadge(payload.unreadCount);
        } else {
            bumpMessagesBadge(1);
        }

        updateThreadRowRealtime(payload);
    });

    connection.on("updateUnreadMessageCount", function (payload) {
        if (payload && typeof payload.unreadCount === "number") {
            renderMessagesBadge(payload.unreadCount);
        }
    });

    connection.onreconnected(function () {
        updateNotificationBadge();
        updateMessagesBadge();
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

// ==========================================
// REAL-TIME MESSAGES (BADGE & TOAST)
// ==========================================

function renderMessagesBadge(count) {
    var numericCount = parseInt(count, 10);
    if (isNaN(numericCount) || numericCount < 0) {
        numericCount = 0;
    }

    var badges = document.querySelectorAll("[data-messages-badge], #messagesUnreadBadge");
    badges.forEach(function (badge) {
        badge.dataset.count = numericCount;
        badge.textContent = numericCount > 99 ? "99+" : numericCount;

        if (numericCount > 0) {
            badge.hidden = false;
            badge.removeAttribute("hidden");
            badge.classList.add("is-visible");
        } else {
            badge.hidden = true;
            badge.setAttribute("hidden", "");
            badge.classList.remove("is-visible");
        }
    });

    window.__ccUnreadMessageCount = numericCount;
}

function updateMessagesBadge() {
    fetch("/Booking/Messages/UnreadCount", {
        headers: { "X-Requested-With": "XMLHttpRequest" }
    })
    .then(function (res) {
        if (!res.ok) throw new Error("Status " + res.status);
        return res.json();
    })
    .then(function (data) {
        var count = typeof data.count === "number" ? data.count : parseInt(data.count || "0", 10);
        renderMessagesBadge(count);
    })
    .catch(function () {
        /* badge stays hidden if endpoint is unavailable or user not logged in */
    });
}

function bumpMessagesBadge(by) {
    var current = typeof window.__ccUnreadMessageCount === "number"
        ? window.__ccUnreadMessageCount
        : 0;
    var badge = document.querySelector("[data-messages-badge], #messagesUnreadBadge");
    if (badge && badge.dataset.count) {
        current = parseInt(badge.dataset.count, 10) || current;
    }
    renderMessagesBadge(current + by);
}

function showRealtimeMessageToast(payload) {
    var toast = document.createElement("div");
    toast.className = "realtime-toast realtime-toast--message";
    toast.setAttribute("role", "alert");
    toast.setAttribute("aria-live", "polite");

    var title = document.createElement("strong");
    var sender = payload.senderName || "New message";
    title.textContent = sender;

    var message = document.createElement("span");
    var preview = payload.messagePreview || "You received a new message.";
    message.textContent = preview.length > 90 ? preview.substring(0, 87) + "..." : preview;

    toast.appendChild(title);
    toast.appendChild(message);

    var linkUrl = payload.url || (payload.bookingId ? "/Booking/" + payload.bookingId + "/Messages" : "/Booking/Messages");
    var link = document.createElement("a");
    link.href = linkUrl;
    link.textContent = "Reply \u2192";
    toast.appendChild(link);

    toast.addEventListener("click", function (e) {
        if (e.target.tagName !== "A") {
            window.location.href = linkUrl;
        }
    });

    document.body.appendChild(toast);

    requestAnimationFrame(function () {
        toast.classList.add("is-visible");
    });

    setTimeout(function () {
        toast.classList.remove("is-visible");
        setTimeout(function () {
            toast.remove();
        }, 300);
    }, 6500);
}

function updateThreadRowRealtime(payload) {
    if (!payload || !payload.bookingId) return;

    var threadList = document.getElementById("threadList");
    var threadEl = document.querySelector("[data-thread-booking-id='" + payload.bookingId + "']");
    if (!threadEl) return;

    // Update preview text
    var previewEl = threadEl.querySelector("[data-thread-preview]");
    if (previewEl && payload.messagePreview) {
        var preview = payload.messagePreview;
        previewEl.textContent = preview.length > 90 ? preview.substring(0, 87) + "..." : preview;
        previewEl.classList.add("is-realtime");
    }

    // Update time
    var timeEl = threadEl.querySelector("[data-thread-time]");
    if (timeEl) {
        timeEl.textContent = "Just now";
        timeEl.setAttribute("datetime", new Date().toISOString());
    }

    // Update unread count for this thread
    var currentThreadUnread = parseInt(threadEl.getAttribute("data-thread-unread") || "0", 10) || 0;
    var threadUnread = typeof payload.threadUnreadCount === "number"
        ? payload.threadUnreadCount
        : currentThreadUnread + 1;
    var metaEl = threadEl.querySelector(".booking-message-thread__meta") || threadEl;
    var pillEl = threadEl.querySelector("[data-thread-unread-pill]") || threadEl.querySelector(".cc-messenger-thread__unread");

    if (threadUnread !== null) {
        threadEl.setAttribute("data-thread-unread", threadUnread);
        if (threadUnread > 0) {
            threadEl.classList.add("has-unread");
            if (!pillEl && metaEl) {
                pillEl = document.createElement("span");
                pillEl.className = threadEl.classList.contains("cc-messenger-thread") ? "cc-messenger-thread__unread" : "booking-thread-unread-pill";
                pillEl.setAttribute("data-thread-unread-pill", "");
                var statusBadge = metaEl.querySelector(".status-badge");
                if (statusBadge) {
                    metaEl.insertBefore(pillEl, statusBadge);
                } else {
                    metaEl.prepend(pillEl);
                }
            }
            if (pillEl) {
                pillEl.textContent = threadUnread > 99 ? "99+" : threadUnread;
                pillEl.hidden = false;
            }
        } else {
            threadEl.classList.remove("has-unread");
            if (pillEl) {
                pillEl.remove();
            }
        }
    }

    // Move thread to top of list if not already
    if (threadList && threadList.firstElementChild !== threadEl) {
        threadList.insertBefore(threadEl, threadList.firstElementChild);
    }
}

window.renderMessagesBadge = renderMessagesBadge;
window.updateMessagesBadge = updateMessagesBadge;
window.bumpMessagesBadge = bumpMessagesBadge;
window.showRealtimeMessageToast = showRealtimeMessageToast;
window.updateThreadRowRealtime = updateThreadRowRealtime;

})();


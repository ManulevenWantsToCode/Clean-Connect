const sdkUrl = "https://cdn.jsdelivr.net/npm/agora-chat@1.4.0/+esm";

document.querySelectorAll("[data-booking-chat-page]").forEach((root) => {
    const bookingId = root.dataset.bookingId;
    const sessionUrl = root.dataset.sessionUrl;
    const canSendMessages = root.dataset.canSend !== "false";
    const token = root.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
    const transcript = root.querySelector("[data-chat-transcript]");
    const emptyState = root.querySelector("[data-chat-empty]");
    const loadingState = root.querySelector("[data-chat-loading]");
    const errorState = root.querySelector("[data-chat-error]");
    const errorText = root.querySelector("[data-chat-error-text]");
    const retryButton = root.querySelector("[data-chat-retry]");
    const status = root.querySelector("[data-chat-status]");
    const form = root.querySelector("[data-chat-form]");
    const input = root.querySelector("[data-chat-input]");
    const sendButton = root.querySelector("[data-chat-send]");
    const unreadBadge = root.querySelector("[data-chat-unread]");
    const unreadCount = root.querySelector("[data-chat-unread-count]");

    let AgoraChat = null;
    let connection = null;
    let session = null;
    let renderedIds = new Set();
    let newMessageDividerAdded = false;
    let otherLastReadAt = 0;
    let historyLoaded = false;
    let pendingLiveMessages = [];

    bindReadReceiptListener();

    retryButton?.addEventListener("click", () => connect());

    input?.addEventListener("input", () => {
        input.style.height = "auto";
        input.style.height = `${Math.min(input.scrollHeight, 140)}px`;
    });

    form?.addEventListener("submit", async (event) => {
        event.preventDefault();
        await sendMessage();
    });

    connect();

    async function connect() {
        showLoading();

        try {
            session = await fetchSession();
            otherLastReadAt = parseTimestamp(session.otherParticipantLastReadAtUtc);
            AgoraChat = AgoraChat || await loadSdk();
            connection = new AgoraChat.connection({ appId: session.appId });
            bindConnectionEvents();

            await connection.open({
                user: session.currentUserProviderId,
                accessToken: session.accessToken
            });
        } catch (error) {
            console.error("Agora Chat connection failed", error);
            showError("Chat is not available right now.");
        }
    }

    function bindConnectionEvents() {
        connection.addEventHandler(`booking-chat-${bookingId}`, {
            onConnected: async () => {
                setComposerEnabled(canSendMessages);
                setStatus("Connected");
                hideLoading();
                await loadUnreadCount();
                await loadHistory();
                flushPendingLiveMessages();
                await markConversationRead();
            },
            onDisconnected: () => {
                historyLoaded = false;
                setComposerEnabled(false);
                setStatus("Disconnected");
            },
            onTextMessage: (message) => {
                if (!isBookingMessage(message) || message.from === session.currentUserProviderId) {
                    return;
                }

                if (!historyLoaded) {
                    pendingLiveMessages.push(message);
                    return;
                }

                appendMessage(message, "received", { newMessage: !newMessageDividerAdded });
                newMessageDividerAdded = true;
                markConversationRead();
            },
            onTokenWillExpire: renewToken,
            onError: (error) => {
                console.error("Agora Chat error", error);
                setStatus("Connection issue");
            }
        });
    }

    async function fetchSession() {
        const response = await fetch(sessionUrl, {
            method: "POST",
            headers: {
                "Accept": "application/json",
                "RequestVerificationToken": token
            },
            credentials: "same-origin"
        });

        if (!response.ok) {
            throw new Error(`Chat session failed with status ${response.status}`);
        }

        return await response.json();
    }

    async function loadSdk() {
        const module = await import(sdkUrl);
        const candidates = [module?.websdk, module?.default?.websdk, module?.default, module, window.WebIM];
        const sdk = candidates.find((candidate) => candidate && typeof candidate.connection === "function");

        if (!sdk) {
            throw new Error("Agora Chat SDK failed to load.");
        }

        return sdk;
    }

    async function renewToken() {
        try {
            const renewedSession = await fetchSession();
            session = renewedSession;
            if (connection?.renewToken) {
                connection.renewToken(renewedSession.accessToken);
            }
            setStatus("Connected");
        } catch (error) {
            console.error("Agora Chat token renewal failed", error);
            setStatus("Token renewal failed");
        }
    }

    async function loadHistory() {
        historyLoaded = false;
        const historyMethod = connection?.getHistoryMessages || connection?.fetchHistoryMessages;
        if (!historyMethod) {
            renderEmptyIfNeeded();
            setStatus("Connected. History is unavailable in this SDK build.");
            return;
        }

        try {
            const result = await historyMethod.call(connection, {
                targetId: session.otherParticipantProviderId,
                chatType: session.chatType || "singleChat",
                pageSize: 30,
                searchDirection: "up"
            });

            const messages = extractMessages(result)
                .filter(isBookingMessage)
                .sort((a, b) => messageTime(a) - messageTime(b));

            messages.forEach((message) => {
                appendMessage(
                    message,
                    message.from === session.currentUserProviderId ? "sent" : "received",
                    { preserveScroll: true });
            });

            renderEmptyIfNeeded();
            transcript.scrollTop = transcript.scrollHeight;
        } catch (error) {
            console.error("Agora Chat history load failed", error);
            renderEmptyIfNeeded();
            setStatus("Connected. Message history could not be loaded.");
        }
    }

    function flushPendingLiveMessages() {
        historyLoaded = true;
        pendingLiveMessages
            .sort((a, b) => messageTime(a) - messageTime(b))
            .forEach((message) => {
                appendMessage(message, "received", { newMessage: !newMessageDividerAdded });
                newMessageDividerAdded = true;
            });
        pendingLiveMessages = [];
    }

    async function loadUnreadCount() {
        if (!connection?.getConversationlist) {
            return;
        }

        try {
            const result = await connection.getConversationlist({ pageNum: 1, pageSize: 50 });
            const conversations = Array.isArray(result?.data) ? result.data : Array.isArray(result) ? result : [];
            const conversation = conversations.find((item) => {
                const id = item.conversationId || item.channel_id || item.name || item.id;
                return id === session.otherParticipantProviderId || id === session.conversationId;
            });

            const count = Number(conversation?.unread_num ?? conversation?.unreadCount ?? conversation?.unread ?? 0);
            if (count > 0 && unreadBadge && unreadCount) {
                unreadCount.textContent = count.toString();
                unreadBadge.hidden = false;
            }
        } catch (error) {
            console.debug("Agora Chat unread count is unavailable", error);
        }
    }

    async function markConversationRead() {
        try {
            if (connection?.markConversationRead) {
                await connection.markConversationRead(session.otherParticipantProviderId, session.chatType || "singleChat");
            } else if (connection?.setConversationRead) {
                await connection.setConversationRead(session.otherParticipantProviderId);
            }

            if (unreadBadge) {
                unreadBadge.hidden = true;
            }
        } catch (error) {
            console.debug("Agora Chat mark-as-read is unavailable", error);
        }

        try {
            if (window.__ccSignalRConnection && window.__ccSignalRConnection.state === "Connected") {
                const count = await window.__ccSignalRConnection.invoke("MarkConversationRead", session.bookingId);
                if (typeof window.renderMessagesBadge === "function" && typeof count === "number") {
                    window.renderMessagesBadge(count);
                }
            } else {
                const formData = new FormData();
                if (token) {
                    formData.append("__RequestVerificationToken", token);
                }
                const res = await fetch(`/Booking/${session.bookingId}/messages/read`, {
                    method: "POST",
                    headers: {
                        "X-Requested-With": "XMLHttpRequest"
                    },
                    body: formData,
                    credentials: "same-origin"
                });
                if (res.ok) {
                    const data = await res.json();
                    if (typeof window.renderMessagesBadge === "function" && typeof data.count === "number") {
                        window.renderMessagesBadge(data.count);
                    }
                }
            }
        } catch (backendError) {
            console.debug("Backend mark conversation read failed", backendError);
        }
    }

    async function notifyBackendMessageSent(messageText, messageId) {
        try {
            if (window.__ccSignalRConnection && window.__ccSignalRConnection.state === "Connected") {
                await window.__ccSignalRConnection.invoke("SendChatMessageNotification", session.bookingId, messageText, messageId || null);
                return;
            }
        } catch (hubError) {
            console.debug("SignalR notification invoke failed, using HTTP fallback", hubError);
        }

        try {
            const formData = new FormData();
            formData.append("MessageText", messageText);
            if (messageId) {
                formData.append("MessageId", messageId);
            }
            if (token) {
                formData.append("__RequestVerificationToken", token);
            }

            await fetch(`/Booking/${session.bookingId}/messages/notify`, {
                method: "POST",
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: formData,
                credentials: "same-origin"
            });
        } catch (httpError) {
            console.debug("HTTP message notification failed", httpError);
        }
    }

    async function sendMessage() {
        const text = input.value.trim();
        if (!canSendMessages || !text || !connection || !session || !AgoraChat) {
            return;
        }

        setSending(true);

        try {
            const message = AgoraChat.message.create({
                chatType: session.chatType || "singleChat",
                type: "txt",
                to: session.otherParticipantProviderId,
                msg: text,
                ext: {
                    bookingId: session.bookingId,
                    conversationId: session.conversationId
                }
            });

            const result = await connection.send(message);
            appendMessage({ ...message, ...result, msg: text, time: Date.now(), from: session.currentUserProviderId }, "sent", { live: true });
            input.value = "";
            input.style.height = "auto";
            renderEmptyIfNeeded();
            notifyBackendMessageSent(text, message.id || result?.id || result?.serverMsgId);
        } catch (error) {
            console.error("Agora Chat send failed", error);
            showError("Your message could not be sent. Please try again.");
        } finally {
            setSending(false);
        }
    }

    function appendMessage(message, direction, options = {}) {
        const id = message.id || message.mid || message.msg_id || `${message.from || direction}-${message.time || message.serverTime || message.msg}-${direction}`;
        if (renderedIds.has(id)) {
            return;
        }

        renderedIds.add(id);
        emptyState.hidden = true;

        try {
            const previewText = message.msg || message.data || "";
            if (previewText && session?.bookingId) {
                localStorage.setItem(`cc_msg_preview_${session.bookingId}`, JSON.stringify({
                    text: previewText.length > 110 ? previewText.substring(0, 110) + "..." : previewText,
                    time: messageTime(message),
                    direction: direction
                }));
            }
        } catch (e) {
            // LocalStorage errors ignored in restricted contexts
        }

        const sentAt = messageTime(message);
        const ackMarkup = direction === "sent"
            ? '<span class="booking-chat-bubble__ack" data-chat-ack aria-label="Sent"><i class="fa-solid fa-check" aria-hidden="true"></i></span>'
            : "";
        const bubble = document.createElement("article");
        bubble.className = `booking-chat-bubble booking-chat-bubble--${direction}`;
        bubble.dataset.sentAt = String(sentAt);
        bubble.innerHTML = `
            <div class="booking-chat-bubble__body">${escapeHtml(message.msg || message.data || "")}</div>
            <div class="booking-chat-bubble__meta">
                <time datetime="${new Date(sentAt).toISOString()}">${formatTime(sentAt)}</time>
                ${ackMarkup}
            </div>
        `;

        const nextBubble = Array.from(transcript.querySelectorAll(".booking-chat-bubble"))
            .find((existingBubble) => Number(existingBubble.dataset.sentAt || 0) > sentAt);

        if (options.newMessage && !newMessageDividerAdded) {
            const divider = document.createElement("div");
            divider.className = "booking-chat-new-divider";
            divider.setAttribute("role", "separator");
            divider.innerHTML = '<span>New message</span>';
            transcript.insertBefore(divider, nextBubble || null);
            newMessageDividerAdded = true;
        }

        transcript.insertBefore(bubble, nextBubble || null);
        if (direction === "sent" && !options.live) {
            updateAckState(bubble);
        }
        if (!options.preserveScroll) {
            transcript.scrollTop = transcript.scrollHeight;
        }
    }

    function updateAckState(bubble) {
        const sentAt = Number(bubble.dataset.sentAt || 0);
        setAckState(bubble, Boolean(otherLastReadAt && sentAt && sentAt <= otherLastReadAt));
    }

    function setAckState(bubble, seen) {
        const ack = bubble.querySelector("[data-chat-ack]");
        if (!ack) {
            return;
        }

        ack.classList.toggle("is-seen", seen);
        ack.setAttribute("aria-label", seen ? "Seen" : "Sent");
        ack.innerHTML = `<i class="fa-solid ${seen ? "fa-check-double" : "fa-check"}" aria-hidden="true"></i>`;
    }

    function parseTimestamp(value) {
        if (!value) {
            return 0;
        }

        const time = new Date(value).getTime();
        return Number.isFinite(time) ? time : 0;
    }

    function bindReadReceiptListener() {
        let bound = false;

        const attach = () => {
            const hub = window.__ccSignalRConnection;
            if (!hub || bound) {
                return Boolean(hub);
            }

            hub.on("conversationRead", (payload) => {
                if (!payload || String(payload.bookingId).toLowerCase() !== String(bookingId).toLowerCase()) {
                    return;
                }

                if (session && payload.readerRole === session.participantRole) {
                    return;
                }

                otherLastReadAt = Math.max(otherLastReadAt, parseTimestamp(payload.readAt) || Date.now());
                transcript.querySelectorAll(".booking-chat-bubble--sent").forEach((bubble) => {
                    setAckState(bubble, true);
                });
            });

            bound = true;
            return true;
        };

        if (attach()) {
            return;
        }

        let attempts = 0;
        const timer = setInterval(() => {
            attempts += 1;
            if (attach() || attempts >= 40) {
                clearInterval(timer);
            }
        }, 250);
    }

    function extractMessages(result) {
        if (Array.isArray(result)) {
            return result;
        }

        if (Array.isArray(result?.messages)) {
            return result.messages;
        }

        if (Array.isArray(result?.data)) {
            return result.data;
        }

        if (Array.isArray(result?.data?.messages)) {
            return result.data.messages;
        }

        return [];
    }

    function isBookingMessage(message) {
        if (!message) {
            return false;
        }

        if (message.ext?.bookingId && message.ext.bookingId !== session.bookingId) {
            return false;
        }

        const fromCurrentPair =
            message.from === session.currentUserProviderId ||
            message.from === session.otherParticipantProviderId ||
            message.to === session.currentUserProviderId ||
            message.to === session.otherParticipantProviderId;

        return Boolean(message.msg || message.data) && fromCurrentPair;
    }

    function messageTime(message) {
        const rawTime = message.time ?? message.serverTime ?? message.timestamp ?? Date.now();
        const numericTime = Number(rawTime);
        const timestamp = Number.isFinite(numericTime)
            ? numericTime
            : Date.parse(rawTime);

        if (!Number.isFinite(timestamp) || timestamp <= 0) {
            return Date.now();
        }

        // Agora history may expose epoch seconds while live messages use milliseconds.
        return timestamp < 1_000_000_000_000 ? timestamp * 1000 : timestamp;
    }

    function renderEmptyIfNeeded() {
        const hasMessages = transcript.querySelector(".booking-chat-bubble");
        emptyState.hidden = Boolean(hasMessages);
    }

    function showLoading() {
        loadingState.hidden = false;
        errorState.hidden = true;
        setComposerEnabled(false);
        setStatus("Connecting...");
    }

    function hideLoading() {
        loadingState.hidden = true;
    }

    function showError(message) {
        hideLoading();
        errorText.textContent = message;
        errorState.hidden = false;
        setComposerEnabled(false);
        setStatus("Disconnected");
    }

    function setComposerEnabled(enabled) {
        if (input) input.disabled = !enabled;
        if (sendButton) sendButton.disabled = !enabled;
    }

    function setSending(sending) {
        if (sendButton) sendButton.disabled = sending || !canSendMessages;
    }

    function setStatus(message) {
        status.textContent = message;
    }

    function formatTime(value) {
        return new Intl.DateTimeFormat(undefined, {
            hour: "numeric",
            minute: "2-digit"
        }).format(new Date(value));
    }

    function escapeHtml(value) {
        const span = document.createElement("span");
        span.textContent = value;
        return span.innerHTML;
    }
});

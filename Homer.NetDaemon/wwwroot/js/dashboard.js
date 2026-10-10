// Client-side helpers for the living room dashboard. Anything that ticks or follows a finger runs here, so the
// Nest Hub never waits on a server round trip for it.

const pad = n => String(n).padStart(2, "0");

function tick() {
    const now = new Date();

    const clock = `${pad(now.getHours())}:${pad(now.getMinutes())}`;
    for (const el of document.querySelectorAll("[data-clock]")) {
        if (el.textContent !== clock) el.textContent = clock;
    }

    for (const el of document.querySelectorAll("[data-countdown]")) {
        const seconds = Math.max(0, Math.round((Date.parse(el.dataset.countdown) - now) / 1000));
        const text = `${pad(Math.floor(seconds / 60))}:${pad(seconds % 60)}`;
        if (el.textContent !== text) el.textContent = text;
    }
}

let started = false;

export function start() {
    if (started) return;
    started = true;

    tick();
    setInterval(tick, 1000);
    watchReconnect();
}

// Page dots follow the scroll position, and the stack drifts back to the most relevant page after a while.
export function initStack(stack) {
    if (!stack || stack.dataset.ready) return;
    stack.dataset.ready = "1";

    let idle;
    const update = () => {
        const page = Math.round(stack.scrollTop / Math.max(1, stack.clientHeight));
        stack.parentElement.querySelectorAll(".dot").forEach((dot, i) => dot.classList.toggle("is-active", i === page));

        clearTimeout(idle);
        if (stack.scrollTop > 0) {
            idle = setTimeout(() => stack.scrollTo({ top: 0, behavior: "smooth" }), 45000);
        }
    };

    stack.addEventListener("scroll", update, { passive: true });
    update();
}

// A vertical blind slider: 0 at the top (open) to 1 at the bottom (closed). Only the release goes to the server.
export function initBlind(track, dotnet, index) {
    if (!track || track.dataset.ready) return;
    track.dataset.ready = "1";

    const label = track.closest(".blind")?.querySelector("[data-percent]");
    const valueAt = e => {
        const rect = track.getBoundingClientRect();
        return Math.min(1, Math.max(0, (e.clientY - rect.top) / rect.height));
    };
    const show = v => {
        track.style.setProperty("--drag", v.toFixed(3));
        if (label) label.textContent = describe(v);
    };

    track.addEventListener("pointerdown", e => {
        track.setPointerCapture(e.pointerId);
        track.classList.add("is-dragging");
        show(valueAt(e));
    });

    track.addEventListener("pointermove", e => {
        if (track.classList.contains("is-dragging")) show(valueAt(e));
    });

    track.addEventListener("pointerup", e => {
        if (!track.classList.contains("is-dragging")) return;
        track.classList.remove("is-dragging");
        dotnet.invokeMethodAsync("OnBlindReleased", index, valueAt(e));
    });

    track.addEventListener("pointercancel", () => track.classList.remove("is-dragging"));
}

function describe(v) {
    const percent = Math.round(v * 100);
    return percent <= 2 ? "全开" : percent >= 98 ? "全关" : `关 ${percent}%`;
}

// The hub is a kiosk: if the server went away for good, reload instead of leaving a dead page up.
function watchReconnect() {
    const modal = document.getElementById("components-reconnect-modal");
    if (!modal) return;

    new MutationObserver(() => {
        if (modal.classList.contains("components-reconnect-failed") ||
            modal.classList.contains("components-reconnect-rejected")) {
            setTimeout(() => location.reload(), 5000);
        }
    }).observe(modal, { attributes: true, attributeFilter: ["class"] });
}

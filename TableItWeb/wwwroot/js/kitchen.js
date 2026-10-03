(function () {
    'use strict';

    const WARN_MIN = 15, DANGER_MIN = 25;
    const COLS = ['New', 'InProgress', 'Ready'];
    const NEXT = {
        New: { status: 'InProgress', label: 'Start', cls: 'btn-primary' },
        InProgress: { status: 'Ready', label: 'Done', cls: 'btn-warning' },
        Ready: { status: 'Served', label: 'Served', cls: 'btn-success' }
    };
    const esc = TableIt.escapeHtml;

    const orders = new Map();
    const flashing = new Set();
    let soundOn = false;
    let audioCtx = null;
    let wasReconnecting = false;

    const $ = function (id) { return document.getElementById(id); };

    // ---------- storage helpers ----------
    function lsGet(k) { try { return localStorage.getItem(k); } catch (e) { return null; } }
    function lsSet(k, v) { try { localStorage.setItem(k, v); } catch (e) { /* ignore */ } }

    // ---------- toast ----------
    function showError(msg) {
        const holder = $('k-toasts');
        const el = document.createElement('div');
        el.className = 'toast align-items-center text-bg-danger border-0';
        el.setAttribute('role', 'alert');
        el.innerHTML = '<div class="d-flex"><div class="toast-body fs-5">' + esc(msg) +
            '</div><button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button></div>';
        holder.appendChild(el);
        if (window.bootstrap && bootstrap.Toast) {
            const t = new bootstrap.Toast(el, { delay: 6000 });
            el.addEventListener('hidden.bs.toast', function () { el.remove(); });
            t.show();
        } else {
            setTimeout(function () { el.remove(); }, 6000);
        }
    }

    // ---------- sound ----------
    function ensureAudio() {
        try {
            if (!audioCtx) {
                const AC = window.AudioContext || window.webkitAudioContext;
                if (!AC) return null;
                audioCtx = new AC();
            }
            if (audioCtx.state === 'suspended') audioCtx.resume();
        } catch (e) { return null; }
        return audioCtx;
    }

    function chime() {
        const ctx = ensureAudio();
        if (!ctx) return;
        try {
            const now = ctx.currentTime;
            [880, 1175].forEach(function (freq, i) {
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.type = 'sine';
                osc.frequency.value = freq;
                const t0 = now + i * 0.18;
                gain.gain.setValueAtTime(0.0001, t0);
                gain.gain.exponentialRampToValueAtTime(0.35, t0 + 0.02);
                gain.gain.exponentialRampToValueAtTime(0.0001, t0 + 0.4);
                osc.connect(gain).connect(ctx.destination);
                osc.start(t0);
                osc.stop(t0 + 0.45);
            });
        } catch (e) { /* ignore */ }
    }

    function renderSound() {
        const b = $('k-sound');
        b.textContent = 'Sound: ' + (soundOn ? 'on' : 'off');
        b.setAttribute('aria-pressed', String(soundOn));
        b.classList.toggle('btn-light', soundOn);
        b.classList.toggle('btn-outline-light', !soundOn);
    }

    // ---------- rendering ----------
    function elapsedText(min) {
        if (min < 1) return '<1 min';
        if (min < 60) return min + ' min';
        return Math.floor(min / 60) + 'h ' + (min % 60) + 'm';
    }

    function cardClass(o) {
        let cls = 'k-card';
        if (o.status === 'New' || o.status === 'InProgress') {
            const m = TableIt.minutesSince(o.createdAt);
            if (m >= DANGER_MIN) cls += ' danger';
            else if (m >= WARN_MIN) cls += ' warn';
        }
        if (flashing.has(o.id)) cls += ' flash';
        return cls;
    }

    function cardHtml(o) {
        const lines = (o.lines || []).map(function (l) {
            return '<li><span class="k-qty">' + esc(l.quantity) + ' &times;</span> ' + esc(l.name) +
                (l.note ? '<div class="k-line-note">' + esc(l.note) + '</div>' : '') + '</li>';
        }).join('');
        const next = NEXT[o.status];
        const cancel = (o.status === 'New' || o.status === 'InProgress')
            ? '<button type="button" class="btn btn-outline-danger btn-cancel" data-act="cancel" title="Cancel order" aria-label="Cancel order">&times;</button>' : '';
        return '<div class="' + cardClass(o) + '" data-id="' + esc(o.id) + '">' +
            '<div class="k-card-head"><div><div class="k-table">Table ' + esc(o.tableNumber) + '</div>' +
            '<div class="k-meta">Order #' + esc(o.id) + '</div></div>' +
            '<div class="k-elapsed" data-created="' + esc(o.createdAt) + '">' + esc(elapsedText(TableIt.minutesSince(o.createdAt))) + '</div></div>' +
            '<ul class="k-lines">' + lines + '</ul>' +
            (o.note ? '<div class="k-note">' + esc(o.note) + '</div>' : '') +
            '<div class="k-actions"><button type="button" class="btn ' + next.cls + ' btn-main" data-act="advance">' + esc(next.label) + '</button>' + cancel + '</div>' +
            '</div>';
    }

    function render() {
        const counts = { New: 0, InProgress: 0, Ready: 0, Served: 0 };
        const buckets = { New: [], InProgress: [], Ready: [] };
        orders.forEach(function (o) {
            if (counts[o.status] !== undefined) counts[o.status]++;
            if (buckets[o.status]) buckets[o.status].push(o);
        });
        Object.keys(counts).forEach(function (k) { $('cnt-' + k).textContent = counts[k]; });
        COLS.forEach(function (c) {
            const list = buckets[c].sort(function (a, b) {
                return new Date(a.createdAt) - new Date(b.createdAt) || a.id - b.id;
            });
            $('badge-' + c).textContent = list.length;
            $('body-' + c).innerHTML = list.length
                ? list.map(cardHtml).join('')
                : '<div class="k-empty">No orders</div>';
        });
    }

    function tickElapsed() {
        document.querySelectorAll('.k-card').forEach(function (card) {
            const o = orders.get(Number(card.dataset.id));
            if (!o) return;
            const el = card.querySelector('.k-elapsed');
            if (el) el.textContent = elapsedText(TableIt.minutesSince(o.createdAt));
            card.classList.remove('warn', 'danger');
            if (o.status === 'New' || o.status === 'InProgress') {
                const m = TableIt.minutesSince(o.createdAt);
                if (m >= DANGER_MIN) card.classList.add('danger');
                else if (m >= WARN_MIN) card.classList.add('warn');
            }
        });
    }

    function tickClock() {
        $('k-clock').textContent = new Date().toLocaleTimeString('da-DK', { hour: '2-digit', minute: '2-digit' });
    }

    // ---------- actions ----------
    async function setStatus(id, status, buttons) {
        buttons.forEach(function (b) { b.disabled = true; });
        try {
            const updated = await TableIt.api.patch('/api/orders/' + id + '/status', { status: status });
            if (updated) { orders.set(updated.id, updated); render(); }
        } catch (e) {
            showError('Could not update order #' + id + ': ' + (e.message || e));
            buttons.forEach(function (b) { b.disabled = false; });
        }
    }

    document.addEventListener('click', function (ev) {
        const btn = ev.target.closest('button[data-act]');
        if (!btn) return;
        const card = btn.closest('.k-card');
        if (!card) return;
        const id = Number(card.dataset.id);
        const o = orders.get(id);
        if (!o) return;
        const all = Array.prototype.slice.call(card.querySelectorAll('button'));
        if (btn.dataset.act === 'advance') {
            setStatus(id, NEXT[o.status].status, all);
        } else if (btn.dataset.act === 'cancel') {
            if (confirm('Cancel order #' + o.id + ' for table ' + o.tableNumber + '?')) {
                setStatus(id, 'Cancelled', all);
            }
        }
    });

    $('k-sound').addEventListener('click', function () {
        soundOn = !soundOn;
        lsSet('kitchen.sound', soundOn ? '1' : '0');
        if (soundOn) { ensureAudio(); chime(); }
        renderSound();
    });

    function applyCollapsed(c) {
        $('col-Ready').classList.toggle('collapsed', c);
    }
    $('k-collapse').addEventListener('click', function () {
        const c = !$('col-Ready').classList.contains('collapsed');
        applyCollapsed(c);
        lsSet('kitchen.readyCollapsed', c ? '1' : '0');
    });

    // ---------- data ----------
    async function load() {
        try {
            const list = await TableIt.api.get('/api/orders');
            orders.clear();
            (list || []).forEach(function (o) { orders.set(o.id, o); });
            render();
        } catch (e) {
            showError('Could not load orders: ' + (e.message || e));
        }
    }

    function onCreated(o) {
        const isNew = !orders.has(o.id);
        orders.set(o.id, o);
        if (isNew && o.status === 'New') {
            flashing.add(o.id);
            setTimeout(function () { flashing.delete(o.id); render(); }, 4500);
            if (soundOn) chime();
        }
        render();
    }

    function onUpdated(o) {
        orders.set(o.id, o);
        render();
    }

    function onStatus(state) {
        const dot = $('k-dot');
        dot.className = 'k-dot ' + state;
        $('k-conn-text').textContent = state === 'connected' ? 'Live' : state === 'reconnecting' ? 'Reconnecting...' : 'Offline';
        if (state === 'connected' && wasReconnecting) load();
        wasReconnecting = state !== 'connected';
    }

    // ---------- init ----------
    soundOn = lsGet('kitchen.sound') === '1';
    // Audio cannot play before a user gesture; show the saved preference but unlock on first click anywhere.
    renderSound();
    document.addEventListener('click', function () { if (soundOn) ensureAudio(); }, { once: true });
    applyCollapsed(lsGet('kitchen.readyCollapsed') === '1');
    tickClock();
    setInterval(tickClock, 1000);
    setInterval(tickElapsed, 30000);
    load();
    TableIt.connectHub({ OrderCreated: onCreated, OrderUpdated: onUpdated, onStatus: onStatus });
})();

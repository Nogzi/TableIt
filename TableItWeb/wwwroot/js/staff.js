(function () {
    'use strict';
    const T = window.TableIt;
    const esc = T.escapeHtml;
    const root = document.getElementById('staff-app');
    const toastBox = document.getElementById('st-toasts');

    let tables = [];
    let menu = [];
    let orders = new Map();
    let conn = 'disconnected';
    let loaded = false;
    let loadError = null;
    let sending = false;
    let sheetOpen = false;
    let flashId = null;
    let lastRoute = null;
    let draft = newDraft(null);

    function newDraft(tableId) { return { tableId: tableId, lines: [], note: '' }; }

    // ---------- helpers ----------
    function toast(msg, kind, ms) {
        const el = document.createElement('div');
        el.className = 'st-toast ' + (kind || 'info');
        el.textContent = msg;
        toastBox.appendChild(el);
        setTimeout(function () { el.remove(); }, ms || 3000);
    }
    const OPEN = ['New', 'InProgress', 'Ready'];
    const isOpen = function (o) { return OPEN.indexOf(o.status) >= 0; };
    function orderTotal(o) { return o.lines.reduce(function (s, l) { return s + l.unitPrice * l.quantity; }, 0); }
    function fmtTime(iso) { return new Date(iso).toLocaleTimeString('da-DK', { hour: '2-digit', minute: '2-digit' }); }
    function tableById(id) { return tables.find(function (t) { return t.id === id; }); }
    function menuById(id) { return menu.find(function (m) { return m.id === id; }); }
    function draftTotal() {
        return draft.lines.reduce(function (s, l) { const m = menuById(l.menuItemId); return s + (m ? m.price * l.quantity : 0); }, 0);
    }
    function draftCount() { return draft.lines.reduce(function (s, l) { return s + l.quantity; }, 0); }

    function route() {
        const h = location.hash.replace(/^#/, '');
        let m = /^table\/(\d+)\/new$/.exec(h);
        if (m) return { name: 'builder', id: +m[1] };
        m = /^table\/(\d+)$/.exec(h);
        if (m) return { name: 'table', id: +m[1] };
        return { name: 'tables' };
    }
    function go(hash) { location.hash = hash; }

    const connDot = function () { return '<span class="st-conn ' + conn + '" title="' + conn + '"></span>'; };

    // ---------- render ----------
    function render() {
        if (!loaded) {
            root.innerHTML = loadError
                ? '<div class="alert alert-danger mt-3">' + esc(loadError) + '</div><button class="btn btn-primary btn-lg" data-act="reload">Retry</button>'
                : '<div class="text-center text-muted py-5">Loading...</div>';
            return;
        }
        const r = route();
        if (r.name !== 'tables' && !tableById(r.id)) { go(''); return; }
        if (r.name === 'builder') renderBuilder(r.id);
        else if (r.name === 'table') renderTable(r.id);
        else renderTables();
    }

    function renderTables() {
        let html = '<div class="st-top"><h1>Tables</h1>' + connDot() + '</div>';
        if (!tables.length) html += '<p class="text-muted">No tables configured.</p>';
        html += '<div class="st-grid">';
        tables.forEach(function (t) {
            const open = Array.from(orders.values()).filter(function (o) { return o.tableId === t.id && isOpen(o); });
            let top = null;
            ['Ready', 'InProgress', 'New'].some(function (s) {
                if (open.some(function (o) { return o.status === s; })) { top = s; return true; }
                return false;
            });
            html += '<button class="st-table' + (top === 'Ready' ? ' has-Ready' : '') + '" data-act="open-table" data-id="' + t.id + '">' +
                '<span class="num">' + esc(t.number) + '</span><span class="seats">' + esc(t.seats) + ' seats</span>' +
                (open.length ? '<span class="badge rounded-pill st-badge-' + top + '">' + open.length + '</span>' : '') +
                '</button>';
        });
        html += '</div>';
        root.innerHTML = html;
    }

    function statusPill(s) { return '<span class="st-pill st-pill-' + esc(s) + '">' + esc(s === 'InProgress' ? 'In progress' : s) + '</span>'; }

    function renderTable(id) {
        const t = tableById(id);
        const list = Array.from(orders.values()).filter(function (o) { return o.tableId === id; })
            .sort(function (a, b) { return new Date(b.createdAt) - new Date(a.createdAt); });
        let html = '<div class="st-top"><button class="btn btn-outline-secondary st-back" data-act="home" aria-label="Back">&larr;</button>' +
            '<h1>Table ' + esc(t.number) + '</h1>' + connDot() + '</div>' +
            '<button class="btn btn-primary btn-lg w-100 my-3 py-3" data-act="new-order">+ New order</button>';
        if (!list.length) html += '<p class="text-muted text-center">No orders tonight.</p>';
        list.forEach(function (o) {
            html += '<div class="st-order ' + esc(o.status) + '"><div class="d-flex justify-content-between align-items-center mb-1">' +
                '<strong>' + fmtTime(o.createdAt) + '</strong>' + statusPill(o.status) + '</div>';
            o.lines.forEach(function (l) {
                html += '<div>' + esc(l.quantity) + ' &times; ' + esc(l.name) +
                    (l.note ? '<div class="st-note ms-3">' + esc(l.note) + '</div>' : '') + '</div>';
            });
            if (o.note) html += '<div class="st-note mt-1">Note: ' + esc(o.note) + '</div>';
            html += '<div class="d-flex justify-content-between align-items-center mt-2"><strong>' + esc(T.formatPrice(orderTotal(o))) + '</strong><div>';
            if (o.status === 'New') html += '<button class="btn btn-sm btn-outline-danger me-2" data-act="cancel" data-id="' + o.id + '">Cancel</button>';
            if (o.status === 'Ready') html += '<button class="btn btn-success btn-lg" data-act="serve" data-id="' + o.id + '">Mark served</button>';
            html += '</div></div></div>';
        });
        root.innerHTML = html;
    }

    const COURSES = ['Starters', 'Mains', 'Desserts', 'Drinks'];
    function sortCats(cats) {
        const rank = function (c) { const i = COURSES.findIndex(function (x) { return x.toLowerCase() === String(c).toLowerCase(); }); return i < 0 ? 99 : i; };
        return cats.slice().sort(function (a, b) { return rank(a) - rank(b) || String(a).localeCompare(String(b)); });
    }

    function renderBuilder(id) {
        if (draft.tableId !== id) draft = newDraft(id);
        const t = tableById(id);
        const avail = menu.filter(function (m) { return m.isAvailable; });
        const cats = [];
        avail.forEach(function (m) { if (cats.indexOf(m.category) < 0) cats.push(m.category); });
        const prevSheet = root.querySelector('.st-sheet-body');
        const sheetScroll = prevSheet ? prevSheet.scrollTop : 0;
        const qtyOf = function (mid) { return draft.lines.filter(function (l) { return l.menuItemId === mid; }).reduce(function (s, l) { return s + l.quantity; }, 0); };

        let html = '<div class="st-top"><button class="btn btn-outline-secondary st-back" data-act="builder-back" aria-label="Back">&larr;</button>' +
            '<h1>New order &middot; Table ' + esc(t.number) + '</h1>' + connDot() + '</div>';
        if (!avail.length) html += '<p class="text-muted">No items available.</p>';
        sortCats(cats).forEach(function (c) {
            html += '<h3 class="st-cat">' + esc(c) + '</h3>';
            avail.filter(function (m) { return m.category === c; }).forEach(function (m) {
                const q = qtyOf(m.id);
                html += '<button class="st-item' + (flashId === m.id ? ' flash' : '') + '" data-act="add" data-id="' + m.id + '"><span>' + esc(m.name) +
                    (q ? ' <span class="badge rounded-pill bg-primary">&times;' + q + '</span>' : '') + '</span><strong>' + esc(T.formatPrice(m.price)) + '</strong></button>';
            });
        });
        flashId = null;

        const count = draftCount();
        html += '<div class="st-bar">';
        if (sheetOpen) {
            html += '<div class="st-sheet-body">';
            if (!draft.lines.length) html += '<p class="text-muted my-2">No items yet. Tap menu items to add them.</p>';
            draft.lines.forEach(function (l, i) {
                const m = menuById(l.menuItemId);
                html += '<div class="st-line"><div class="d-flex justify-content-between align-items-center gap-2">' +
                    '<div class="flex-grow-1"><div>' + esc(m.name) + '</div><small class="text-muted">' + esc(T.formatPrice(m.price * l.quantity)) + '</small></div>' +
                    '<div class="st-qty"><button class="btn btn-outline-secondary" data-act="dec" data-i="' + i + '" aria-label="Less">&minus;</button>' +
                    '<strong>' + l.quantity + '</strong>' +
                    '<button class="btn btn-outline-secondary" data-act="inc" data-i="' + i + '" aria-label="More">+</button></div></div>';
                if (l.noteOpen || l.note) {
                    html += '<input type="text" class="form-control mt-1" maxlength="200" placeholder="Line note (e.g. no onions)" data-line-note="' + i + '" value="' + esc(l.note) + '">';
                } else {
                    html += '<button class="btn btn-link btn-sm p-0" data-act="line-note" data-i="' + i + '">+ note</button>';
                }
                html += '</div>';
            });
            html += '<textarea class="form-control mt-2" rows="2" maxlength="500" placeholder="Order note (optional)" id="st-order-note">' + esc(draft.note) + '</textarea></div>';
        }
        html += '<div class="st-bar-in"><button class="sum st-sum" data-act="toggle-sheet" aria-expanded="' + sheetOpen + '">' +
            '<div>' + count + ' item' + (count === 1 ? '' : 's') + ' ' + (sheetOpen ? '&#9662;' : '&#9652;') + '</div>' +
            '<strong>' + esc(T.formatPrice(draftTotal())) + '</strong></button>' +
            '<button class="btn btn-success" data-act="send"' + (count === 0 || sending ? ' disabled' : '') + '>' +
            (sending ? '<span class="spinner-border spinner-border-sm me-1"></span>Sending' : 'Send') + '</button></div></div>';
        root.innerHTML = html;
        const nb = root.querySelector('.st-sheet-body');
        if (nb) nb.scrollTop = sheetScroll;
    }


    // ---------- actions ----------
    function renderKeepScroll() { const y = window.scrollY; render(); window.scrollTo(0, y); }

    function addToDraft(menuItemId) {
        const l = draft.lines.find(function (x) { return x.menuItemId === menuItemId && !x.note; });
        if (l) l.quantity++; else draft.lines.push({ menuItemId: menuItemId, quantity: 1, note: '', noteOpen: false });
        flashId = menuItemId;
        renderKeepScroll();
    }

    async function setStatus(id, status) {
        try {
            const o = await T.api.patch('/api/orders/' + id + '/status', { status: status });
            if (o) orders.set(o.id, o);
            render();
        } catch (e) { toast(e.message || 'Failed', 'danger', 4000); }
    }

    async function send() {
        if (sending || !draft.lines.length) return;
        sending = true; render();
        const body = {
            tableId: draft.tableId,
            note: draft.note.trim() || null,
            lines: draft.lines.map(function (l) { return { menuItemId: l.menuItemId, quantity: l.quantity, note: (l.note || '').trim() || null }; })
        };
        try {
            const o = await T.api.post('/api/orders', body);
            if (o) orders.set(o.id, o);
            const tid = draft.tableId;
            draft = newDraft(null);
            sending = false;
            toast('Order sent', 'success');
            go('table/' + tid);
            render();
        } catch (e) {
            sending = false;
            toast(e.message || 'Could not send order', 'danger', 5000);
            render();
        }
    }

    root.addEventListener('click', function (ev) {
        const b = ev.target.closest('[data-act]');
        if (!b) return;
        const act = b.dataset.act;
        const id = +b.dataset.id, i = +b.dataset.i;
        const r = route();
        switch (act) {
            case 'reload': location.reload(); break;
            case 'open-table': go('table/' + id); break;
            case 'home': go(''); break;
            case 'new-order': draft = newDraft(r.id); sheetOpen = false; go('table/' + r.id + '/new'); break;
            case 'serve': setStatus(id, 'Served'); break;
            case 'cancel': if (confirm('Cancel this order?')) setStatus(id, 'Cancelled'); break;
            case 'builder-back':
                // hashchange handler asks for confirmation if the draft is non-empty
                go('table/' + r.id);
                break;
            case 'add': addToDraft(id); break;
            case 'inc': draft.lines[i].quantity++; renderKeepScroll(); break;
            case 'dec':
                if (--draft.lines[i].quantity <= 0) draft.lines.splice(i, 1);
                renderKeepScroll(); break;
            case 'line-note': {
                draft.lines[i].noteOpen = true; render();
                const inp = root.querySelector('[data-line-note="' + i + '"]'); if (inp) inp.focus();
                break;
            }
            case 'toggle-sheet': sheetOpen = !sheetOpen; renderKeepScroll(); break;
            case 'send': send(); break;
        }
    });

    root.addEventListener('input', function (ev) {
        const t = ev.target;
        if (t.id === 'st-order-note') draft.note = t.value;
        else if (t.dataset.lineNote !== undefined) draft.lines[+t.dataset.lineNote].note = t.value;
    });

    window.addEventListener('hashchange', function () {
        const r = route();
        // Leaving the builder (back button or hash change) with a non-empty draft: confirm, else restore.
        if (r.name !== 'builder' && lastRoute && lastRoute.name === 'builder' && draft.lines.length && !sending) {
            if (!confirm('Discard this draft order?')) { go('table/' + lastRoute.id + '/new'); return; }
        }
        if (r.name !== 'builder') draft = newDraft(null);
        lastRoute = r;
        render();
        window.scrollTo(0, 0);
    });

    // ---------- live updates ----------
    function onOrder(o) {
        const prev = orders.get(o.id);
        orders.set(o.id, o);
        if (o.status === 'Ready' && (!prev || prev.status !== 'Ready')) {
            toast('Table ' + o.tableNumber + ': order ready', 'success', 5000);
            if (navigator.vibrate) { try { navigator.vibrate([200, 100, 200]); } catch (e) { } }
        }
        if (loaded && route().name !== 'builder') render();
    }

    function onMenu(items) {
        menu = items || [];
        const before = draft.lines.length;
        draft.lines = draft.lines.filter(function (l) { const m = menuById(l.menuItemId); return m && m.isAvailable; });
        const removed = draft.lines.length < before;
        toast(removed ? 'Menu updated: unavailable items were removed from your draft' : 'Menu updated', removed ? 'warning' : 'info', 4000);
        if (loaded) render();
    }

    async function load() {
        try {
            const res = await Promise.all([T.api.get('/api/tables'), T.api.get('/api/menu'), T.api.get('/api/orders')]);
            tables = res[0] || []; menu = res[1] || [];
            orders = new Map((res[2] || []).map(function (o) { return [o.id, o]; }));
            loaded = true; loadError = null;
        } catch (e) { loadError = 'Could not load data: ' + e.message; }
        lastRoute = route();
        render();
    }

    let wasConnected = false;
    T.connectHub({
        OrderCreated: onOrder,
        OrderUpdated: onOrder,
        TablesChanged: function (ts) { tables = ts || []; if (loaded) render(); },
        MenuChanged: onMenu,
        onStatus: function (s) {
            conn = s;
            // Resync orders after a reconnect so nothing missed while offline is lost.
            if (s === 'connected' && wasConnected && loaded) {
                T.api.get('/api/orders').then(function (list) {
                    orders = new Map((list || []).map(function (o) { return [o.id, o]; }));
                    if (route().name !== 'builder') render();
                }).catch(function () { });
            }
            if (s === 'connected') wasConnected = true;
            const dot = root.querySelector('.st-conn');
            if (dot) { dot.className = 'st-conn ' + s; dot.title = s; }
        }
    });

    load();
})();

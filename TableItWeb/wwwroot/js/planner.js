(function () {
    'use strict';

    var W = 1000, H = 700, GRID = 25, SEAT_R = 10, SEAT_GAP = 6;
    var NS = 'http://www.w3.org/2000/svg';

    var svg = document.getElementById('planner-svg');
    var layer = document.getElementById('tables-layer');
    var tables = [];      // each: {id, number, seats, shape, x, y, width, height, rotation, _k}
    var selectedKey = null;
    var dirty = false;
    var keyCounter = 1;
    var drag = null;

    var $ = function (id) { return document.getElementById(id); };
    var fields = {
        number: $('f-number'), seats: $('f-seats'), shape: $('f-shape'),
        width: $('f-width'), height: $('f-height'), rotation: $('f-rotation')
    };

    // ---------- helpers ----------
    function snap(v) { return Math.round(v / GRID) * GRID; }
    function clamp(v, lo, hi) { return Math.max(lo, Math.min(hi, v)); }
    function el(name, attrs) {
        var e = document.createElementNS(NS, name);
        for (var k in attrs) e.setAttribute(k, attrs[k]);
        return e;
    }
    function norm(t) {
        return {
            id: t.id, number: t.number, seats: t.seats, shape: t.shape,
            x: t.x, y: t.y, width: t.width, height: t.height, rotation: t.rotation,
            _k: keyCounter++
        };
    }
    function selected() {
        for (var i = 0; i < tables.length; i++) if (tables[i]._k === selectedKey) return tables[i];
        return null;
    }
    function findByKey(k) {
        for (var i = 0; i < tables.length; i++) if (tables[i]._k === k) return tables[i];
        return null;
    }
    function setDirty(v) {
        dirty = v;
        $('dirty-indicator').classList.toggle('d-none', !v);
    }
    function toast(message, kind) {
        var d = document.createElement('div');
        d.className = 'alert alert-' + (kind || 'success') + ' shadow py-2 mb-2';
        d.setAttribute('role', 'status');
        d.textContent = message;
        $('toast-area').appendChild(d);
        setTimeout(function () { d.remove(); }, kind === 'danger' ? 8000 : 2500);
    }
    function clampPos(t) {
        var w = t.width, h = t.height;
        // use rotated bounding box so tables stay inside the room
        var r = (t.rotation || 0) * Math.PI / 180;
        var bw = Math.abs(w * Math.cos(r)) + Math.abs(h * Math.sin(r));
        var bh = Math.abs(w * Math.sin(r)) + Math.abs(h * Math.cos(r));
        t.x = clamp(t.x, bw / 2, W - bw / 2);
        t.y = clamp(t.y, bh / 2, H - bh / 2);
    }

    // ---------- seats ----------
    function seatPositions(t) {
        var n = t.seats, pts = [], i;
        if (t.shape === 'Round') {
            var rx = t.width / 2 + SEAT_R + SEAT_GAP, ry = t.height / 2 + SEAT_R + SEAT_GAP;
            for (i = 0; i < n; i++) {
                var a = -Math.PI / 2 + i * 2 * Math.PI / n;
                pts.push([rx * Math.cos(a), ry * Math.sin(a)]);
            }
        } else {
            var side = Math.floor(n / 2), off = t.height / 2 + SEAT_R + SEAT_GAP;
            for (i = 0; i < side; i++) {
                var x = -t.width / 2 + t.width * (i + 0.5) / side;
                pts.push([x, -off]);
                pts.push([x, off]);
            }
            if (n % 2 === 1) pts.push([t.width / 2 + SEAT_R + SEAT_GAP, 0]);
        }
        return pts;
    }

    // ---------- rendering ----------
    function transformOf(t) {
        return 'translate(' + t.x + ',' + t.y + ') rotate(' + (t.rotation || 0) + ')';
    }

    function buildGroup(t) {
        var g = el('g', { 'class': 'table-group' + (t._k === selectedKey ? ' selected' : ''), transform: transformOf(t) });
        g.dataset.key = t._k;
        seatPositions(t).forEach(function (p) {
            g.appendChild(el('circle', { 'class': 't-seat', cx: p[0], cy: p[1], r: SEAT_R }));
        });
        if (t.shape === 'Round') {
            g.appendChild(el('ellipse', { 'class': 't-shape', cx: 0, cy: 0, rx: t.width / 2, ry: t.height / 2 }));
        } else {
            g.appendChild(el('rect', { 'class': 't-shape', x: -t.width / 2, y: -t.height / 2, width: t.width, height: t.height, rx: 10, ry: 10 }));
        }
        var txt = el('text', { 'class': 't-num', x: 0, y: 0, transform: 'rotate(' + (-(t.rotation || 0)) + ')' });
        txt.textContent = t.number;
        g.appendChild(txt);
        if (t._k === selectedKey) {
            if (t.shape === 'Round') {
                g.appendChild(el('ellipse', { 'class': 't-sel', cx: 0, cy: 0, rx: t.width / 2 + 5, ry: t.height / 2 + 5 }));
            } else {
                g.appendChild(el('rect', { 'class': 't-sel', x: -t.width / 2 - 5, y: -t.height / 2 - 5, width: t.width + 10, height: t.height + 10, rx: 14, ry: 14 }));
            }
        }
        return g;
    }

    function render() {
        while (layer.firstChild) layer.removeChild(layer.firstChild);
        // draw selected last so it is on top
        tables.filter(function (t) { return t._k !== selectedKey; }).forEach(function (t) { layer.appendChild(buildGroup(t)); });
        var s = selected();
        if (s) layer.appendChild(buildGroup(s));
        updateStats();
        updateToolbar();
    }

    function updateStats() {
        var seats = 0;
        tables.forEach(function (t) { seats += t.seats; });
        $('total-seats').textContent = seats;
        $('total-tables').textContent = tables.length;
    }
    function updateToolbar() {
        var has = !!selected();
        $('btn-duplicate').disabled = !has;
        $('btn-delete').disabled = !has;
    }

    // ---------- panel ----------
    function clearInvalid() {
        for (var k in fields) fields[k].classList.remove('is-invalid');
    }
    function fillPanel() {
        var t = selected();
        $('panel-empty').classList.toggle('d-none', !!t);
        $('panel-form').classList.toggle('d-none', !t);
        clearInvalid();
        if (!t) return;
        fields.number.value = t.number;
        fields.seats.value = t.seats;
        fields.shape.value = t.shape;
        fields.width.value = t.width;
        fields.height.value = t.height;
        fields.rotation.value = t.rotation;
    }
    function numberError(t, n) {
        if (!Number.isInteger(n) || n < 1) return 'Enter a whole number of 1 or more.';
        for (var i = 0; i < tables.length; i++) {
            if (tables[i] !== t && tables[i].number === n) return 'Table number ' + n + ' is already used.';
        }
        return null;
    }
    function onField(name) {
        var t = selected();
        if (!t) return;
        var f = fields[name], v = f.value, n = parseFloat(v), ok = true;
        if (name === 'number') {
            var err = numberError(t, parseInt(v, 10));
            ok = !err;
            $('f-number-err').textContent = err || '';
            if (ok) t.number = parseInt(v, 10);
        } else if (name === 'seats') {
            ok = Number.isInteger(n) && n >= 1 && n <= 20;
            if (ok) t.seats = n;
        } else if (name === 'shape') {
            t.shape = v === 'Rect' ? 'Rect' : 'Round';
        } else if (name === 'width' || name === 'height') {
            ok = isFinite(n) && n >= 40 && n <= 400;
            if (ok) t[name] = n;
        } else if (name === 'rotation') {
            ok = isFinite(n);
            if (ok) t.rotation = ((n % 360) + 360) % 360;
        }
        f.classList.toggle('is-invalid', !ok);
        if (ok) {
            clampPos(t);
            setDirty(true);
            render();
        }
    }
    Object.keys(fields).forEach(function (name) {
        fields[name].addEventListener('input', function () { onField(name); });
        fields[name].addEventListener('change', function () { onField(name); });
    });
    $('panel-form').addEventListener('submit', function (e) { e.preventDefault(); });

    function select(key) {
        selectedKey = key;
        render();
        fillPanel();
    }

    // ---------- pointer interaction ----------
    function svgPoint(e) {
        var m = svg.getScreenCTM();
        if (!m) return { x: 0, y: 0 };
        var p = new DOMPoint(e.clientX, e.clientY).matrixTransform(m.inverse());
        return { x: p.x, y: p.y };
    }

    svg.addEventListener('pointerdown', function (e) {
        if (e.button !== undefined && e.button > 0) return;
        var g = e.target.closest ? e.target.closest('.table-group') : null;
        if (!g) {
            if (selectedKey !== null) select(null);
            return;
        }
        var key = parseInt(g.dataset.key, 10);
        var t = findByKey(key);
        if (!t) return;
        e.preventDefault();
        var p = svgPoint(e);
        var needSelect = selectedKey !== key;
        selectedKey = key;
        if (needSelect) { render(); fillPanel(); }
        // re-fetch the group (render may have replaced it)
        g = layer.querySelector('.table-group[data-key="' + key + '"]');
        if (!g) return;
        drag = { key: key, g: g, dx: t.x - p.x, dy: t.y - p.y, moved: false, pointerId: e.pointerId, ox: t.x, oy: t.y };
        try { g.setPointerCapture(e.pointerId); } catch (err) { /* ignore */ }
        g.classList.add('dragging');
        g.addEventListener('pointermove', onMove);
        g.addEventListener('pointerup', onUp);
        g.addEventListener('pointercancel', onUp);
    });

    function onMove(e) {
        if (!drag || e.pointerId !== drag.pointerId) return;
        var t = findByKey(drag.key);
        if (!t) return;
        var p = svgPoint(e);
        t.x = p.x + drag.dx;
        t.y = p.y + drag.dy;
        if (!drag.moved && Math.hypot(t.x - drag.ox, t.y - drag.oy) < 4) return;
        drag.moved = true;
        clampPos(t);
        drag.g.setAttribute('transform', transformOf(t));
    }

    function onUp(e) {
        if (!drag || e.pointerId !== drag.pointerId) return;
        var g = drag.g, t = findByKey(drag.key), moved = drag.moved;
        g.removeEventListener('pointermove', onMove);
        g.removeEventListener('pointerup', onUp);
        g.removeEventListener('pointercancel', onUp);
        try { g.releasePointerCapture(e.pointerId); } catch (err) { /* ignore */ }
        var ox = drag.ox, oy = drag.oy;
        drag = null;
        if (t && moved) {
            t.x = snap(t.x);
            t.y = snap(t.y);
            clampPos(t);
            if (t.x !== ox || t.y !== oy) setDirty(true);
        }
        render();
    }

    // ---------- toolbar actions ----------
    function nextNumber() {
        var used = {};
        tables.forEach(function (t) { used[t.number] = true; });
        var n = 1;
        while (used[n]) n++;
        return n;
    }
    function occupied(x, y, skip) {
        return tables.some(function (t) { return t !== skip && Math.abs(t.x - x) < 25 && Math.abs(t.y - y) < 25; });
    }
    function placeFree(t) {
        var x = snap(W / 2), y = snap(H / 2), step = 0;
        t.x = x; t.y = y;
        while (occupied(t.x, t.y, t) && step < 40) {
            step++;
            t.x = x + step * GRID;
            t.y = y + step * GRID;
            if (t.x > W - 50 || t.y > H - 50) { t.x = x; t.y = y; break; }
        }
        clampPos(t);
    }
    function addTable(shape) {
        var t = norm({
            id: 0, number: nextNumber(), seats: shape === 'Round' ? 4 : 6, shape: shape,
            x: 500, y: 350, width: shape === 'Round' ? 90 : 160, height: 90, rotation: 0
        });
        placeFree(t);
        tables.push(t);
        setDirty(true);
        select(t._k);
    }
    function duplicateSelected() {
        var s = selected();
        if (!s) return;
        var t = norm({
            id: 0, number: nextNumber(), seats: s.seats, shape: s.shape,
            x: s.x + 50, y: s.y + 50, width: s.width, height: s.height, rotation: s.rotation
        });
        clampPos(t);
        var guard = 0;
        while (occupied(t.x, t.y, t) && guard++ < 20) { t.x += GRID; t.y += GRID; clampPos(t); }
        tables.push(t);
        setDirty(true);
        select(t._k);
    }
    function deleteSelected() {
        var s = selected();
        if (!s) return;
        if (!window.confirm('Delete table ' + s.number + '?')) return;
        tables = tables.filter(function (t) { return t !== s; });
        setDirty(true);
        select(null);
    }

    $('btn-add-round').addEventListener('click', function () { addTable('Round'); });
    $('btn-add-rect').addEventListener('click', function () { addTable('Rect'); });
    $('btn-duplicate').addEventListener('click', duplicateSelected);
    $('btn-delete').addEventListener('click', deleteSelected);

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Delete' && e.key !== 'Backspace') return;
        var a = document.activeElement;
        if (a && (a.tagName === 'INPUT' || a.tagName === 'SELECT' || a.tagName === 'TEXTAREA' || a.isContentEditable)) return;
        if (!selected()) return;
        e.preventDefault();
        deleteSelected();
    });

    // ---------- save / load ----------
    function setState(list) {
        var oldNumber = null, s = selected();
        if (s) oldNumber = s.number;
        tables = list.map(norm);
        selectedKey = null;
        if (oldNumber !== null) {
            for (var i = 0; i < tables.length; i++) if (tables[i].number === oldNumber) selectedKey = tables[i]._k;
        }
        render();
        fillPanel();
    }

    function save() {
        for (var i = 0; i < tables.length; i++) {
            var t = tables[i];
            var err = numberError(t, t.number);
            if (err) { toast(err, 'danger'); select(t._k); return; }
            if (!(t.seats >= 1 && t.seats <= 20)) { toast('Table ' + t.number + ' needs 1 to 20 seats.', 'danger'); select(t._k); return; }
        }
        var payload = tables.map(function (t) {
            return { id: t.id, number: t.number, seats: t.seats, shape: t.shape, x: Math.round(t.x), y: Math.round(t.y), width: t.width, height: t.height, rotation: t.rotation };
        });
        var btn = $('btn-save');
        btn.disabled = true;
        TableIt.api.put('/api/tables', payload).then(function (saved) {
            setState(saved || []);
            setDirty(false);
            $('remote-notice').classList.add('d-none');
            toast('Layout saved');
        }).catch(function (err) {
            toast(err.message || 'Save failed', 'danger');
        }).then(function () { btn.disabled = false; });
    }
    $('btn-save').addEventListener('click', save);

    function load(silent) {
        return TableIt.api.get('/api/tables').then(function (list) {
            setState(list || []);
            setDirty(false);
            $('remote-notice').classList.add('d-none');
            $('load-error').classList.add('d-none');
        }).catch(function (err) {
            if (silent) return;
            var box = $('load-error');
            box.textContent = 'Could not load tables: ' + (err.message || err);
            box.classList.remove('d-none');
        });
    }
    $('btn-reload').addEventListener('click', function () { load(false); });

    window.addEventListener('beforeunload', function (e) {
        if (!dirty) return;
        e.preventDefault();
        e.returnValue = '';
    });

    // ---------- live ----------
    TableIt.connectHub({
        TablesChanged: function (list) {
            if (dirty) {
                $('remote-notice').classList.remove('d-none');
            } else {
                setState(list || []);
            }
        }
    });

    render();
    load(false);
})();

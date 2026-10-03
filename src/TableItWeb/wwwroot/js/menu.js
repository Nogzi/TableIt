(function () {
    'use strict';
    const esc = TableIt.escapeHtml;
    let items = [];
    let editingId = null;
    let saving = false;

    const listEl = document.getElementById('menuList');
    const searchEl = document.getElementById('menuSearch');
    const alertEl = document.getElementById('menuAlert');
    const formEl = document.getElementById('itemForm');
    const formError = document.getElementById('formError');
    const modalEl = document.getElementById('itemModal');
    const modal = new bootstrap.Modal(modalEl);
    const f = {
        name: document.getElementById('fName'),
        category: document.getElementById('fCategory'),
        price: document.getElementById('fPrice'),
        description: document.getElementById('fDescription'),
        available: document.getElementById('fAvailable')
    };

    function showAlert(msg) {
        alertEl.textContent = msg;
        alertEl.classList.toggle('d-none', !msg);
    }

    function errText(e) {
        let m = (e && e.message) || String(e);
        try {
            const j = JSON.parse(m);
            if (j.errors) m = Object.values(j.errors).flat().join(' ');
            else if (j.title || j.detail) m = j.detail || j.title;
        } catch (_) { }
        return m;
    }

    function render() {
        const q = searchEl.value.trim().toLowerCase();
        const filtered = items.filter(function (i) {
            return !q || (i.name || '').toLowerCase().includes(q) || (i.description || '').toLowerCase().includes(q);
        });
        const cats = Array.from(new Set(items.map(function (i) { return i.category || ''; }))).filter(Boolean).sort();
        document.getElementById('categoryList').innerHTML = cats.map(function (c) { return '<option value="' + esc(c) + '"></option>'; }).join('');

        if (!filtered.length) {
            listEl.innerHTML = '<div class="text-muted">' + (items.length ? 'No items match your search.' : 'No menu items yet. Use "Add item".') + '</div>';
            return;
        }
        const groups = new Map();
        filtered.forEach(function (i) {
            const c = i.category || 'Uncategorized';
            if (!groups.has(c)) groups.set(c, []);
            groups.get(c).push(i);
        });
        let html = '';
        groups.forEach(function (arr, cat) {
            html += '<section class="card mb-3"><div class="card-header"><h2 class="h5 mb-0">' + esc(cat) + '</h2></div><div class="card-body p-0">';
            arr.forEach(function (i) {
                html += '<div class="menu-row' + (i.isAvailable ? '' : ' unavailable') + '" data-id="' + i.id + '">' +
                    '<div class="col-name fw-semibold">' + esc(i.name) + '</div>' +
                    '<div class="col-desc">' + esc(i.description) + '</div>' +
                    '<div class="col-price">' + esc(TableIt.formatPrice(i.price)) + '</div>' +
                    '<div class="col-avail form-check form-switch m-0"><input class="form-check-input avail-toggle" type="checkbox" role="switch" aria-label="Available: ' + esc(i.name) + '"' + (i.isAvailable ? ' checked' : '') + '></div>' +
                    '<div class="col-actions"><button type="button" class="btn btn-sm btn-outline-primary me-1" data-action="edit">Edit</button>' +
                    '<button type="button" class="btn btn-sm btn-outline-danger" data-action="delete">Delete</button></div>' +
                    '</div>';
            });
            html += '</div></section>';
        });
        listEl.innerHTML = html;
    }

    function setItems(arr) {
        items = Array.isArray(arr) ? arr : [];
        render();
    }

    async function load() {
        try {
            setItems(await TableIt.api.get('/api/menu'));
            showAlert('');
        } catch (e) {
            showAlert('Could not load menu: ' + errText(e));
            listEl.innerHTML = '';
        }
    }

    function findItem(id) { return items.find(function (i) { return i.id === id; }); }

    listEl.addEventListener('change', async function (ev) {
        if (!ev.target.classList.contains('avail-toggle')) return;
        const row = ev.target.closest('.menu-row');
        const item = findItem(Number(row.dataset.id));
        if (!item) return;
        const checked = ev.target.checked;
        ev.target.disabled = true;
        try {
            const updated = await TableIt.api.put('/api/menu/' + item.id, Object.assign({}, item, { isAvailable: checked }));
            Object.assign(item, updated || { isAvailable: checked });
            row.classList.toggle('unavailable', !item.isAvailable);
            showAlert('');
        } catch (e) {
            ev.target.checked = !checked;
            showAlert('Could not update "' + item.name + '": ' + errText(e));
        } finally {
            ev.target.disabled = false;
        }
    });

    listEl.addEventListener('click', async function (ev) {
        const btn = ev.target.closest('button[data-action]');
        if (!btn) return;
        const item = findItem(Number(btn.closest('.menu-row').dataset.id));
        if (!item) return;
        if (btn.dataset.action === 'edit') {
            openModal(item);
        } else if (btn.dataset.action === 'delete') {
            if (!confirm('Delete ' + item.name + '?')) return;
            try {
                await TableIt.api.del('/api/menu/' + item.id);
                items = items.filter(function (i) { return i.id !== item.id; });
                render();
                showAlert('');
            } catch (e) {
                showAlert('Could not delete "' + item.name + '": ' + errText(e));
            }
        }
    });

    function openModal(item) {
        editingId = item ? item.id : null;
        document.getElementById('itemModalTitle').textContent = item ? 'Edit item' : 'Add item';
        formEl.classList.remove('was-validated');
        formError.classList.add('d-none');
        f.name.value = item ? item.name : '';
        f.category.value = item ? (item.category || '') : '';
        f.price.value = item ? item.price : '';
        f.description.value = item ? (item.description || '') : '';
        f.available.checked = item ? !!item.isAvailable : true;
        modal.show();
    }

    modalEl.addEventListener('shown.bs.modal', function () { f.name.focus(); });
    document.getElementById('addItemBtn').addEventListener('click', function () { openModal(null); });
    searchEl.addEventListener('input', render);

    formEl.addEventListener('submit', async function (ev) {
        ev.preventDefault();
        if (saving) return;
        f.name.value = f.name.value.trim();
        formError.classList.add('d-none');
        const priceOk = f.price.value !== '' && Number(f.price.value) >= 0;
        f.price.setCustomValidity(priceOk ? '' : 'invalid');
        formEl.classList.add('was-validated');
        if (!formEl.checkValidity()) return;

        const body = {
            name: f.name.value,
            category: f.category.value.trim() || 'Uncategorized',
            price: Number(f.price.value),
            description: f.description.value.trim(),
            isAvailable: f.available.checked
        };
        const btn = document.getElementById('saveBtn');
        saving = true;
        btn.disabled = true;
        try {
            if (editingId != null) {
                const updated = await TableIt.api.put('/api/menu/' + editingId, Object.assign({ id: editingId }, body));
                const idx = items.findIndex(function (i) { return i.id === editingId; });
                if (idx >= 0) items[idx] = updated; else items.push(updated);
            } else {
                items.push(await TableIt.api.post('/api/menu', body));
            }
            items.sort(function (a, b) { return (a.category || '').localeCompare(b.category || '') || (a.name || '').localeCompare(b.name || ''); });
            render();
            modal.hide();
        } catch (e) {
            formError.textContent = errText(e);
            formError.classList.remove('d-none');
        } finally {
            saving = false;
            btn.disabled = false;
        }
    });

    TableIt.connectHub({
        MenuChanged: function (menu) { setItems(menu); }
    });
    load();
})();

(function () {
    'use strict';

    async function request(method, url, body) {
        const options = { method: method, headers: {} };
        if (body !== undefined) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(body);
        }
        const response = await fetch(url, options);
        if (!response.ok) {
            const text = await response.text();
            throw new Error(text || (response.status + ' ' + response.statusText));
        }
        if (response.status === 204) return null;
        const text = await response.text();
        return text ? JSON.parse(text) : null;
    }

    const api = {
        get: function (url) { return request('GET', url); },
        post: function (url, body) { return request('POST', url, body); },
        put: function (url, body) { return request('PUT', url, body); },
        patch: function (url, body) { return request('PATCH', url, body); },
        del: function (url) { return request('DELETE', url); }
    };

    function connectHub(handlers) {
        handlers = handlers || {};
        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/restaurant')
            .withAutomaticReconnect()
            .build();

        ['OrderCreated', 'OrderUpdated', 'TablesChanged', 'MenuChanged'].forEach(function (name) {
            if (typeof handlers[name] === 'function') connection.on(name, handlers[name]);
        });

        function status(state) {
            if (typeof handlers.onStatus === 'function') handlers.onStatus(state);
        }

        connection.onreconnecting(function () { status('reconnecting'); });
        connection.onreconnected(function () { status('connected'); });
        connection.onclose(function () {
            status('disconnected');
            // Automatic reconnect gave up; keep trying.
            setTimeout(start, 3000);
        });

        function start() {
            connection.start().then(function () {
                status('connected');
            }).catch(function () {
                status('disconnected');
                setTimeout(start, 3000);
            });
        }

        start();
        return connection;
    }

    function escapeHtml(str) {
        return String(str == null ? '' : str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    const priceFormat = new Intl.NumberFormat('da-DK', { style: 'currency', currency: 'DKK' });

    function formatPrice(number) {
        return priceFormat.format(number);
    }

    function minutesSince(isoString) {
        return Math.floor((Date.now() - new Date(isoString).getTime()) / 60000);
    }

    window.TableIt = {
        api: api,
        connectHub: connectHub,
        escapeHtml: escapeHtml,
        formatPrice: formatPrice,
        minutesSince: minutesSince
    };
})();

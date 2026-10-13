const Api = (() => {
    const base = "/api/databases";
    const enc = encodeURIComponent;

    async function request(method, url, body, rawBody) {
        const options = { method, headers: {} };
        if (rawBody !== undefined) {
            options.body = rawBody;
            options.headers["Content-Type"] = "application/json";
        } else if (body !== undefined) {
            options.body = JSON.stringify(body);
            options.headers["Content-Type"] = "application/json";
        }
        const response = await fetch(url, options);
        if (response.status === 204) return null;
        const data = await response.json().catch(() => null);
        if (!response.ok) throw new Error(data?.error ?? `Помилка сервера (${response.status})`);
        return data;
    }

    const db = name => `${base}/${enc(name)}`;
    const table = (d, t) => `${db(d)}/tables/${enc(t)}`;

    return {
        listDatabases: () => request("GET", base),
        createDatabase: name => request("POST", base, { name }),
        getDatabase: name => request("GET", db(name)),
        saveDatabase: name => request("POST", `${db(name)}/save`),
        reloadDatabase: name => request("POST", `${db(name)}/reload`),
        exportUrl: name => `${db(name)}/export`,
        importDatabase: json => request("POST", `${base}/import`, undefined, json),

        createTable: (d, name, columns) => request("POST", `${db(d)}/tables`, { name, columns }),
        dropTable: (d, t) => request("DELETE", table(d, t)),
        getTable: (d, t) => request("GET", table(d, t)),

        addRow: (d, t, values) => request("POST", `${table(d, t)}/rows`, { values }),
        updateRow: (d, t, index, values) => request("PUT", `${table(d, t)}/rows/${index}`, { values }),
        deleteRow: (d, t, index) => request("DELETE", `${table(d, t)}/rows/${index}`),

        search: (d, t, pattern, column, caseSensitive) => {
            const q = new URLSearchParams({ pattern, caseSensitive: String(caseSensitive) });
            if (column) q.set("column", column);
            return request("GET", `${table(d, t)}/search?${q}`);
        }
    };
})();

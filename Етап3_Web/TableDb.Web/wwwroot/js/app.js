const $ = id => document.getElementById(id);

const TYPES = ["integer", "real", "char", "string", "enum", "email"];
const TYPE_HINTS = {
    integer: "ціле число",
    real: "дійсне число, напр. 3.14",
    char: "один символ",
    string: "довільний рядок",
    enum: "значення з переліку",
    email: "name@example.com"
};

const state = {
    databases: [],
    db: null,
    table: null,
    search: null,
    editingIndex: null
};

function el(tag, props = {}, ...children) {
    const node = document.createElement(tag);
    for (const [key, value] of Object.entries(props)) {
        if (key === "class") node.className = value;
        else if (key === "dataset") Object.assign(node.dataset, value);
        else if (key.startsWith("on")) node.addEventListener(key.slice(2), value);
        else if (value !== undefined && value !== null && value !== false) node.setAttribute(key, value === true ? "" : value);
    }
    for (const child of children.flat()) {
        if (child !== null && child !== undefined) node.append(child instanceof Node ? child : String(child));
    }
    return node;
}

let toastTimer;
function toast(message, isError = false) {
    const t = $("toast");
    t.textContent = message;
    t.className = "toast" + (isError ? " error" : "");
    t.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => (t.hidden = true), 3500);
}

async function guard(action) {
    try {
        return await action();
    } catch (e) {
        toast(e.message, true);
    }
}

function typeLabel(column) {
    return column.type === "enum" ? `enum{${column.enumValues.join(", ")}}` : column.type;
}

function updateHash() {
    const params = new URLSearchParams();
    if (state.db) params.set("db", state.db.name);
    if (state.table) params.set("table", state.table.name);
    history.replaceState(null, "", "#" + params);
}

function wildcardRegex(pattern, caseSensitive) {
    const body = [...pattern].map(ch => ch === "*" ? ".*" : ch === "?" ? "." : ch.replace(/[.+^${}()|[\]\\]/g, "\\$&")).join("");
    return new RegExp(`^${body}$`, caseSensitive ? "su" : "siu");
}

async function loadDatabases(selectName) {
    state.databases = await Api.listDatabases();
    const select = $("dbSelect");
    select.replaceChildren(...state.databases.map(d =>
        el("option", { value: d.name }, d.name + (d.isDirty ? " *" : ""))));
    if (state.databases.length === 0) {
        select.append(el("option", { value: "" }, "— немає баз —"));
        state.db = null;
        state.table = null;
        render();
        return;
    }
    const name = state.databases.some(d => d.name === selectName) ? selectName : state.databases[0].name;
    select.value = name;
    await selectDatabase(name);
}

async function selectDatabase(name, tableName) {
    state.db = await Api.getDatabase(name);
    state.table = null;
    state.search = null;
    const target = state.db.tables.find(t => t.name === tableName) ?? state.db.tables[0];
    if (target) await selectTable(target.name);
    else render();
}

async function refreshDatabase() {
    state.db = await Api.getDatabase(state.db.name);
    const option = [...$("dbSelect").options].find(o => o.value === state.db.name);
    if (option) option.textContent = state.db.name + (state.db.isDirty ? " *" : "");
}

async function selectTable(name) {
    state.table = await Api.getTable(state.db.name, name);
    state.search = null;
    $("patternInput").value = "";
    $("caseInput").checked = false;
    render();
}

async function reloadTable() {
    state.table = await Api.getTable(state.db.name, state.table.name);
    if (state.search) {
        const s = state.search;
        state.search = await Api.search(state.db.name, state.table.name, s.pattern, s.column, s.caseSensitive);
    }
    await refreshDatabase();
    render();
}

function render() {
    renderSidebar();
    renderTable();
    updateHash();
}

function renderSidebar() {
    const hasDb = !!state.db;
    const dbState = $("dbState");
    dbState.textContent = !hasDb ? "" : state.db.isDirty ? "● Є незбережені зміни" : "✓ Усі зміни збережено на диску";
    dbState.className = "db-state" + (hasDb && state.db.isDirty ? " dirty" : "");
    for (const id of ["saveDbBtn", "reloadDbBtn", "exportDbBtn", "createTableBtn"]) $(id).disabled = !hasDb;

    const list = $("tableList");
    if (!hasDb || state.db.tables.length === 0) {
        list.replaceChildren(el("li", { class: "empty-note" }, hasDb ? "У базі ще немає таблиць" : "База не вибрана"));
        return;
    }
    list.replaceChildren(...state.db.tables.map(t =>
        el("li", {
                class: state.table?.name === t.name ? "active" : "",
                onclick: () => guard(() => selectTable(t.name))
            },
            el("span", {}, t.name, el("span", { class: "count" }, `${t.rowCount} рядк.`)),
            el("button", {
                class: "icon-btn danger", title: "Видалити таблицю",
                onclick: e => { e.stopPropagation(); dropTable(t.name, t.rowCount); }
            }, "✕"))));
}

function renderTable() {
    const table = state.table;
    $("emptyState").hidden = !!table;
    $("tableView").hidden = !table;
    if (!table) {
        $("emptyState").textContent = state.db ? "Створіть таблицю або виберіть її ліворуч." : "Створіть нову базу даних або імпортуйте її з файлу.";
        return;
    }

    $("tableTitle").textContent = table.name;
    $("schema").replaceChildren(...table.columns.map(c => el("span", { class: "chip" }, el("b", {}, c.name), ": " + typeLabel(c))));

    const columnSelect = $("columnSelect");
    const selectedColumn = columnSelect.value;
    columnSelect.replaceChildren(el("option", { value: "" }, "(усі поля)"), ...table.columns.map(c => el("option", { value: c.name }, c.name)));
    columnSelect.value = table.columns.some(c => c.name === selectedColumn) ? selectedColumn : "";

    $("gridHead").replaceChildren(el("tr", {},
        el("th", {}, "№"),
        ...table.columns.map(c => el("th", {}, c.name, el("span", { class: "type" }, typeLabel(c)))),
        el("th", {})));

    const search = state.search;
    const summary = $("searchSummary");
    summary.hidden = !search;
    let rows = table.rows;
    let matcher = null;
    if (search) {
        const where = search.column ? `у полі «${search.column}»` : "в усіх полях";
        summary.textContent = `Шаблон «${search.pattern}» ${where}${search.caseSensitive ? " (з урахуванням регістру)" : ""}: знайдено ${search.results.length} з ${search.totalRows} рядків.`;
        rows = search.results;
        const regex = wildcardRegex(search.pattern, search.caseSensitive);
        const columnIndex = search.column ? table.columns.findIndex(c => c.name.toLowerCase() === search.column.toLowerCase()) : -1;
        matcher = (value, i) => (columnIndex < 0 || columnIndex === i) && regex.test(value ?? "");
    }

    const body = $("gridBody");
    if (rows.length === 0) {
        body.replaceChildren(el("tr", {}, el("td", { class: "no-rows", colspan: table.columns.length + 2 },
            search ? "Жоден рядок не відповідає шаблону." : "Таблиця порожня — додайте перший рядок.")));
        return;
    }
    body.replaceChildren(...rows.map(row => el("tr", { class: search ? "found" : "" },
        el("td", { class: "num" }, row.index + 1),
        ...table.columns.map((c, i) => {
            const value = row.values[i];
            if (value === null || value === undefined) return el("td", { class: "null" }, "—");
            return el("td", {}, matcher && matcher(value, i) ? el("mark", {}, value) : value);
        }),
        el("td", { class: "actions" },
            el("button", { class: "icon-btn", title: "Редагувати", onclick: () => openRowDialog(row.index) }, "✎"),
            el("button", { class: "icon-btn danger", title: "Видалити", onclick: () => deleteRow(row.index) }, "🗑")))));
}

$("searchForm").addEventListener("submit", e => {
    e.preventDefault();
    guard(async () => {
        state.search = await Api.search(state.db.name, state.table.name, $("patternInput").value, $("columnSelect").value, $("caseInput").checked);
        renderTable();
    });
});

$("resetSearchBtn").addEventListener("click", () => {
    state.search = null;
    $("patternInput").value = "";
    renderTable();
});

function openRowDialog(index = null) {
    state.editingIndex = index;
    const current = index === null ? null : state.table.rows.find(r => r.index === index)?.values;
    $("rowDialogTitle").textContent = index === null ? `Новий рядок — ${state.table.name}` : `Редагування рядка ${index + 1} — ${state.table.name}`;
    $("rowSubmitBtn").textContent = index === null ? "Додати" : "Зберегти";
    $("rowError").textContent = "";

    const fields = state.table.columns.flatMap((c, i) => {
        const id = `field_${i}`;
        const value = current?.[i] ?? "";
        const input = c.type === "enum"
            ? el("select", { id }, el("option", { value: "" }, "—"), ...c.enumValues.map(v => el("option", { value: v }, v)))
            : el("input", { id, type: "text", placeholder: TYPE_HINTS[c.type], maxlength: c.type === "char" ? 1 : undefined, autocomplete: "off" });
        input.value = value;
        return [el("label", { for: id }, c.name, el("small", {}, typeLabel(c))), input];
    });
    $("rowFields").replaceChildren(...fields);
    $("rowDialog").showModal();
    $("field_0")?.focus();
}

$("rowForm").addEventListener("submit", async e => {
    e.preventDefault();
    const values = state.table.columns.map((_, i) => $(`field_${i}`).value);
    try {
        if (state.editingIndex === null) await Api.addRow(state.db.name, state.table.name, values);
        else await Api.updateRow(state.db.name, state.table.name, state.editingIndex, values);
        $("rowDialog").close();
        toast(state.editingIndex === null ? "Рядок додано." : `Рядок ${state.editingIndex + 1} збережено.`);
        await reloadTable();
    } catch (err) {
        $("rowError").textContent = "⚠ " + err.message;
    }
});

async function deleteRow(index) {
    if (!confirm(`Видалити рядок ${index + 1}?`)) return;
    await guard(async () => {
        await Api.deleteRow(state.db.name, state.table.name, index);
        toast(`Рядок ${index + 1} видалено.`);
        await reloadTable();
    });
}

$("addRowBtn").addEventListener("click", () => openRowDialog(null));

function addColumnRow(name = "", type = "string", enumValues = "") {
    const enumInput = el("input", { type: "text", value: enumValues });
    const typeSelect = el("select", {}, ...TYPES.map(t => el("option", { value: t }, t)));
    typeSelect.value = type;
    const syncEnum = () => {
        enumInput.disabled = typeSelect.value !== "enum";
        enumInput.placeholder = enumInput.disabled ? "" : "денна; заочна";
    };
    typeSelect.addEventListener("change", syncEnum);
    syncEnum();
    const row = el("div", { class: "col-row" },
        el("input", { type: "text", placeholder: "Назва поля", value: name, class: "col-name" }),
        typeSelect,
        enumInput,
        el("button", { type: "button", class: "icon-btn danger", title: "Прибрати поле", onclick: () => row.remove() }, "✕"));
    $("columnsEditor").append(row);
}

$("createTableBtn").addEventListener("click", () => {
    $("tableNameInput").value = "";
    $("tableError").textContent = "";
    $("columnsEditor").replaceChildren();
    addColumnRow("Id", "integer");
    addColumnRow();
    $("tableDialog").showModal();
    $("tableNameInput").focus();
});

$("addColumnBtn").addEventListener("click", () => addColumnRow());

$("tableForm").addEventListener("submit", async e => {
    e.preventDefault();
    const columns = [...$("columnsEditor").children].map(row => {
        const [nameInput, typeSelect, enumInput] = row.querySelectorAll("input, select");
        return {
            name: nameInput.value.trim(),
            type: typeSelect.value,
            enumValues: typeSelect.value === "enum" ? enumInput.value.split(";").map(v => v.trim()).filter(Boolean) : []
        };
    }).filter(c => c.name || c.enumValues.length);
    try {
        const table = await Api.createTable(state.db.name, $("tableNameInput").value, columns);
        $("tableDialog").close();
        toast(`Таблицю «${table.name}» створено.`);
        await refreshDatabase();
        await selectTable(table.name);
    } catch (err) {
        $("tableError").textContent = "⚠ " + err.message;
    }
});

async function dropTable(name, rowCount) {
    if (!confirm(`Видалити таблицю «${name}» (${rowCount} рядків)?`)) return;
    await guard(async () => {
        await Api.dropTable(state.db.name, name);
        toast(`Таблицю «${name}» видалено.`);
        const wasCurrent = state.table?.name === name;
        await refreshDatabase();
        if (wasCurrent) {
            state.table = null;
            if (state.db.tables[0]) await selectTable(state.db.tables[0].name);
        }
        render();
    });
}

$("dbSelect").addEventListener("change", e => guard(() => selectDatabase(e.target.value)));

$("newDbBtn").addEventListener("click", () => {
    $("dbNameInput").value = "";
    $("dbError").textContent = "";
    $("dbDialog").showModal();
});

$("dbForm").addEventListener("submit", async e => {
    e.preventDefault();
    try {
        const db = await Api.createDatabase($("dbNameInput").value);
        $("dbDialog").close();
        toast(`Базу «${db.name}» створено. Не забудьте зберегти її.`);
        await loadDatabases(db.name);
    } catch (err) {
        $("dbError").textContent = "⚠ " + err.message;
    }
});

$("saveDbBtn").addEventListener("click", () => guard(async () => {
    const result = await Api.saveDatabase(state.db.name);
    toast(result.message);
    await refreshDatabase();
    renderSidebar();
}));

$("reloadDbBtn").addEventListener("click", () => guard(async () => {
    if (!confirm(`Відкинути незбережені зміни в базі «${state.db.name}» і перечитати її з диска?`)) return;
    const result = await Api.reloadDatabase(state.db.name);
    toast(result.message);
    await selectDatabase(state.db.name, state.table?.name);
    await refreshDatabase();
    renderSidebar();
}));

$("exportDbBtn").addEventListener("click", () => {
    window.location.href = Api.exportUrl(state.db.name);
});

$("importDbBtn").addEventListener("click", () => $("importFile").click());

$("importFile").addEventListener("change", async e => {
    const file = e.target.files[0];
    e.target.value = "";
    if (!file) return;
    await guard(async () => {
        const db = await Api.importDatabase(await file.text());
        toast(`Базу «${db.name}» імпортовано.`);
        await loadDatabases(db.name);
    });
});

document.querySelectorAll("[data-close]").forEach(b => b.addEventListener("click", () => b.closest("dialog").close()));

(async () => {
    const params = new URLSearchParams(location.hash.slice(1));
    await guard(async () => {
        await loadDatabases(params.get("db"));
        const tableName = params.get("table");
        if (tableName && state.db?.tables.some(t => t.name === tableName) && state.table?.name !== tableName)
            await selectTable(tableName);
    });
})();

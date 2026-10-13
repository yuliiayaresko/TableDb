# TableDb — десктоп-версія (Етап 2)

СУ табличними базами даних, варіант 41: типи `enum` та `email`, пошук рядків за шаблоном (`*`, `?`).
C# / .NET 10, Windows Forms (без HTML/CSS).

## Запуск

```
dotnet run --project TableDb.Desktop
```

Демонстраційна база: «Файл → Відкрити…» → `samples/Університет.tdb.json`.

## Тести

```
dotnet test
```

## Структура

- `TableDb.Core` — модель, типи даних, валідація, пошук, збереження у JSON
- `TableDb.Desktop` — графічний інтерфейс (MainForm, CreateTableForm, RowEditForm, SearchResultsForm)
- `TableDb.Tests` — unit-тести xUnit (37 тестових випадків)
- `screenshots/` — знімки екрана для звіту

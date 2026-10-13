# TableDb Web — веб-версія (Етап 3)

REST API на ASP.NET Core Minimal API (.NET 10) + клієнт на HTML/CSS/JavaScript.
Використовує ту саму бібліотеку `TableDb.Core`, що й десктоп-версія.

## Запуск

```
dotnet run --project TableDb.Web
```

Відкрити http://localhost:5000. Під час першого запуску демонстраційна база
«Університет» копіюється в `TableDb.Web/Data/`; там же зберігаються всі бази.

## Основні ендпоінти

| Метод | URL | Дія |
|---|---|---|
| GET | `/api/databases` | список баз |
| POST | `/api/databases` | створити базу |
| POST | `/api/databases/{db}/tables` | створити таблицю |
| POST/PUT/DELETE | `/api/databases/{db}/tables/{t}/rows[/{i}]` | робота з рядками |
| GET | `/api/databases/{db}/tables/{t}/search?pattern=*@gmail.com&column=Email` | пошук за шаблоном |
| POST | `/api/databases/{db}/save` | зберегти на диск |

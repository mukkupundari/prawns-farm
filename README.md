# Prawns Farm - Expense & Profit Tracker

A simple ASP.NET Core **MVC (.NET 8)** application to track farm **expenses** (feed, labour, power, medicine, etc.), record **crop sales**, and view **profit/loss** on a single dashboard with **date-range filtering** and **report export**.

UI + backend live in a single project. Data is stored in **SQL Server (LocalDB)** via **Entity Framework Core**, with a `Sql/` folder containing ready-to-use **stored procedures** for filtering and reporting.

## Features

- Basic **login** (cookie authentication)
- **Dashboard**: KPI cards (Total Expenses, Total Sales, Net Profit/Loss, Avg/Day), Expenses-vs-Sales chart, Expenses-by-Category doughnut, Payment-mode breakdown, recent transactions — all driven by a **date-range filter** (Today / 7 Days / This Month / This Year / All / custom).
- **Expenses**: add / edit / delete, filter by date range and category.
- **Sales**: record harvest income (Qty × Rate auto-calculates total).
- **Categories**: manage expense/income categories.
- **Reports**: date range + type + category filter, **Excel (.xlsx)** and **CSV** export (net profit/loss included).

### Database layer (stored procedures)

All **filtering, date-range and reporting logic runs inside the local SQL Server database** via stored procedures. They are **created/updated automatically on every startup** from `PrawnsFarm/Sql/02_StoredProcedures.sql` (CREATE OR ALTER, so it's safe to run repeatedly) — no manual step needed.

| Procedure | Used by |
|-----------|---------|
| `usp_Dashboard_Summary` | Dashboard KPI totals (expenses, sales, counts) |
| `usp_Expenses_ByCategory` | Dashboard doughnut + Excel summary sheet |
| `usp_Expenses_ByPaymentMode` | Dashboard payment breakdown |
| `usp_Expenses_List` | Expenses screen (date + category filter) |
| `usp_Expenses_RecentlyAdded` | Dashboard "Expenses Added (Last 24 Hours)" |
| `usp_Sales_List` | Sales screen (date filter) |
| `usp_Report_Transactions` | Reports grid + CSV/Excel export |
| `usp_Report_MonthlyTrend` | Monthly profit/loss trend (available for reports) |

Reads go through these procedures (via ADO.NET in `Data/ReportRepository.cs`); writes (add/edit/delete) use EF Core. Everything is 100% local — no external services.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- **SQL Server LocalDB** (installed with Visual Studio or the "SQL Server Express LocalDB" component). Already present on most dev machines.

To confirm LocalDB is available:

```powershell
sqllocaldb info
```

If you prefer full SQL Server or SQL Express, just edit the connection string in `PrawnsFarm/appsettings.json`.

## Run

From the `Farms` folder:

```powershell
cd PrawnsFarm
dotnet restore
dotnet run
```

Then open the HTTPS URL shown in the console (e.g. `https://localhost:7189`).

On first run the database `PrawnsFarmDb` is created automatically and seeded with the default farm profile, categories, and a login.

### Default login

| Username | Password  |
|----------|-----------|
| `admin`  | `admin123` |

## Stored procedures

The procedures in `PrawnsFarm/Sql/02_StoredProcedures.sql` are installed automatically each time the app starts, so **you don't need to run anything manually**. If you prefer to inspect or run them by hand, open that file in SQL Server Management Studio / Azure Data Studio and execute it against `PrawnsFarmDb`.

`Sql/01_Schema_Reference.sql` documents the exact table and column names.

> After pulling these changes, just run the app again — the new/updated procedures are applied on startup.

## Project structure

```
PrawnsFarm/
  Controllers/     Account, Dashboard, Expenses, Sales, Categories, Reports, Home
  Data/            AppDbContext, DbSeeder
  Models/
    Entities/      User, Category, Expense, Sale, FarmProfile
    ViewModels/    LoginViewModel, DateRangeFilter, DashboardViewModel
  Services/        PasswordHasher
  Views/           Razor views per controller + shared layout
  wwwroot/         css / js (Bootstrap 5 + Chart.js via CDN)
  Sql/             Schema reference + stored procedures
  appsettings.json Connection string
```

## Notes

- Change the connection string in `appsettings.json` to point at a different SQL Server instance if needed.
- Passwords are hashed with PBKDF2 (SHA-256, 100k iterations).
- To reset all data, drop the `PrawnsFarmDb` database and re-run the app.

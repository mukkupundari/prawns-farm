/* =====================================================================
   Prawns Farm - Stored Procedures (filtering, date ranges, reports)
   ---------------------------------------------------------------------
   These procedures run ALL filtering / aggregation / reporting logic
   inside the local SQL Server database (LocalDB).

   They are created / updated AUTOMATICALLY every time the app starts
   (see Data/ProcedureInstaller.ApplyAsync). You can also run this file
   manually in SSMS against [PrawnsFarmDb].

   Convention:
     @FromDate / @ToDate are inclusive calendar dates. Each proc converts
     @ToDate to an exclusive upper bound (next day) so time components on
     [Date] are always covered.
     @FarmId scopes every result to a single farm (F1 / F2 / F3).
   ===================================================================== */
USE [PrawnsFarmDb];
GO

/* ---------------------------------------------------------------------
   1) Dashboard summary (totals) for a date range + farm
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_Summary
    @FromDate DATE,
    @ToDate   DATE,
    @FarmId   INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ToInclusive DATETIME2 = DATEADD(DAY, 1, CAST(@ToDate AS DATETIME2));

    SELECT
        TotalExpenses = ISNULL((SELECT SUM(Amount) FROM dbo.Expenses
                                WHERE [Date] >= @FromDate AND [Date] < @ToInclusive AND FarmId = @FarmId), 0),
        TotalSales    = ISNULL((SELECT SUM(Amount) FROM dbo.Sales
                                WHERE [Date] >= @FromDate AND [Date] < @ToInclusive AND FarmId = @FarmId), 0),
        ExpenseCount  = (SELECT COUNT(*) FROM dbo.Expenses
                         WHERE [Date] >= @FromDate AND [Date] < @ToInclusive AND FarmId = @FarmId),
        SaleCount     = (SELECT COUNT(*) FROM dbo.Sales
                         WHERE [Date] >= @FromDate AND [Date] < @ToInclusive AND FarmId = @FarmId);
END
GO

/* ---------------------------------------------------------------------
   2) Expenses grouped by category (for the doughnut chart)
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Expenses_ByCategory
    @FromDate DATE,
    @ToDate   DATE,
    @FarmId   INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ToInclusive DATETIME2 = DATEADD(DAY, 1, CAST(@ToDate AS DATETIME2));

    SELECT
        c.Id,
        c.Name,
        c.Color,
        Amount = ISNULL(SUM(e.Amount), 0)
    FROM dbo.Categories c
    LEFT JOIN dbo.Expenses e
           ON e.CategoryId = c.Id
          AND e.[Date] >= @FromDate AND e.[Date] < @ToInclusive
          AND e.FarmId = @FarmId
    WHERE c.[Type] = 0            -- 0 = Expense
    GROUP BY c.Id, c.Name, c.Color
    HAVING ISNULL(SUM(e.Amount), 0) > 0
    ORDER BY Amount DESC;
END
GO

/* ---------------------------------------------------------------------
   3) Expenses grouped by payment mode (for the payment doughnut)
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Expenses_ByPaymentMode
    @FromDate DATE,
    @ToDate   DATE,
    @FarmId   INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ToInclusive DATETIME2 = DATEADD(DAY, 1, CAST(@ToDate AS DATETIME2));

    SELECT
        PaymentMode = e.PaymentMode,
        Amount      = SUM(e.Amount)
    FROM dbo.Expenses e
    WHERE e.[Date] >= @FromDate AND e.[Date] < @ToInclusive
      AND e.FarmId = @FarmId
    GROUP BY e.PaymentMode
    ORDER BY Amount DESC;
END
GO

/* ---------------------------------------------------------------------
   4) Filtered expense list (Expenses screen + reports)
      @CategoryId optional (NULL = all categories)
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Expenses_List
    @FromDate   DATE,
    @ToDate     DATE,
    @FarmId     INT,
    @CategoryId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ToInclusive DATETIME2 = DATEADD(DAY, 1, CAST(@ToDate AS DATETIME2));

    SELECT
        e.Id,
        e.[Date],
        e.CategoryId,
        CategoryName  = c.Name,
        CategoryColor = c.Color,
        e.[Description],
        e.Amount,
        e.PaymentMode,
        e.Notes,
        e.CreatedOn
    FROM dbo.Expenses e
    INNER JOIN dbo.Categories c ON c.Id = e.CategoryId
    WHERE e.[Date] >= @FromDate AND e.[Date] < @ToInclusive
      AND e.FarmId = @FarmId
      AND (@CategoryId IS NULL OR e.CategoryId = @CategoryId)
    ORDER BY e.[Date] DESC, e.Id DESC;
END
GO

/* ---------------------------------------------------------------------
   5) Expenses recorded (CreatedOn) within the last N hours
      Used by the dashboard "Expenses Added (Last 24 Hours)" grid.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Expenses_RecentlyAdded
    @FarmId INT,
    @Hours  INT = 24
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Since DATETIME2 = DATEADD(HOUR, -@Hours, SYSUTCDATETIME());

    SELECT
        e.Id,
        e.[Date],
        e.CategoryId,
        CategoryName  = c.Name,
        CategoryColor = c.Color,
        e.[Description],
        e.Amount,
        e.PaymentMode,
        e.Notes,
        e.CreatedOn
    FROM dbo.Expenses e
    INNER JOIN dbo.Categories c ON c.Id = e.CategoryId
    WHERE e.CreatedOn >= @Since
      AND e.FarmId = @FarmId
    ORDER BY e.CreatedOn DESC;
END
GO

/* ---------------------------------------------------------------------
   6) Filtered sales list (Sales screen + reports)
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Sales_List
    @FromDate DATE,
    @ToDate   DATE,
    @FarmId   INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ToInclusive DATETIME2 = DATEADD(DAY, 1, CAST(@ToDate AS DATETIME2));

    SELECT
        s.Id,
        s.[Date],
        s.Crop,
        s.QuantityKg,
        s.RatePerKg,
        s.Amount,
        s.Buyer,
        s.PaymentMode,
        s.Notes,
        s.CreatedOn
    FROM dbo.Sales s
    WHERE s.[Date] >= @FromDate AND s.[Date] < @ToInclusive
      AND s.FarmId = @FarmId
    ORDER BY s.[Date] DESC, s.Id DESC;
END
GO

/* ---------------------------------------------------------------------
   7) Combined transactions report (expenses + sales)
      @Type: 'all' | 'expense' | 'sale'
      @CategoryId: optional (applies to expenses only)
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Report_Transactions
    @FromDate   DATE,
    @ToDate     DATE,
    @FarmId     INT,
    @Type       VARCHAR(10) = 'all',
    @CategoryId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ToInclusive DATETIME2 = DATEADD(DAY, 1, CAST(@ToDate AS DATETIME2));

    ;WITH Combined AS
    (
        SELECT
            [Date]        = e.[Date],
            [Type]        = CAST('Expense' AS VARCHAR(10)),
            [Category]    = c.Name,
            [Description] = e.[Description],
            [Amount]      = e.Amount,
            [PaymentMode] = e.PaymentMode
        FROM dbo.Expenses e
        INNER JOIN dbo.Categories c ON c.Id = e.CategoryId
        WHERE e.[Date] >= @FromDate AND e.[Date] < @ToInclusive
          AND e.FarmId = @FarmId
          AND (@CategoryId IS NULL OR e.CategoryId = @CategoryId)
          AND (@Type IS NULL OR @Type IN ('all','expense'))

        UNION ALL

        SELECT
            [Date]        = s.[Date],
            [Type]        = CAST('Sale' AS VARCHAR(10)),
            [Category]    = s.Crop,
            [Description] = s.Buyer,
            [Amount]      = s.Amount,
            [PaymentMode] = s.PaymentMode
        FROM dbo.Sales s
        WHERE s.[Date] >= @FromDate AND s.[Date] < @ToInclusive
          AND s.FarmId = @FarmId
          AND (@Type IS NULL OR @Type IN ('all','sale'))
          AND @CategoryId IS NULL          -- category filter is expense-only
    )
    SELECT * FROM Combined
    ORDER BY [Date] DESC;
END
GO

/* ---------------------------------------------------------------------
   8) Monthly profit/loss trend for a date range + farm
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Report_MonthlyTrend
    @FromDate DATE,
    @ToDate   DATE,
    @FarmId   INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ToInclusive DATETIME2 = DATEADD(DAY, 1, CAST(@ToDate AS DATETIME2));

    SELECT
        [Year]    = DATEPART(YEAR, d.[Date]),
        [Month]   = DATEPART(MONTH, d.[Date]),
        Expenses  = SUM(d.Expenses),
        Sales     = SUM(d.Sales),
        NetProfit = SUM(d.Sales) - SUM(d.Expenses)
    FROM (
        SELECT [Date], Expenses = Amount, Sales = 0 FROM dbo.Expenses
        WHERE [Date] >= @FromDate AND [Date] < @ToInclusive AND FarmId = @FarmId
        UNION ALL
        SELECT [Date], Expenses = 0, Sales = Amount FROM dbo.Sales
        WHERE [Date] >= @FromDate AND [Date] < @ToInclusive AND FarmId = @FarmId
    ) d
    GROUP BY DATEPART(YEAR, d.[Date]), DATEPART(MONTH, d.[Date])
    ORDER BY [Year], [Month];
END
GO

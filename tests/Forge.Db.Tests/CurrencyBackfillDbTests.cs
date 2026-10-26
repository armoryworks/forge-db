using Forge.Db;
using Npgsql;
using Xunit;

namespace Forge.Db.Tests;

/// <summary>
/// Integration coverage of <c>data/0050-currency-backfill.sql</c> against a real Postgres.
/// Skips silently unless <c>FORGE_DB_TEST_DSN</c> is set (a postgres:// URL whose user has CREATEDB). It
/// creates a uniquely-named throwaway DB holding only the columns the script reads, runs the script there
/// through <see cref="DataSeedRunner.Apply"/>, and drops the DB in a finally.
/// </summary>
public class CurrencyBackfillDbTests
{
    private const string ScriptName = "0050-currency-backfill.sql";

    private const string Tables = """
        CREATE TABLE currencies (id integer PRIMARY KEY, code varchar(10) NOT NULL, is_base_currency boolean NOT NULL, is_active boolean NOT NULL);
        CREATE TABLE acct_books (id integer PRIMARY KEY, functional_currency_id integer NOT NULL, is_active boolean NOT NULL);
        CREATE TABLE customers (id integer PRIMARY KEY, default_currency varchar(3), currency_id integer);
        CREATE TABLE quotes (id integer PRIMARY KEY, currency_id integer);
        CREATE TABLE sales_orders (id integer PRIMARY KEY, currency_id integer);
        CREATE TABLE purchase_orders (id integer PRIMARY KEY, quote_currency varchar(3) NOT NULL, currency_id integer);
        CREATE TABLE invoices (id integer PRIMARY KEY, currency_id integer NOT NULL DEFAULT 1, fx_rate numeric(18,8) NOT NULL DEFAULT 1);
        CREATE TABLE vendor_bills (id integer PRIMARY KEY, currency_id integer NOT NULL DEFAULT 1, fx_rate numeric(18,8) NOT NULL DEFAULT 1);
        CREATE TABLE payments (id integer PRIMARY KEY, currency_id integer, fx_rate numeric(18,8) NOT NULL DEFAULT 1);
        CREATE TABLE payment_applications (id integer PRIMARY KEY, payment_id integer NOT NULL, invoice_id integer NOT NULL, settlement_fx_rate numeric(18,8) NOT NULL DEFAULT 1);
        CREATE TABLE vendor_payments (id integer PRIMARY KEY, currency_id integer, fx_rate numeric(18,8) NOT NULL DEFAULT 1);
        CREATE TABLE vendor_payment_applications (id integer PRIMARY KEY, vendor_payment_id integer NOT NULL, vendor_bill_id integer NOT NULL, settlement_fx_rate numeric(18,8) NOT NULL DEFAULT 1);
        CREATE TABLE acct_ar_open_items (id integer PRIMARY KEY, source_type varchar(50) NOT NULL, source_id integer NOT NULL, currency_id integer NOT NULL, fx_rate numeric(18,8) NOT NULL);
        CREATE TABLE acct_ap_open_items (id integer PRIMARY KEY, source_type varchar(50) NOT NULL, source_id integer NOT NULL, currency_id integer NOT NULL, fx_rate numeric(18,8) NOT NULL);
        """;

    private static string? Dsn => Environment.GetEnvironmentVariable("FORGE_DB_TEST_DSN");

    [Fact]
    public void Apply_FillsPartyAndDocumentCurrencies_FromCodesApplicationsAndTheFunctionalCurrency()
    {
        if (string.IsNullOrWhiteSpace(Dsn)) return;

        WithScratchDb("""
            INSERT INTO currencies VALUES (1, 'USD', true, true), (2, 'CAD', false, true), (3, 'EUR', false, false);
            INSERT INTO acct_books VALUES (1, 1, true);
            INSERT INTO customers VALUES (1, 'cad', NULL), (2, 'EUR', NULL), (3, NULL, NULL), (4, 'USD', 2);
            INSERT INTO quotes VALUES (1, NULL), (2, 2);
            INSERT INTO sales_orders VALUES (1, NULL);
            INSERT INTO purchase_orders VALUES (1, 'CAD', NULL), (2, 'XYZ', NULL), (3, 'USD', NULL);
            INSERT INTO invoices VALUES (1, 1, 1), (2, 2, 0.73);
            INSERT INTO payments VALUES (1, NULL, 1), (2, NULL, 1);
            INSERT INTO payment_applications VALUES (1, 1, 2, 0.75);
            INSERT INTO vendor_bills VALUES (1, 2, 0.72);
            INSERT INTO vendor_payments VALUES (1, NULL, 1), (2, NULL, 1);
            INSERT INTO vendor_payment_applications VALUES (1, 1, 1, 0.74);
            """, scratch =>
        {
            Assert.Equal(2L, ScalarLong(scratch, "SELECT currency_id FROM customers WHERE id = 1"));
            Assert.Equal(0L, ScalarLong(scratch, "SELECT count(*) FROM customers WHERE id IN (2, 3) AND currency_id IS NOT NULL"));
            Assert.Equal(2L, ScalarLong(scratch, "SELECT currency_id FROM customers WHERE id = 4"));
            Assert.Equal(1L, ScalarLong(scratch, "SELECT currency_id FROM quotes WHERE id = 1"));
            Assert.Equal(2L, ScalarLong(scratch, "SELECT currency_id FROM quotes WHERE id = 2"));
            Assert.Equal(1L, ScalarLong(scratch, "SELECT currency_id FROM sales_orders WHERE id = 1"));
            Assert.Equal(2L, ScalarLong(scratch, "SELECT currency_id FROM purchase_orders WHERE id = 1"));
            Assert.Equal(1L, ScalarLong(scratch, "SELECT currency_id FROM purchase_orders WHERE id = 2"));
            Assert.Equal(1L, ScalarLong(scratch, "SELECT currency_id FROM purchase_orders WHERE id = 3"));
            Assert.Equal(2L, ScalarLong(scratch, "SELECT currency_id FROM payments WHERE id = 1"));
            Assert.Equal(0.75m, ScalarDecimal(scratch, "SELECT fx_rate FROM payments WHERE id = 1"));
            Assert.Equal(1L, ScalarLong(scratch, "SELECT currency_id FROM payments WHERE id = 2"));
            Assert.Equal(2L, ScalarLong(scratch, "SELECT currency_id FROM vendor_payments WHERE id = 1"));
            Assert.Equal(0.74m, ScalarDecimal(scratch, "SELECT fx_rate FROM vendor_payments WHERE id = 1"));
            Assert.Equal(1L, ScalarLong(scratch, "SELECT currency_id FROM vendor_payments WHERE id = 2"));
            Assert.Equal(1L, ScalarLong(scratch, "SELECT currency_id FROM invoices WHERE id = 1"));
        });
    }

    [Fact]
    public void Apply_MovesDefaultedInvoicesAndBills_OnlyWhenCurrencyOneIsNotFunctional()
    {
        if (string.IsNullOrWhiteSpace(Dsn)) return;

        WithScratchDb("""
            INSERT INTO currencies VALUES (1, 'USD', false, true), (2, 'CAD', true, true);
            INSERT INTO acct_books VALUES (1, 2, true);
            INSERT INTO invoices VALUES (1, 1, 1), (2, 1, 1.36);
            INSERT INTO vendor_bills VALUES (1, 1, 1);
            INSERT INTO acct_ar_open_items VALUES (1, 'Invoice', 1, 1, 1), (2, 'Invoice', 2, 1, 1.36);
            INSERT INTO acct_ap_open_items VALUES (1, 'VendorBill', 1, 1, 1);
            INSERT INTO payments VALUES (1, NULL, 1);
            INSERT INTO payment_applications VALUES (1, 1, 1, 1);
            """, scratch =>
        {
            Assert.Equal(2L, ScalarLong(scratch, "SELECT currency_id FROM invoices WHERE id = 1"));
            Assert.Equal(1L, ScalarLong(scratch, "SELECT currency_id FROM invoices WHERE id = 2"));
            Assert.Equal(2L, ScalarLong(scratch, "SELECT currency_id FROM vendor_bills WHERE id = 1"));
            Assert.Equal(2L, ScalarLong(scratch, "SELECT currency_id FROM acct_ar_open_items WHERE id = 1"));
            Assert.Equal(1L, ScalarLong(scratch, "SELECT currency_id FROM acct_ar_open_items WHERE id = 2"));
            Assert.Equal(2L, ScalarLong(scratch, "SELECT currency_id FROM acct_ap_open_items WHERE id = 1"));
            Assert.Equal(2L, ScalarLong(scratch, "SELECT currency_id FROM payments WHERE id = 1"));
            Assert.Equal(1m, ScalarDecimal(scratch, "SELECT fx_rate FROM payments WHERE id = 1"));
        });
    }

    [Fact]
    public void Apply_DoesNothing_WhenThereIsNoBookAndNoBaseCurrency()
    {
        if (string.IsNullOrWhiteSpace(Dsn)) return;

        WithScratchDb("""
            INSERT INTO currencies VALUES (1, 'USD', false, true);
            INSERT INTO quotes VALUES (1, NULL);
            """, scratch =>
        {
            Assert.Equal(0L, ScalarLong(scratch, "SELECT count(*) FROM quotes WHERE currency_id IS NOT NULL"));
        });
    }

    private static void WithScratchDb(string rows, Action<string> assert)
    {
        var repo = RepoRoot();
        var baseConn = DbUrl.ToNpgsql(Dsn!);
        var admin = new NpgsqlConnectionStringBuilder(baseConn) { Database = "postgres" }.ConnectionString;
        var scratchDb = "forge_currency_backfill_" + DateTime.UtcNow.Ticks;
        var scratch = new NpgsqlConnectionStringBuilder(baseConn) { Database = scratchDb }.ConnectionString;

        Exec(admin, $"CREATE DATABASE \"{scratchDb}\"");
        var root = Path.Combine(Path.GetTempPath(), scratchDb);
        try
        {
            Exec(scratch, Tables);
            Exec(scratch, rows);
            Directory.CreateDirectory(Path.Combine(root, "data"));
            File.Copy(Path.Combine(repo, "data", ScriptName), Path.Combine(root, "data", ScriptName));

            var first = DataSeedRunner.Apply(root, scratch, allowMutation: true);
            Assert.Equal(1, first.Applied);
            assert(scratch);

            Exec(scratch, File.ReadAllText(Path.Combine(repo, "data", ScriptName)));
            assert(scratch);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            Exec(admin, $"DROP DATABASE IF EXISTS \"{scratchDb}\" WITH (FORCE)");
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "data", ScriptName)))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException($"data/{ScriptName} not found above the test output.");
    }

    private static void Exec(string conn, string sql)
    {
        using var c = new NpgsqlConnection(conn);
        c.Open();
        using var cmd = new NpgsqlCommand(sql, c);
        cmd.ExecuteNonQuery();
    }

    private static long ScalarLong(string conn, string sql)
    {
        using var c = new NpgsqlConnection(conn);
        c.Open();
        using var cmd = new NpgsqlCommand(sql, c);
        return Convert.ToInt64(cmd.ExecuteScalar()!);
    }

    private static decimal ScalarDecimal(string conn, string sql)
    {
        using var c = new NpgsqlConnection(conn);
        c.Open();
        using var cmd = new NpgsqlCommand(sql, c);
        return Convert.ToDecimal(cmd.ExecuteScalar()!);
    }
}

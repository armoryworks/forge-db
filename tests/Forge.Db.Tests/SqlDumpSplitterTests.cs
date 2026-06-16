using Forge.Db;
using Xunit;

namespace Forge.Db.Tests;

public class SqlDumpSplitterTests : IDisposable
{
    // A miniature pg_dump --schema-only with one of every object kind the splitter routes, plus the
    // EF bookkeeping table it must skip.
    private const string Dump = """
        --
        -- Name: vector; Type: EXTENSION; Schema: -; Owner: -
        --

        CREATE EXTENSION IF NOT EXISTS vector WITH SCHEMA public;


        --
        -- Name: widgets; Type: TABLE; Schema: public; Owner: -
        --

        CREATE TABLE public.widgets (
            id bigint NOT NULL,
            name character varying(100) NOT NULL
        );


        --
        -- Name: widgets pk_widgets; Type: CONSTRAINT; Schema: public; Owner: -
        --

        ALTER TABLE ONLY public.widgets
            ADD CONSTRAINT pk_widgets PRIMARY KEY (id);


        --
        -- Name: gadgets; Type: TABLE; Schema: public; Owner: -
        --

        CREATE TABLE public.gadgets (
            id bigint NOT NULL,
            widget_id bigint NOT NULL
        );


        --
        -- Name: gadgets fk_gadgets_widget; Type: FK CONSTRAINT; Schema: public; Owner: -
        --

        ALTER TABLE ONLY public.gadgets
            ADD CONSTRAINT fk_gadgets_widget FOREIGN KEY (widget_id) REFERENCES public.widgets(id);


        --
        -- Name: ix_gadgets_widget; Type: INDEX; Schema: public; Owner: -
        --

        CREATE INDEX ix_gadgets_widget ON public.gadgets USING btree (widget_id);


        --
        -- Name: widgets_immutability(); Type: FUNCTION; Schema: public; Owner: -
        --

        CREATE FUNCTION public.widgets_immutability() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NEW; END; $$;


        --
        -- Name: widgets trg_widgets_immutability; Type: TRIGGER; Schema: public; Owner: -
        --

        CREATE TRIGGER trg_widgets_immutability BEFORE UPDATE ON public.widgets FOR EACH ROW EXECUTE FUNCTION public.widgets_immutability();


        --
        -- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: -
        --

        CREATE TABLE public."__EFMigrationsHistory" (
            "MigrationId" character varying(150) NOT NULL
        );


        --
        -- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: -
        --

        ALTER TABLE ONLY public."__EFMigrationsHistory"
            ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");
        """;

    private readonly string _repo = Path.Combine(Path.GetTempPath(), "forge-db-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_repo)) Directory.Delete(_repo, recursive: true);
    }

    [Fact]
    public void Write_RoutesEachObjectToItsDir_AndSkipsEf()
    {
        var result = SqlDumpSplitter.Write(Dump, _repo);

        Assert.True(File.Exists(Path.Combine(_repo, "schema/extensions/vector.sql")));
        Assert.True(File.Exists(Path.Combine(_repo, "schema/tables/widgets.sql")));
        Assert.True(File.Exists(Path.Combine(_repo, "schema/tables/gadgets.sql")));
        Assert.True(File.Exists(Path.Combine(_repo, "schema/indexes/ix_gadgets_widget.sql")));
        Assert.True(File.Exists(Path.Combine(_repo, "schema/functions/widgets_immutability.sql")));
        Assert.True(File.Exists(Path.Combine(_repo, "schema/triggers/trg_widgets_immutability.sql")));

        // EF bookkeeping must NOT be materialized.
        Assert.False(File.Exists(Path.Combine(_repo, "schema/tables/__EFMigrationsHistory.sql")));
        Assert.True(result.EfObjectsSkipped >= 1);
    }

    [Fact]
    public void Write_CoLocatesConstraintsAndFksWithTheirTable()
    {
        SqlDumpSplitter.Write(Dump, _repo);

        var widgets = File.ReadAllText(Path.Combine(_repo, "schema/tables/widgets.sql"));
        Assert.Contains("CREATE TABLE public.widgets", widgets);
        Assert.Contains("pk_widgets", widgets);
        // The trigger ON widgets is NOT part of the table file — it lives under schema/triggers.
        Assert.DoesNotContain("CREATE TRIGGER", widgets);

        var gadgets = File.ReadAllText(Path.Combine(_repo, "schema/tables/gadgets.sql"));
        Assert.Contains("CREATE TABLE public.gadgets", gadgets);
        Assert.Contains("FOREIGN KEY", gadgets);
    }

    [Fact]
    public void Assemble_EmitsFksAfterAllTables_AndExcludesAtlasBlindSpots()
    {
        SqlDumpSplitter.Write(Dump, _repo);
        var sql = DesiredStateAssembler.Assemble(_repo);

        // FK must come after BOTH create-table statements (dependency-safe ordering).
        var fkAt = sql.IndexOf("FOREIGN KEY", StringComparison.Ordinal);
        Assert.True(fkAt > sql.IndexOf("CREATE TABLE public.widgets", StringComparison.Ordinal));
        Assert.True(fkAt > sql.IndexOf("CREATE TABLE public.gadgets", StringComparison.Ordinal));

        // Atlas-blind-spot objects are never handed to Atlas.
        Assert.DoesNotContain("CREATE EXTENSION", sql);
        Assert.DoesNotContain("CREATE FUNCTION", sql);
        Assert.DoesNotContain("CREATE TRIGGER", sql);
    }
}

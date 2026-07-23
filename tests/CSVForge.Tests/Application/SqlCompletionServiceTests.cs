using CSVForge.Application.Sql;

namespace CSVForge.Tests.Application;

public sealed class SqlCompletionServiceTests
{
    private static readonly SqlSchemaSnapshot Schema = new(
    [
        new SqlSchemaObject("Customers", SqlSchemaObjectKind.Table, ["Id", "Full Name", "City"]),
        new SqlSchemaObject("Order", SqlSchemaObjectKind.Table, ["Id", "CustomerId", "Total"]),
        new SqlSchemaObject("customer_summary", SqlSchemaObjectKind.View, ["CustomerId", "OrderCount"])
    ]);

    private readonly SqlCompletionService _service = new();

    [Fact]
    public void GetSuggestions_InGeneralContext_ReturnsKeywordsFunctionsAndSchema()
    {
        SqlCompletionResult result = _service.GetSuggestions("SEL", 3, Schema);

        Assert.Contains(result.Suggestions, item => item.Text == "SELECT" && item.Kind == SqlSuggestionKind.Keyword);
        Assert.Equal(0, result.ReplacementStart);
        Assert.Equal(3, result.ReplacementLength);
    }

    [Fact]
    public void GetSuggestions_AfterFrom_ReturnsTablesAndViews()
    {
        const string sql = "SELECT * FROM ";

        SqlCompletionResult result = _service.GetSuggestions(sql, sql.Length, Schema);

        Assert.Contains(result.Suggestions, item => item.Text == "Customers" && item.Kind == SqlSuggestionKind.Table);
        Assert.Contains(result.Suggestions, item => item.Text == "customer_summary" && item.Kind == SqlSuggestionKind.View);
        Assert.Contains(result.Suggestions, item => item.Text == "\"Order\"");
        Assert.DoesNotContain(result.Suggestions, item => item.Kind == SqlSuggestionKind.Column);
    }

    [Fact]
    public void GetSuggestions_AfterFromMatchesImportedTableByNameFragmentAndDisplayName()
    {
        SqlSchemaSnapshot importedSchema = new(
        [
            new SqlSchemaObject(
                "import_Raport_20260723",
                SqlSchemaObjectKind.Table,
                ["PPE"],
                "Raport energii")
        ]);
        const string sql = "SELECT PPE FROM Ra";

        SqlCompletionResult result = _service.GetSuggestions(sql, sql.Length, importedSchema);

        SqlSuggestion table = Assert.Single(result.Suggestions);
        Assert.Equal("import_Raport_20260723", table.Text);
        Assert.Contains("Raport energii", table.Description);
    }

    [Fact]
    public void GetSuggestions_AfterAliasDot_ReturnsOnlyColumnsFromAliasedTable()
    {
        const string sql = "SELECT c. FROM Customers AS c JOIN \"Order\" AS o ON c.Id = o.CustomerId";
        int caret = sql.IndexOf("c.", StringComparison.Ordinal) + 2;

        SqlCompletionResult result = _service.GetSuggestions(sql, caret, Schema);

        Assert.Contains(result.Suggestions, item => item.Text == "Id");
        Assert.Contains(result.Suggestions, item => item.Text == "\"Full Name\"");
        Assert.DoesNotContain(result.Suggestions, item => item.Text == "Total");
        Assert.All(result.Suggestions, item => Assert.Equal(SqlSuggestionKind.Column, item.Kind));
    }

    [Fact]
    public void GetSuggestions_UsesStatementAtCaretInMultiStatementScript()
    {
        const string sql = "SELECT * FROM Customers; SELECT o. FROM \"Order\" o;";
        int caret = sql.IndexOf("o.", StringComparison.Ordinal) + 2;

        SqlCompletionResult result = _service.GetSuggestions(sql, caret, Schema);

        Assert.Contains(result.Suggestions, item => item.Text == "CustomerId");
        Assert.DoesNotContain(result.Suggestions, item => item.Text == "City");
    }

    [Fact]
    public void GetSuggestions_RecognizesJoinAliasWhenFirstTableHasNoAlias()
    {
        const string sql = "SELECT o. FROM Customers JOIN \"Order\" o ON Customers.Id = o.CustomerId";
        int caret = sql.IndexOf("o.", StringComparison.Ordinal) + 2;

        SqlCompletionResult result = _service.GetSuggestions(sql, caret, Schema);

        Assert.Contains(result.Suggestions, item => item.Text == "Total");
        Assert.DoesNotContain(result.Suggestions, item => item.Text == "City");
    }

    [Theory]
    [InlineData("simple_name", "simple_name")]
    [InlineData("Full Name", "\"Full Name\"")]
    [InlineData("Order", "\"Order\"")]
    [InlineData("a\"b", "\"a\"\"b\"")]
    public void QuoteIdentifier_QuotesOnlyWhenRequired(string input, string expected)
    {
        Assert.Equal(expected, SqlCompletionService.QuoteIdentifier(input));
    }

    [Fact]
    public void GetSuggestions_ForIncompleteSql_StillReturnsFallbackSuggestions()
    {
        const string sql = "SELECT ( FROM Customers WHERE ";

        SqlCompletionResult result = _service.GetSuggestions(sql, sql.Length, Schema);

        Assert.Contains(result.Suggestions, item => item.Text == "AND");
        Assert.Contains(result.Suggestions, item => item.Text == "City");
    }

    [Fact]
    public void GetSuggestions_WithLargeSchema_RemainsBoundedAndResponsive()
    {
        SqlSchemaSnapshot largeSchema = new(
            Enumerable.Range(1, 250)
                .Select(table => new SqlSchemaObject(
                    $"Table_{table}",
                    SqlSchemaObjectKind.Table,
                    Enumerable.Range(1, 20).Select(column => $"Column_{column}").ToArray()))
                .ToArray());
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        SqlCompletionResult result = _service.GetSuggestions("SELECT Col", 10, largeSchema);

        stopwatch.Stop();
        Assert.InRange(result.Suggestions.Count, 1, 200);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Completion took {stopwatch.Elapsed}.");
    }
}

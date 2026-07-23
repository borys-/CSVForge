namespace CSVForge.Tests.Cli;

public sealed class CliApplicationTests
{
    [Fact]
    public async Task RunAsync_Help_ReturnsSuccess()
    {
        int exitCode = await CliApplication.RunAsync(["--help"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_MissingRequiredOption_ReturnsParameterError()
    {
        int exitCode = await CliApplication.RunAsync(["workspace", "--action", "create"]);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_OperationalFailure_ReturnsGeneralError()
    {
        string missingWorkspace = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"), "missing.db");

        int exitCode = await CliApplication.RunAsync(["list-tables", "--workspace", missingWorkspace]);

        Assert.Equal(1, exitCode);
    }
}

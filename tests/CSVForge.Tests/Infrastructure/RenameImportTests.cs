using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Domain.Imports;
using CSVForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Tests.Infrastructure;

public sealed class RenameImportTests
{
    [Fact]
    public async Task RenameImportUseCase_PersistsTrimmedDisplayName()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string csvPath = Path.Combine(directory, "data.csv");
        await File.WriteAllTextAsync(csvPath, "Id\r\n1\r\n");
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(Path.Combine(directory, "workspace.db"));
        ImportResult import = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "Old", true, null, null));

        await provider.GetRequiredService<IRenameImportUseCase>().ExecuteAsync(import.Import.Id, " New name ");

        Assert.Equal("New name", Assert.Single(await provider.GetRequiredService<IListImportedTablesUseCase>().ExecuteAsync()).DisplayName);
    }
}

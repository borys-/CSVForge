using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Ports;
using CSVForge.Domain.Imports;
using CSVForge.Domain.Workspaces;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Tests.Application;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_RegistersWorkspaceUseCases()
    {
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IWorkspaceService, FakeWorkspaceService>()
            .AddApplication()
            .BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ICreateWorkspaceUseCase>());
        Assert.NotNull(provider.GetRequiredService<IOpenWorkspaceUseCase>());
        Assert.NotNull(provider.GetRequiredService<IListImportedTablesUseCase>());
    }

    private sealed class FakeWorkspaceService : IWorkspaceService
    {
        public Task<Workspace> CreateAsync(string workspacePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(new Workspace(workspacePath, "Test", DateTimeOffset.UnixEpoch));
        }

        public Task<IReadOnlyList<CsvImport>> ListImportsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<CsvImport>>(Array.Empty<CsvImport>());
        }

        public Task<Workspace> OpenAsync(string workspacePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(new Workspace(workspacePath, "Test", DateTimeOffset.UnixEpoch));
        }
    }
}

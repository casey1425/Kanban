using CommunityToolkit.Mvvm.Messaging;
using Kanban.App.Services;
using Kanban.App.ViewModels;
using Kanban.Core;
using Kanban.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Kanban.Tests.Support;

/// <summary>
/// 테스트마다 임시 폴더에 SQLite DB와 설정 파일을 만든다.
/// CreateViewModel()을 다시 부르면 "앱을 껐다 켠" 것과 같다(같은 파일을 새 인스턴스로 연다).
/// </summary>
public sealed class TestApp : IDisposable
{
    public string Directory { get; } = Path.Combine(Path.GetTempPath(), $"kanban-tests-{Guid.NewGuid():N}");

    public string DatabasePath => Path.Combine(Directory, "kanban.db");

    public string SettingsPath => Path.Combine(Directory, "settings.json");

    public TestApp()
    {
        System.IO.Directory.CreateDirectory(Directory);
        WeakReferenceMessenger.Default.Reset();
    }

    public IBoardRepository CreateRepository() =>
        new ServiceCollection()
            .AddKanbanData(DatabasePath)
            .BuildServiceProvider()
            .GetRequiredService<IBoardRepository>();

    public MainWindowViewModel CreateViewModel() =>
        new ServiceCollection()
            .AddKanbanData(DatabasePath)
            .AddSingleton<ISettingsService>(new JsonSettingsService(SettingsPath))
            .AddSingleton<MainWindowViewModel>()
            .BuildServiceProvider()
            .GetRequiredService<MainWindowViewModel>();

    /// <summary>ViewModel을 만들고 데이터를 불러온 상태로 돌려준다.</summary>
    public async Task<MainWindowViewModel> StartAsync()
    {
        var viewModel = CreateViewModel();
        await viewModel.InitializeAsync();
        return viewModel;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        WeakReferenceMessenger.Default.Reset();
        try
        {
            if (System.IO.Directory.Exists(Directory))
                System.IO.Directory.Delete(Directory, recursive: true);
            else if (File.Exists(Directory))
                File.Delete(Directory);
        }
        catch (IOException)
        {
            // 임시 폴더 정리 실패는 테스트 결과와 무관하다.
        }
    }
}

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Kanban.Data;
using Avalonia.Markup.Xaml;
using Kanban.App.ViewModels;
using Kanban.App.Views;

namespace Kanban.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            services.AddKanbanData(KanbanDatabase.DefaultPath());
            services.AddSingleton<MainWindowViewModel>();
            _services = services.BuildServiceProvider();

            var viewModel = _services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = viewModel };

            // 창은 먼저 띄우고, 데이터는 비동기로 불러온다.
            _ = viewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
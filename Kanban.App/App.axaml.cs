using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Styling;
using Kanban.App.Services;
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
            var databasePath = KanbanDatabase.DefaultPath();
            services.AddKanbanData(databasePath);
            services.AddSingleton<ISettingsService>(
                new JsonSettingsService(Path.Combine(Path.GetDirectoryName(databasePath)!, "settings.json")));
            services.AddSingleton<MainWindowViewModel>();
            _services = services.BuildServiceProvider();

            var viewModel = _services.GetRequiredService<MainWindowViewModel>();

            ApplyTheme(viewModel.ThemeMode);
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainWindowViewModel.ThemeMode))
                    ApplyTheme(viewModel.ThemeMode);
            };
            desktop.MainWindow = new MainWindow { DataContext = viewModel };

            // 창은 먼저 띄우고, 데이터는 비동기로 불러온다.
            _ = viewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ApplyTheme(ThemeMode mode)
    {
        RequestedThemeVariant = mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default, // 운영체제 설정을 따른다
        };
    }
}
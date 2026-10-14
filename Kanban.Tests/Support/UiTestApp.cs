using Avalonia;
using Avalonia.Headless;
using Kanban.Tests.Support;

[assembly: AvaloniaTestApplication(typeof(UiTestApp))]

namespace Kanban.Tests.Support;

/// <summary>화면(XAML) 테스트용 헤드리스 앱. 실제 창 없이 비주얼 트리만 만든다.</summary>
public class UiTestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Kanban.App.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

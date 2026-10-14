namespace Kanban.App.Services;

public enum ThemeMode
{
    /// <summary>운영체제 설정을 따른다.</summary>
    System,
    Light,
    Dark,
}

/// <summary>사용자 설정. JSON 파일로 저장된다.</summary>
public sealed class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.System;
}

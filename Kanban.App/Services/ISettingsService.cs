namespace Kanban.App.Services;

public interface ISettingsService
{
    /// <summary>현재 설정. 파일이 없거나 읽을 수 없으면 기본값이다.</summary>
    AppSettings Current { get; }

    void Save();
}

namespace Kanban.Data;

public static class KanbanDatabase
{
    /// <summary>OS별 사용자 데이터 폴더 아래의 기본 DB 경로. 폴더가 없으면 만든다.</summary>
    public static string DefaultPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kanban");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "kanban.db");
    }
}

using Avalonia.Input;
using Kanban.App.ViewModels;

namespace Kanban.App;

/// <summary>드래그 앤 드롭에 쓰는 앱 전용 데이터 형식. 프로세스 밖으로는 나가지 않는다.</summary>
public static class DragFormats
{
    public static readonly DataFormat<CardViewModel> Card =
        DataFormat.CreateInProcessFormat<CardViewModel>("kanban-card");
}

using System.Threading.Tasks;
using Kanban.App.ViewModels;

namespace Kanban.App.Undo;

/// <summary>되돌릴 수 있는 사용자 작업 하나. 이미 실행된 작업을 기록해 두었다가 실행 취소/다시 실행에 쓴다.</summary>
public interface IUndoableAction
{
    /// <summary>"카드 삭제"처럼 사용자에게 보여 줄 짧은 설명.</summary>
    string Description { get; }

    /// <summary>
    /// 이 작업이 카드 상세 패널 안에서 일어나는 변경(라벨, 체크리스트)이면 그 카드. 그 외에는 null.
    /// 패널이 열려 있어도 "열려 있는 바로 그 카드"의 작업이면 되돌릴 수 있게 하는 데 쓴다.
    /// </summary>
    CardViewModel? DetailScope { get; }

    /// <summary>실행 취소가 화면에 나타나는 보드. 되돌리기 전에 그 보드로 이동한다. null이면 이동하지 않는다.</summary>
    int? UndoBoardId { get; }

    /// <summary>다시 실행이 화면에 나타나는 보드.</summary>
    int? RedoBoardId { get; }

    /// <summary>작업을 되돌린다. 실패하면 예외를 던진다. 이때 새 기록을 남기면 안 된다.</summary>
    Task UndoAsync();

    /// <summary>되돌린 작업을 다시 실행한다. 실패하면 예외를 던진다. 이때 새 기록을 남기면 안 된다.</summary>
    Task RedoAsync();
}

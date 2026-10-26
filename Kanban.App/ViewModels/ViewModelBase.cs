using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Kanban.App.Messages;
using Kanban.App.Undo;

namespace Kanban.App.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    /// <summary>
    /// 저장 같은 비동기 작업을 실행하고, 실패하면 앱이 종료되는 대신 오류 알림 메시지를 보낸다.
    /// what은 "카드를 삭제하지 못했어요"처럼 사용자에게 보여 줄 작업 설명이다.
    /// </summary>
    /// <summary>방금 성공한 작업을 실행 취소 기록에 남긴다.</summary>
    protected static void Record(IUndoableAction action) =>
        WeakReferenceMessenger.Default.Send(new ActionPerformed(action));

    protected static async Task RunSafeAsync(Func<Task> action, string what) =>
        await TryRunAsync(action, what);

    /// <summary>RunSafeAsync와 같지만, 성공했는지(true) 실패해서 오류 메시지를 보냈는지(false) 알려 준다.</summary>
    protected static async Task<bool> TryRunAsync(Func<Task> action, string what)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new ErrorOccurred($"{what}: {ex.Message}"));
            return false;
        }
    }
}

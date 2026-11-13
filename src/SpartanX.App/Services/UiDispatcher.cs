using Microsoft.UI.Dispatching;
using SpartanX.Core.Abstractions;

namespace SpartanX.App.Services;

internal sealed class UiDispatcher(DispatcherQueue queue) : IUiDispatcher
{
    public bool HasThreadAccess => queue.HasThreadAccess;

    public void Post(Action action)
    {
        if (queue.HasThreadAccess)
        {
            action();
        }
        else
        {
            queue.TryEnqueue(() => action());
        }
    }
}

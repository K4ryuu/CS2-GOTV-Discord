using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace K4GOTV;

public sealed partial class Plugin : BasePlugin
{
	public static class CSSThread
	{
		public static void RunOnMainThread(Action callback)
		{
			using SyncContextScope synchronizationContext = new SyncContextScope();
			callback.Invoke();
		}

		public static async Task RunOnMainThreadAsync(Func<Task> callback)
		{
			await new Func<Task>(async () =>
			{
				using SyncContextScope synchronizationContext = new SyncContextScope();
				await callback.Invoke();
			}).Invoke();
		}
	}

	public class SourceSynchronizationContext : SynchronizationContext
	{
		public override void Post(SendOrPostCallback callback, object? state)
		{
			Server.NextWorldUpdate(() => callback(state));
		}

		public override SynchronizationContext CreateCopy()
		{
			return this;
		}
	}

	public class SyncContextScope : IDisposable
	{
		private static SynchronizationContext _sourceContext = new SourceSynchronizationContext();

		private SynchronizationContext? _oldContext;

		public SyncContextScope()
		{
			_oldContext = SynchronizationContext.Current;
			SynchronizationContext.SetSynchronizationContext(_sourceContext);
		}

		public void Dispose()
		{
			if (_oldContext != null)
			{
				SynchronizationContext.SetSynchronizationContext(_oldContext);
			}
		}
	}
}
﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Xtate.Annotations;
using Xtate.Persistence;

namespace Xtate
{
	[PublicAPI]
	public sealed partial class StateMachineHost : IAsyncDisposable, IDisposable
	{
		private readonly StateMachineHostOptions  _options;
		private          bool                     _asyncOperationInProgress;
		private          StateMachineHostContext? _context;
		private          bool                     _disposed;

		public StateMachineHost(StateMachineHostOptions options)
		{
			_options = options ?? throw new ArgumentNullException(nameof(options));

			StateMachineHostInit();
		}

	#region Interface IAsyncDisposable

		public async ValueTask DisposeAsync()
		{
			if (_disposed)
			{
				_disposed = true;
			}

			var context = _context;
			_context = null;

			if (context != null)
			{
				await context.DisposeAsync().ConfigureAwait(false);
			}

			await StateMachineHostStopAsync().ConfigureAwait(false);

			_disposed = true;
		}

	#endregion

	#region Interface IDisposable

		public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

	#endregion

		public async Task StartHostAsync(CancellationToken token = default)
		{
			if (_asyncOperationInProgress)
			{
				throw new InvalidOperationException(Resources.Exception_Another_asynchronous_operation_in_progress);
			}

			if (_context != null)
			{
				return;
			}

			var context = _options.StorageProvider != null
					? new StateMachineHostPersistedContext(this, _options)
					: new StateMachineHostContext(this, _options);

			try
			{
				_asyncOperationInProgress = true;

				await StateMachineHostStartAsync(token).ConfigureAwait(false);
				await context.InitializeAsync(token).ConfigureAwait(false);

				_context = context;
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken == token)
			{
				context.Stop();
				await context.DisposeAsync().ConfigureAwait(false);
			}
			finally
			{
				_asyncOperationInProgress = false;
			}
		}

		public async Task StopHostAsync(CancellationToken token = default)
		{
			if (_asyncOperationInProgress)
			{
				throw new InvalidOperationException(Resources.Exception_Another_asynchronous_operation_in_progress);
			}

			var context = _context;
			if (context == null)
			{
				return;
			}

			_asyncOperationInProgress = true;
			_context = null;

			try
			{
				context.Suspend();

				await context.WaitAllAsync(token).ConfigureAwait(false);
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken == token)
			{
				context.Stop();
			}
			finally
			{
				await context.DisposeAsync().ConfigureAwait(false);
				await StateMachineHostStopAsync().ConfigureAwait(false);

				_asyncOperationInProgress = false;
			}
		}

		public async Task WaitAllStateMachinesAsync(CancellationToken token = default)
		{
			var context = _context;

			if (context != null)
			{
				await context.WaitAllAsync(token).ConfigureAwait(false);
			}
		}

		public ValueTask<DataModelValue> ExecuteStateMachineAsync(string scxml, DataModelValue parameters = default) => ExecuteStateMachine(SessionId.New(), new StateMachineOrigin(scxml), parameters);

		public ValueTask<DataModelValue> ExecuteStateMachineAsync(Uri source, DataModelValue parameters = default) => ExecuteStateMachine(SessionId.New(), new StateMachineOrigin(source), parameters);

		public ValueTask<DataModelValue> ExecuteStateMachineAsync(IStateMachine stateMachine, DataModelValue parameters = default) =>
				ExecuteStateMachine(SessionId.New(), new StateMachineOrigin(stateMachine), parameters);

		public ValueTask<DataModelValue> ExecuteStateMachineAsync(string scxml, Uri? baseUri, DataModelValue parameters = default) =>
				ExecuteStateMachine(SessionId.New(), new StateMachineOrigin(scxml, baseUri), parameters);

		public ValueTask<DataModelValue> ExecuteStateMachineAsync(Uri source, Uri? baseUri, DataModelValue parameters = default) =>
				ExecuteStateMachine(SessionId.New(), new StateMachineOrigin(source, baseUri), parameters);

		public ValueTask<DataModelValue> ExecuteStateMachineAsync(IStateMachine stateMachine, Uri? baseUri, DataModelValue parameters = default) =>
				ExecuteStateMachine(SessionId.New(), new StateMachineOrigin(stateMachine, baseUri), parameters);

		public ValueTask<DataModelValue> ExecuteStateMachineAsync(string scxml, string sessionId, DataModelValue parameters = default) =>
				ExecuteStateMachine(SessionId.FromString(sessionId), new StateMachineOrigin(scxml), parameters);

		public ValueTask<DataModelValue> ExecuteStateMachineAsync(Uri source, string sessionId, DataModelValue parameters = default) =>
				ExecuteStateMachine(SessionId.FromString(sessionId), new StateMachineOrigin(source), parameters);

		public ValueTask<DataModelValue> ExecuteStateMachineAsync(IStateMachine stateMachine, string sessionId, DataModelValue parameters = default) =>
				ExecuteStateMachine(SessionId.FromString(sessionId), new StateMachineOrigin(stateMachine), parameters);

		public ValueTask<DataModelValue> ExecuteStateMachineAsync(string scxml, Uri? baseUri, string sessionId, DataModelValue parameters = default) =>
				ExecuteStateMachine(SessionId.FromString(sessionId), new StateMachineOrigin(scxml, baseUri), parameters);

		public ValueTask<DataModelValue> ExecuteStateMachineAsync(Uri source, Uri? baseUri, string sessionId, DataModelValue parameters = default) =>
				ExecuteStateMachine(SessionId.FromString(sessionId), new StateMachineOrigin(source, baseUri), parameters);

		public ValueTask<DataModelValue> ExecuteStateMachineAsync(IStateMachine stateMachine, Uri? baseUri, string sessionId, DataModelValue parameters = default) =>
				ExecuteStateMachine(SessionId.FromString(sessionId), new StateMachineOrigin(stateMachine, baseUri), parameters);

		public ValueTask StartStateMachineAsync(string scxml, DataModelValue parameters = default) => StartStateMachineWrapper(SessionId.New(), new StateMachineOrigin(scxml), parameters);

		public ValueTask StartStateMachineAsync(Uri source, DataModelValue parameters = default) => StartStateMachineWrapper(SessionId.New(), new StateMachineOrigin(source), parameters);

		public ValueTask StartStateMachineAsync(IStateMachine stateMachine, DataModelValue parameters = default) =>
				StartStateMachineWrapper(SessionId.New(), new StateMachineOrigin(stateMachine), parameters);

		public ValueTask StartStateMachineAsync(string scxml, Uri? baseUri, DataModelValue parameters = default) =>
				StartStateMachineWrapper(SessionId.New(), new StateMachineOrigin(scxml, baseUri), parameters);

		public ValueTask StartStateMachineAsync(Uri source, Uri? baseUri, DataModelValue parameters = default) =>
				StartStateMachineWrapper(SessionId.New(), new StateMachineOrigin(source, baseUri), parameters);

		public ValueTask StartStateMachineAsync(IStateMachine stateMachine, Uri? baseUri, DataModelValue parameters = default) =>
				StartStateMachineWrapper(SessionId.New(), new StateMachineOrigin(stateMachine, baseUri), parameters);

		public ValueTask StartStateMachineAsync(string scxml, string sessionId, DataModelValue parameters = default) =>
				StartStateMachineWrapper(SessionId.FromString(sessionId), new StateMachineOrigin(scxml), parameters);

		public ValueTask StartStateMachineAsync(Uri source, string sessionId, DataModelValue parameters = default) =>
				StartStateMachineWrapper(SessionId.FromString(sessionId), new StateMachineOrigin(source), parameters);

		public ValueTask StartStateMachineAsync(IStateMachine stateMachine, string sessionId, DataModelValue parameters = default) =>
				StartStateMachineWrapper(SessionId.FromString(sessionId), new StateMachineOrigin(stateMachine), parameters);

		public ValueTask StartStateMachineAsync(string scxml, Uri? baseUri, string sessionId, DataModelValue parameters = default) =>
				StartStateMachineWrapper(SessionId.FromString(sessionId), new StateMachineOrigin(scxml, baseUri), parameters);

		public ValueTask StartStateMachineAsync(Uri source, Uri? baseUri, string sessionId, DataModelValue parameters = default) =>
				StartStateMachineWrapper(SessionId.FromString(sessionId), new StateMachineOrigin(source, baseUri), parameters);

		public ValueTask StartStateMachineAsync(IStateMachine stateMachine, Uri? baseUri, string sessionId, DataModelValue parameters = default) =>
				StartStateMachineWrapper(SessionId.FromString(sessionId), new StateMachineOrigin(stateMachine, baseUri), parameters);

		private async ValueTask StartStateMachineWrapper(SessionId sessionId, StateMachineOrigin origin, DataModelValue parameters) =>
				await StartStateMachine(sessionId, origin, parameters).ConfigureAwait(false);
	}
}
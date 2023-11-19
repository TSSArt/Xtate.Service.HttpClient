﻿#region Copyright © 2019-2021 Sergii Artemenko

// This file is part of the Xtate project. <https://xtate.net/>
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xtate.IoProcessor;
using Xtate.Persistence;
using Xtate.Service;
using IServiceProvider = Xtate.IoC.IServiceProvider;

namespace Xtate.Core
{
	public class StateMachineControllerProxy : IStateMachineController
	{
		public StateMachineControllerProxy(StateMachineRuntimeController stateMachineRuntimeController) => _baseStateMachineController = stateMachineRuntimeController;

		private readonly IStateMachineController _baseStateMachineController;

		public ValueTask Send(IEvent evt, CancellationToken token = default) => _baseStateMachineController.Send(evt, token);

		ValueTask<DataModelValue> IService.GetResult(CancellationToken token) => _baseStateMachineController.GetResult(token);

		public ValueTask Destroy(CancellationToken token) => _baseStateMachineController.Destroy(token);

		public ValueTask DisposeAsync() => _baseStateMachineController.DisposeAsync();

		public SessionId SessionId              => _baseStateMachineController.SessionId;
		public Uri       StateMachineLocation   => _baseStateMachineController.StateMachineLocation;
		public void      TriggerDestroySignal() => _baseStateMachineController.TriggerDestroySignal();


		public ValueTask StartAsync(CancellationToken token) => _baseStateMachineController.StartAsync(token);

		ValueTask<DataModelValue> IStateMachineController.GetResult(CancellationToken token) => _baseStateMachineController.GetResult(token);
	}

	public abstract class StateMachineControllerBase : IStateMachineController, IService, IExternalCommunication, INotifyStateChanged, IAsyncDisposable, IInvokeController
	{
		private readonly TaskCompletionSource<int>            _acceptedTcs  = new();
		private readonly TaskCompletionSource<DataModelValue> _completedTcs = new();
		private readonly InterpreterOptions                   _defaultOptions;
		private readonly CancellationTokenSource              _destroyTokenSource;
		private readonly DeferredFinalizer                    _finalizer;
		private readonly IStateMachineOptions?                _options;
		private readonly ISecurityContext                     _securityContext;
		private readonly IStateMachine?                       _stateMachine;
		private readonly IStateMachineHost                    _stateMachineHost;

		public required Func<ValueTask<IStateMachineInterpreter>> _stateMachineInterpreterFactory { private get; init; }
		public required IServiceProvider sd { private get; init; } //TODO:delete

		protected StateMachineControllerBase(SessionId sessionId,
											 IStateMachineOptions? options,
											 IStateMachine? stateMachine,
											 Uri? stateMachineLocation,
											 IStateMachineHost stateMachineHost,
											 InterpreterOptions defaultOptions,
											 ISecurityContext securityContext,
											 DeferredFinalizer finalizer)
		{
			SessionId = sessionId;
			StateMachineLocation = stateMachineLocation;
			_options = options;
			_stateMachine = stateMachine;
			_stateMachineHost = stateMachineHost;
			_defaultOptions = defaultOptions;
			_securityContext = securityContext;
			_finalizer = finalizer;

			_destroyTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_defaultOptions.DestroyToken, token2: default);
		}

		protected abstract Channel<IEvent>   EventChannel { get; }
		public required    IEventQueueWriter EventQueueWriter  { private get; init; }

		public Uri? StateMachineLocation { get; }

		public SessionId SessionId { get; }

	#region Interface IAsyncDisposable

		public async ValueTask DisposeAsync()
		{
			await DisposeAsyncCore().ConfigureAwait(false);

			GC.SuppressFinalize(this);
		}

	#endregion

	#region Interface IEventDispatcher

		//public virtual ValueTask Send(IEvent evt, CancellationToken token) => EventChannel.Writer.WriteAsync(evt, token);
		public virtual ValueTask Send(IEvent evt, CancellationToken token) => EventQueueWriter.WriteAsync(evt);

	#endregion

	#region Interface IExternalCommunication

		ValueTask<SendStatus> IExternalCommunication.TrySendEvent(IOutgoingEvent outgoingEvent) => _stateMachineHost.DispatchEvent(SessionId, outgoingEvent, CancellationToken.None);

		ValueTask IExternalCommunication.CancelEvent(SendId sendId) => _stateMachineHost.CancelEvent(SessionId, sendId, CancellationToken.None);

		ValueTask IExternalCommunication.StartInvoke(InvokeData invokeData) => _stateMachineHost.StartInvoke(SessionId, invokeData, _securityContext, CancellationToken.None);

		ValueTask IExternalCommunication.CancelInvoke(InvokeId invokeId) => _stateMachineHost.CancelInvoke(SessionId, invokeId, CancellationToken.None);

		ValueTask IExternalCommunication.ForwardEvent(IEvent evt, InvokeId invokeId) => _stateMachineHost.ForwardEvent(SessionId, evt, invokeId, CancellationToken.None);

	#endregion

		ValueTask IInvokeController.Start(InvokeData invokeData) => _stateMachineHost.StartInvoke(SessionId, invokeData, _securityContext, CancellationToken.None);

		ValueTask IInvokeController.Cancel(InvokeId invokeId) => _stateMachineHost.CancelInvoke(SessionId, invokeId, CancellationToken.None);


	#region Interface INotifyStateChanged

		ValueTask INotifyStateChanged.OnChanged(StateMachineInterpreterState state)
		{
			StateChanged(state);

			if (state == StateMachineInterpreterState.Accepted)
			{
				_acceptedTcs.TrySetResult(0);
			}

			return default;
		}

	#endregion

	#region Interface IService

		ValueTask IService.Destroy(CancellationToken token)
		{
			TriggerDestroySignal();

			//TODO: Wait StateMachine destroyed

			return default;
		}

	#endregion

	#region Interface IStateMachineController

		public ValueTask<DataModelValue> GetResult(CancellationToken token) => _completedTcs.WaitAsync(token);

	#endregion

		protected virtual void StateChanged(StateMachineInterpreterState state) { }

		protected virtual ValueTask DisposeAsyncCore()
		{
			_destroyTokenSource.Dispose();

			return default;
		}

		public async ValueTask StartAsync(CancellationToken token)
		{
			ExecuteAsync().Forget();

			await _acceptedTcs.WaitAsync(token).ConfigureAwait(false);
		}

		private InterpreterOptions GetOptions() =>
			_defaultOptions with
			{
				ExternalCommunication = this,
				StorageProvider = this as IStorageProvider,
				NotifyStateChanged = this,
				SecurityContext = _securityContext,
				DestroyToken = _destroyTokenSource.Token,
				SuspendToken = GetSuspendToken(),
				UnhandledErrorBehaviour = _options?.UnhandledErrorBehaviour is { } behaviour ? behaviour : _defaultOptions.UnhandledErrorBehaviour
			};

		protected virtual CancellationToken GetSuspendToken() => _defaultOptions.SuspendToken;

		protected virtual ValueTask Initialize() => default;

		private async ValueTask<DataModelValue> ExecuteAsync()
		{
			//_finalizer.DefferFinalization();
			var initialized = false;
			while (true)
			{
				try
				{
					if (!initialized)
					{
						initialized = true;

						await Initialize().ConfigureAwait(false);
					}

					try
					{
						//var stateMachineInterpreter = _defaultOptions.ServiceLocator.GetService<IStateMachineInterpreter>();
						var stateMachineInterpreter = await _stateMachineInterpreterFactory().ConfigureAwait(false);
						var result = await stateMachineInterpreter.RunAsync().ConfigureAwait(false);

						//var result = await stateMachineInterpreter.RunAsync(SessionId, _stateMachine, EventChannel.Reader, GetOptions()).ConfigureAwait(false);
						//await _finalizer.ExecuteDeferredFinalization().ConfigureAwait(false);
						_acceptedTcs.TrySetResult(0);
						_completedTcs.TrySetResult(result);

						return result;
					}
					catch (StateMachineSuspendedException) when (!_defaultOptions.SuspendToken.IsCancellationRequested) { }
					catch(Exception s)
					{
						throw;
					}

					await WaitForResume().ConfigureAwait(false);
				}
				catch (OperationCanceledException ex)
				{
					//await _finalizer.ExecuteDeferredFinalization().ConfigureAwait(false);
					_acceptedTcs.TrySetCanceled(ex.CancellationToken);
					_completedTcs.TrySetCanceled(ex.CancellationToken);

					throw;
				}
				catch (Exception ex)
				{
					//await _finalizer.ExecuteDeferredFinalization().ConfigureAwait(false);
					_acceptedTcs.TrySetException(ex);
					_completedTcs.TrySetException(ex);

					throw;
				}
			}
		}

		public void TriggerDestroySignal() => _destroyTokenSource.Cancel();

		private async ValueTask WaitForResume()
		{
			var anyTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_defaultOptions.StopToken, _defaultOptions.DestroyToken, _defaultOptions.SuspendToken);
			try
			{
				if (await EventChannel.Reader.WaitToReadAsync(anyTokenSource.Token).ConfigureAwait(false))
				{
					return;
				}

				await EventChannel.Reader.ReadAsync(anyTokenSource.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken == anyTokenSource.Token && _defaultOptions.StopToken.IsCancellationRequested)
			{
				throw new OperationCanceledException(Resources.Exception_StateMachineHasBeenHalted, ex, _defaultOptions.StopToken);
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken == anyTokenSource.Token && _defaultOptions.SuspendToken.IsCancellationRequested)
			{
				throw new StateMachineSuspendedException(Resources.Exception_StateMachineHasBeenSuspended, ex);
			}
			catch (ChannelClosedException ex)
			{
				throw new StateMachineQueueClosedException(Resources.Exception_StateMachineExternalQueueHasBeenClosed, ex);
			}
			finally
			{
				anyTokenSource.Dispose();
			}
		}
	}
}
﻿using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace TSSArt.StateMachine
{
	[PublicAPI]
	public interface IStartInvokeEvaluator
	{
		ValueTask<(string InvokeId, string InvokeUniqueId)> Start(string stateId, IExecutionContext executionContext, CancellationToken token);
	}
}
﻿using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace TSSArt.StateMachine
{
	[PublicAPI]
	public interface ICancelInvokeEvaluator
	{
		ValueTask Cancel(string invokeId, IExecutionContext executionContext, CancellationToken token);
	}
}
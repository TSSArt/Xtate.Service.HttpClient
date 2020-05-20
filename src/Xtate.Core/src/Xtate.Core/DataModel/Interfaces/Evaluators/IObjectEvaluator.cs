﻿using System.Threading;
using System.Threading.Tasks;
using Xtate.Annotations;

namespace Xtate
{
	[PublicAPI]
	public interface IObjectEvaluator : IValueEvaluator
	{
		ValueTask<IObject> EvaluateObject(IExecutionContext executionContext, CancellationToken token);
	}
}
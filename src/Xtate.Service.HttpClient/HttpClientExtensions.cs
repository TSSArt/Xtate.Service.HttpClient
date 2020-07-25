using System;
using Xtate.Service;

namespace Xtate
{
	public static class HttpClientExtensions
	{
		public static StateMachineHostBuilder AddHttpClient(this StateMachineHostBuilder builder)
		{
			if (builder == null) throw new ArgumentNullException(nameof(builder));

			builder.AddServiceFactory(HttpClientService.Factory);

			return builder;
		}
	}
}
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
using Xtate.Core;
using Xtate.IoC;
using Xtate.DataModel;
using Xtate.DataModel.XPath;

namespace Xtate
{
	[PublicAPI]
	public static class XPathExtensions
	{
		public static IServiceCollection AddXPath(this IServiceCollection services)
		{
			if (services is null) throw new ArgumentNullException(nameof(services));

			//services.AddIErrorProcessorService<XPathDataModelHandler>();

			//services.AddTransient(
				//async provider => new XPathDataModelHandler(await provider.GetRequiredService<IErrorProcessorService<XPathDataModelHandler>>().ConfigureAwait(false)));

				services.AddType<XPathDataModelHandler>();

			//TODO:delete
			/*services.AddForwarding<IDataModelHandler?, string?>(
				async (provider, dataModel) => dataModel == XPathDataModelHandler.DataModelType
					? await provider.GetRequiredService<XPathDataModelHandler>().ConfigureAwait(false)
					: default);*/

			services.AddShared<IDataModelHandlerProvider>(SharedWithin.Container, sp => new XPathDataModelHandlerProvider
																					    {
																						    DataModelHandlerFactory = sp.GetRequiredFactory<XPathDataModelHandler>()
																					    });

			return services;
		}
	}
}
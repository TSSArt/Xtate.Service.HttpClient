﻿using System.Collections.Immutable;
using System.Threading;

namespace TSSArt.StateMachine
{
	public struct InterpreterOptions
	{
		public ImmutableArray<IDataModelHandlerFactory> DataModelHandlerFactories { get; set; }
		public ImmutableArray<ICustomActionFactory>    CustomActionProviders     { get; set; }
		public ImmutableDictionary<string, string>      Configuration             { get; set; }
		public DataModelValue                           Arguments                 { get; set; }
		public IExternalCommunication                   ExternalCommunication     { get; set; }
		public INotifyStateChanged                      NotifyStateChanged        { get; set; }
		public CancellationToken                        SuspendToken              { get; set; }
		public CancellationToken                        StopToken                 { get; set; }
		public CancellationToken                        DestroyToken              { get; set; }
		public IResourceLoader                          ResourceLoader            { get; set; }
		public PersistenceLevel                         PersistenceLevel          { get; set; }
		public IStorageProvider                         StorageProvider           { get; set; }
		public ILogger                                  Logger                    { get; set; }
	}
}
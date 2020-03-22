﻿using System;
using System.Collections.Immutable;

namespace TSSArt.StateMachine
{
	public sealed class DefaultErrorProcessor : IErrorProcessor
	{
		public static readonly IErrorProcessor Instance = new DefaultErrorProcessor();

		private DefaultErrorProcessor()
		{ }

		void IErrorProcessor.AddError(ErrorItem errorItem)
		{
			if (errorItem == null) throw new ArgumentNullException(nameof(errorItem));

			throw new StateMachineValidationException(ImmutableArray.Create(errorItem));
		}

		bool IErrorProcessor.LineInfoRequired => false;

		public void ThrowIfErrors()
		{ }
	}
}
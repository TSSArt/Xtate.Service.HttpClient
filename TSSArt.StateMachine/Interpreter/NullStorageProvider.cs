﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace TSSArt.StateMachine
{
	internal sealed class NullStorageProvider : IStorageProvider, ITransactionalStorage
	{
		public static readonly NullStorageProvider Instance = new NullStorageProvider();

		public ValueTask<ITransactionalStorage> GetTransactionalStorage(string? partition, string key, CancellationToken token) => new ValueTask<ITransactionalStorage>(Instance);

		public ValueTask RemoveTransactionalStorage(string? partition, string key, CancellationToken token) => default;

		public ValueTask RemoveAllTransactionalStorage(string? partition, CancellationToken token) => default;

		public void Write(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{ }

		public ValueTask CheckPoint(int level, CancellationToken token) => default;

		public void Dispose()
		{ }

		public ValueTask DisposeAsync() => default;

		public ReadOnlyMemory<byte> Read(ReadOnlySpan<byte> key) => ReadOnlyMemory<byte>.Empty;

		public ValueTask Shrink(CancellationToken token) => default;
	}
}
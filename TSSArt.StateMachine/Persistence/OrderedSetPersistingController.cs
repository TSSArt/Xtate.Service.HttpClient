﻿using System;
using System.Collections.Immutable;

namespace TSSArt.StateMachine
{
	internal sealed class OrderedSetPersistingController<T> : IDisposable where T : class
	{
		private readonly Bucket        _bucket;
		private readonly OrderedSet<T> _orderedSet;
		private          int           _record;

		public OrderedSetPersistingController(Bucket bucket, OrderedSet<T> orderedSet, ImmutableDictionary<int, IEntity> entityMap)
		{
			if (entityMap == null) throw new ArgumentNullException(nameof(entityMap));
			_bucket = bucket;
			_orderedSet = orderedSet ?? throw new ArgumentNullException(nameof(orderedSet));

			var shrink = !orderedSet.IsEmpty;
			while (true)
			{
				var recordBucket = bucket.Nested(_record);

				if (!recordBucket.TryGet(Keys.Operation, out Keys operation) ||
					!recordBucket.TryGet(Keys.DocumentId, out int documentId))
				{
					break;
				}

				switch (operation)
				{
					case Keys.Added:
						orderedSet.Add(entityMap[documentId].As<T>());
						break;

					case Keys.Deleted:
						orderedSet.Delete(entityMap[documentId].As<T>());
						shrink = true;
						break;
				}

				_record ++;
			}

			if (shrink)
			{
				bucket.RemoveSubtree(Bucket.RootKey);

				_record = 0;
				foreach (var entity in orderedSet)
				{
					var recordBucket = bucket.Nested(_record ++);
					recordBucket.Add(Keys.DocumentId, entity.As<IDocumentId>().DocumentId);
					recordBucket.Add(Keys.Operation, Keys.Added);
				}
			}

			orderedSet.Changed += OnChanged;
		}

	#region Interface IDisposable

		public void Dispose()
		{
			_orderedSet.Changed -= OnChanged;
		}

	#endregion

		private void OnChanged(OrderedSet<T>.ChangedAction action, T item)
		{
			switch (action)
			{
				case OrderedSet<T>.ChangedAction.Add:
				{
					var bucket = _bucket.Nested(_record ++);
					bucket.Add(Keys.DocumentId, item.As<IDocumentId>().DocumentId);
					bucket.Add(Keys.Operation, Keys.Added);
					break;
				}

				case OrderedSet<T>.ChangedAction.Clear:
					_record = 0;
					_bucket.RemoveSubtree(Bucket.RootKey);
					break;

				case OrderedSet<T>.ChangedAction.Delete:
					if (_orderedSet.IsEmpty)
					{
						_record = 0;
						_bucket.RemoveSubtree(Bucket.RootKey);
					}
					else
					{
						var bucket = _bucket.Nested(_record ++);
						bucket.Add(Keys.DocumentId, item.As<IDocumentId>().DocumentId);
						bucket.Add(Keys.Operation, Keys.Deleted);
					}

					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(action), action, message: null);
			}
		}

		private enum Keys
		{
			DocumentId,
			Operation,
			Added,
			Deleted
		}
	}
}
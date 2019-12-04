﻿using System;

namespace TSSArt.StateMachine
{
	public class DataModelArrayPersistingController : DataModelPersistingController
	{
		private readonly Bucket                    _bucket;
		private readonly DataModelArray            _dataModelArray;
		private readonly DataModelReferenceTracker _referenceTracker;
		private          int                       _record;

		public DataModelArrayPersistingController(Bucket bucket, DataModelReferenceTracker referenceTracker, DataModelArray dataModelArray)
		{
			_bucket = bucket;
			_referenceTracker = referenceTracker ?? throw new ArgumentNullException(nameof(referenceTracker));
			_dataModelArray = dataModelArray ?? throw new ArgumentNullException(nameof(dataModelArray));

			var shrink = dataModelArray.Length > 0;
			while (true)
			{
				var recordBucket = bucket.Nested(_record);

				if (!recordBucket.TryGet(Key.Operation, out Key operation))
				{
					break;
				}

				switch (operation)
				{
					case Key.Set when recordBucket.TryGet(Key.Index, out int index):
					{
						var dataModelValue = recordBucket.GetDataModelValue(referenceTracker, dataModelArray[index]);
						dataModelArray.SetInternal(index, dataModelValue);
						referenceTracker.AddReference(dataModelValue);
						break;
					}

					case Key.Insert when recordBucket.TryGet(Key.Index, out int index):
					{
						var dataModelValue = recordBucket.GetDataModelValue(referenceTracker, dataModelArray[index]);
						dataModelArray.InsertInternal(index, dataModelValue);
						referenceTracker.AddReference(dataModelValue);
						break;
					}

					case Key.Remove when recordBucket.TryGet(Key.Index, out int index):
					{
						shrink = true;
						referenceTracker.RemoveReference(dataModelArray[index]);
						dataModelArray.RemoveAtInternal(index);
						break;
					}

					case Key.SetLength when recordBucket.TryGet(Key.Index, out int length):
					{
						shrink = length < dataModelArray.Length;
						for (var i = length; i < dataModelArray.Length; i ++)
						{
							referenceTracker.RemoveReference(dataModelArray[i]);
						}

						dataModelArray.SetLengthInternal(length);
						break;
					}

					default: throw new ArgumentOutOfRangeException();
				}

				_record ++;
			}

			if (shrink)
			{
				bucket.RemoveSubtree(Bucket.RootKey);
				if (dataModelArray.IsReadOnly)
				{
					bucket.Add(Key.ReadOnly, value: true);
				}

				_record = 0;
				for (var i = 0; i < dataModelArray.Length; i ++)
				{
					var value = dataModelArray[i];
					if (value.Type != DataModelValueType.Undefined)
					{
						var recordBucket = bucket.Nested(_record ++);
						recordBucket.Add(Key.Operation, Key.Set);
						recordBucket.Add(Key.Index, i);
						recordBucket.SetDataModelValue(referenceTracker, value);
					}
				}
			}

			dataModelArray.Changed += OnChanged;
		}

		private void OnChanged(DataModelArray.ChangedAction action, int index, DataModelValue value)
		{
			switch (action)
			{
				case DataModelArray.ChangedAction.Set:
				{
					var recordBucket = _bucket.Nested(_record ++);
					recordBucket.Add(Key.Operation, Key.Set);
					recordBucket.Add(Key.Index, index);
					_referenceTracker.AddReference(value);
					recordBucket.SetDataModelValue(_referenceTracker, value);
					break;
				}
				case DataModelArray.ChangedAction.Remove:
				{
					_referenceTracker.RemoveReference(value);
					if (_dataModelArray.Length > 1)
					{
						var recordBucket = _bucket.Nested(_record ++);
						recordBucket.Add(Key.Operation, Key.Remove);
						recordBucket.Add(Key.Index, index);
					}
					else
					{
						_record = 0;
						_bucket.RemoveSubtree(Bucket.RootKey);
					}

					break;
				}
				case DataModelArray.ChangedAction.Insert:
				{
					var recordBucket = _bucket.Nested(_record ++);
					recordBucket.Add(Key.Operation, Key.Insert);
					recordBucket.Add(Key.Index, index);
					_referenceTracker.AddReference(value);
					recordBucket.SetDataModelValue(_referenceTracker, value);
					break;
				}
				case DataModelArray.ChangedAction.Clear:
				{
					foreach (var item in _dataModelArray)
					{
						_referenceTracker.RemoveReference(item);
					}

					_record = 0;
					_bucket.RemoveSubtree(Bucket.RootKey);
					break;
				}
				case DataModelArray.ChangedAction.SetLength:
				{
					if (index < _dataModelArray.Length)
					{
						for (var i = index; i < _dataModelArray.Length; i ++)
						{
							_referenceTracker.RemoveReference(_dataModelArray[i]);
						}
					}

					var recordBucket = _bucket.Nested(_record ++);
					recordBucket.Add(Key.Operation, Key.SetLength);
					recordBucket.Add(Key.Index, index);
					break;
				}
				default: throw new ArgumentOutOfRangeException(nameof(action), action, message: null);
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_dataModelArray.Changed -= OnChanged;
			}
		}
	}
}
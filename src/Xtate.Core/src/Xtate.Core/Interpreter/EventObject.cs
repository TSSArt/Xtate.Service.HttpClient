﻿using System;
using System.Collections.Immutable;
using Xtate.Persistence;

namespace Xtate
{
	internal sealed class EventObject : IEvent, IStoreSupport, IAncestorProvider
	{
		public EventObject(EventType type, IOutgoingEvent evt, Uri? origin = default, Uri? originType = default, InvokeId? invokeId = default)
				: this(type, evt.SendId, evt.NameParts, invokeId, origin, originType, evt.Data, ancestor: default) { }

		public EventObject(EventType type, ImmutableArray<IIdentifier> nameParts, DataModelValue data = default, SendId? sendId = default, InvokeId? invokeId = default, object? ancestor = default)
				: this(type, sendId, nameParts, invokeId, origin: null, originType: null, data, ancestor) { }

		private EventObject(EventType type, SendId? sendId, ImmutableArray<IIdentifier> nameParts, InvokeId? invokeId, Uri? origin, Uri? originType, DataModelValue data, object? ancestor)
		{
			Ancestor = ancestor;
			Type = type;
			SendId = sendId;
			NameParts = nameParts;
			InvokeId = invokeId;
			Origin = origin;
			OriginType = originType;
			Data = data.AsConstant();
		}

		public EventObject(in Bucket bucket)
		{
			if (!bucket.TryGet(Key.TypeInfo, out TypeInfo storedTypeInfo) || storedTypeInfo != TypeInfo.EventObject)
			{
				throw new ArgumentException(Resources.Exception_Invalid_TypeInfo_value);
			}

			var name = bucket.GetString(Key.Name);
			NameParts = name != null ? EventName.ToParts(name) : default;
			Type = bucket.Get<EventType>(Key.Type);
			SendId = bucket.GetSendId(Key.SendId);
			Origin = bucket.GetUri(Key.Origin);
			OriginType = bucket.GetUri(Key.OriginType);
			InvokeId = bucket.GetInvokeId(Key.InvokeUniqueId);

			if (bucket.TryGet(Key.Data, out bool data) && data)
			{
				using var tracker = new DataModelReferenceTracker(bucket.Nested(Key.DataReferences));
				Data = bucket.Nested(Key.DataValue).GetDataModelValue(tracker, baseValue: default).AsConstant();
			}
		}

	#region Interface IAncestorProvider

		public object? Ancestor { get; }

	#endregion

	#region Interface IEvent

		public DataModelValue Data { get; }

		public InvokeId? InvokeId { get; }

		public ImmutableArray<IIdentifier> NameParts { get; }

		public Uri? Origin { get; }

		public Uri? OriginType { get; }

		public SendId? SendId { get; }

		public EventType Type { get; }

	#endregion

	#region Interface IStoreSupport

		public void Store(Bucket bucket)
		{
			bucket.Add(Key.TypeInfo, TypeInfo.EventObject);
			bucket.Add(Key.Name, EventName.ToName(NameParts));
			bucket.Add(Key.Type, Type);
			bucket.AddId(Key.SendId, SendId);
			bucket.Add(Key.Origin, Origin);
			bucket.Add(Key.OriginType, OriginType);
			bucket.AddId(Key.InvokeId, InvokeId);

			if (!Data.IsUndefinedOrNull())
			{
				bucket.Add(Key.Data, value: true);
				using var tracker = new DataModelReferenceTracker(bucket.Nested(Key.DataReferences));
				bucket.Nested(Key.DataValue).SetDataModelValue(tracker, Data);
			}
		}

	#endregion
	}
}
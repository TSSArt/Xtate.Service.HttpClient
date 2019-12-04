﻿using System;
using System.Globalization;

namespace TSSArt.StateMachine
{
	public static class IdGenerator
	{
		private static string NewGuidString() => Guid.NewGuid().ToString(format: "D", CultureInfo.InvariantCulture);

		public static string NewSessionId() => NewGuidString();

		public static string NewSendId() => NewGuidString();

		public static string NewInvokeId(string stateId) => stateId + "." + NewGuidString();

		public static string NewUniqueStateId() => NewGuidString();
	}
}
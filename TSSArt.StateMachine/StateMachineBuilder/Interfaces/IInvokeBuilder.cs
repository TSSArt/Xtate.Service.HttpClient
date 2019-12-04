﻿using System;
using System.Collections.Generic;

namespace TSSArt.StateMachine
{
	public interface IInvokeBuilder
	{
		IInvoke Build();

		void SetType(Uri type);
		void SetTypeExpression(IValueExpression typeExpression);
		void SetSource(Uri source);
		void SetSourceExpression(IValueExpression sourceExpression);
		void SetId(string id);
		void SetIdLocation(ILocationExpression idLocation);
		void SetNameList(IReadOnlyList<ILocationExpression> nameList);
		void SetAutoForward(bool autoForward);
		void AddParam(IParam param);
		void SetFinalize(IFinalize finalize);
		void SetContent(IContent content);
	}
}
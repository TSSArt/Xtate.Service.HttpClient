﻿namespace TSSArt.StateMachine
{
	public struct Content : IContent, IEntity<Content, IContent>, IAncestorProvider
	{
		public IValueExpression Expression { get; set; }

		public string Value { get; set; }

		void IEntity<Content, IContent>.Init(IContent source)
		{
			Ancestor = source;
			Expression = source.Expression;
			Value = source.Value;
		}

		bool IEntity<Content, IContent>.RefEquals(in Content other) =>
				ReferenceEquals(Expression, other.Expression) &&
				ReferenceEquals(Value, other.Value);

		internal object Ancestor;

		object IAncestorProvider.Ancestor => Ancestor;
	}
}
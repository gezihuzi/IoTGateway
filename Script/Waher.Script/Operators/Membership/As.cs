﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Waher.Script.Abstraction.Elements;
using Waher.Script.Exceptions;
using Waher.Script.Model;
using Waher.Script.Objects;

namespace Waher.Script.Operators.Membership
{
	/// <summary>
	/// As operator
	/// </summary>
	public class As : BinaryScalarOperator
	{
		/// <summary>
		/// As operator.
		/// </summary>
		/// <param name="Left">Left operand.</param>
		/// <param name="Right">Right operand.</param>
		/// <param name="Start">Start position in script expression.</param>
		/// <param name="Length">Length of expression covered by node.</param>
		/// <param name="Expression">Expression containing script.</param>
		public As(ScriptNode Left, ScriptNode Right, int Start, int Length, Expression Expression)
			: base(Left, Right, Start, Length, Expression)
		{
		}

		/// <summary>
		/// Evaluates the operator.
		/// </summary>
		/// <param name="Left">Left value.</param>
		/// <param name="Right">Right value.</param>
		/// <param name="Variables">Variables collection.</param>
		/// <returns>Result</returns>
		public override IElement Evaluate(IElement Left, IElement Right, Variables Variables)
		{
			if (Right is TypeValue TypeValue)
			{
				Type T = TypeValue.Value;
				object Obj = Left.AssociatedObjectValue;

				if (!(Obj is null) && T.GetTypeInfo().IsAssignableFrom(Obj.GetType().GetTypeInfo()))
					return Left;
				else
					return ObjectValue.Null;
			}
			else
				return base.Evaluate(Left, Right, Variables);
		}

		/// <summary>
		/// Evaluates the operator on scalar operands.
		/// </summary>
		/// <param name="Left">Left value.</param>
		/// <param name="Right">Right value.</param>
		/// <param name="Variables">Variables collection.</param>
		/// <returns>Result</returns>
		public override IElement EvaluateScalar(IElement Left, IElement Right, Variables Variables)
		{
			if (Right is TypeValue TypeValue)
			{
				Type T = TypeValue.Value;
				object Obj = Left.AssociatedObjectValue;

				if (!(Obj is null) && T.GetTypeInfo().IsAssignableFrom(Obj.GetType().GetTypeInfo()))
					return Left;
				else
					return ObjectValue.Null;
			}
			else
				throw new ScriptRuntimeException("Right operand in an AS operation must be a type value.", this);
		}

		/// <summary>
		/// How scalar operands of different types are to be treated. By default, scalar operands are required to be of the same type.
		/// </summary>
		public override UpgradeBehaviour ScalarUpgradeBehaviour
		{
			get { return UpgradeBehaviour.DifferentTypesOk; }
		}
	}
}

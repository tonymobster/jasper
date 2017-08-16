﻿using System.Collections.Generic;
using System.Linq;
using Jasper.Codegen.Compilation;

namespace Jasper.Codegen
{
    public abstract class CompositeFrame : Frame
    {
        private readonly Frame[] _inner;

        protected CompositeFrame(Frame[] inner) : base(inner.Any(x => x.IsAsync))
        {
            _inner = inner;
        }

        public override IEnumerable<Variable> Creates => Enumerable.SelectMany<Frame, Variable>(_inner, x => x.Creates).ToArray();
        public sealed override void GenerateCode(GeneratedMethod method, ISourceWriter writer)
        {
            if (_inner.Length > 1)
            {
                for (int i = 1; i < _inner.Length; i++)
                {
                    _inner[i - 1].Next = _inner[i];
                }
            }

            generateCode(method, writer, _inner[0]);

            Next?.GenerateCode(method, writer);
        }

        protected abstract void generateCode(GeneratedMethod method, ISourceWriter writer, Frame inner);

        protected internal override IEnumerable<Variable> resolveVariables(GeneratedMethod chain)
        {
            return Enumerable.SelectMany<Frame, Variable>(_inner, x => x.resolveVariables(chain)).Distinct();
        }

        public override bool CanReturnTask()
        {
            return Enumerable.Last<Frame>(_inner).CanReturnTask();
        }
    }
}
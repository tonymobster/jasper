﻿using System.Linq;
using Jasper.Bus;
using Jasper.Bus.Model;
using Jasper.Codegen;
using Jasper.Codegen.Compilation;
using Shouldly;
using Xunit;

namespace Jasper.Testing.Bus.Bootstrapping
{
    public class applying_handler_policies : IntegrationContext
    {
        [Fact]
        public void can_apply_a_wrapper_to_all_chains()
        {
            with(_ => _.Policies.Global<WrapWithSimple>());

            ShouldBeBooleanExtensions.ShouldBeTrue(chainFor<MovieAdded>().Middleware.OfType<SimpleWrapper>().Any());
        }
    }

    public class WrapWithSimple : IHandlerPolicy
    {
        public void Apply(HandlerGraph graph)
        {
            foreach (var chain in graph.Chains)
            {
                chain.Middleware.Add(new SimpleWrapper());
            }
        }
    }

    public class SimpleWrapper : Frame
    {
        public SimpleWrapper() : base(false)
        {
        }

        public override void GenerateCode(GeneratedMethod method, ISourceWriter writer)
        {
            writer.Write("// Just a comment that SimpleWrapper was there");

            Next?.GenerateCode(method, writer);
        }
    }
}
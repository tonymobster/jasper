﻿using System.Threading.Tasks;
using Jasper.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TestingSupport;
using TestingSupport.Fakes;
using TestMessages;
using Xunit;

namespace Jasper.Testing.Compilation
{
    public class use_wrappers : CompilationContext
    {
        public use_wrappers()
        {
            theOptions.Handlers.IncludeType<TransactionalHandler>();

            theOptions.Services.AddSingleton(theTracking);
            theOptions.Services.ForSingletonOf<IFakeStore>().Use<FakeStore>();
        }

        private readonly TestingSupport.Fakes.Tracking theTracking = new TestingSupport.Fakes.Tracking();

        [Fact]
        public async Task wrapper_applied_by_generic_attribute_executes()
        {
            var message = new Message2();

            await Execute(message);

            theTracking.DisposedTheSession.ShouldBeTrue();
            theTracking.OpenedSession.ShouldBeTrue();
            theTracking.CalledSaveChanges.ShouldBeTrue();
        }


        [Fact]
        public async Task wrapper_executes()
        {
            var message = new Message1();

            await Execute(message);

            theTracking.DisposedTheSession.ShouldBeTrue();
            theTracking.OpenedSession.ShouldBeTrue();
            theTracking.CalledSaveChanges.ShouldBeTrue();
        }
    }

    [JasperIgnore]
    public class TransactionalHandler
    {
        [FakeTransaction]
        public void Handle(Message1 message)
        {
        }

        [GenericFakeTransaction]
        public void Handle(Message2 message)
        {
        }
    }
}

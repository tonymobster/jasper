using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Alba;
using Jasper.Http;
using Jasper.Http.Model;
using Jasper.Http.Routing;
using Jasper.TestSupport.Alba;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Jasper.MvcExtender.Tests
{
    public class MvcExtendedApp : IDisposable
    {
        public MvcExtendedApp()
        {
            System = SystemUnderTest.For(x => x.UseStartup<Startup>().UseJasper());



            Routes = System.Services.GetRequiredService<RouteGraph>();
        }

        public RouteGraph Routes { get; set; }


        public SystemUnderTest System { get; }

        public void Dispose()
        {
            System?.Dispose();
        }
    }

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseJasper();
            app.UseMvc();
        }
    }

    public class finding_action_methods : IClassFixture<MvcExtendedApp>
    {
        private readonly MvcExtendedApp _app;

        public finding_action_methods(MvcExtendedApp app)
        {
            _app = app;
        }

        [Fact]
        public void will_find_jasper_actions_on_controller_base()
        {
            var chain = _app.Routes.ChainForAction<MyController1>(x => x.get_stuff());
            chain.ShouldNotBeNull();
            chain.Route.HttpMethod.ShouldBe("GET");
            chain.Route.Pattern.ShouldBe("stuff");
        }

        [Fact]
        public void will_find_jasper_actions_on_controller()
        {
            var chain = _app.Routes.ChainForAction<MyController2>(x => x.get_stuff_other());
            chain.ShouldNotBeNull();
            chain.Route.HttpMethod.ShouldBe("GET");
            chain.Route.Pattern.ShouldBe("stuff/other");
        }

        [Fact]
        public void can_find_and_determine_route_from_HttpGet_marked_method_with_no_arguments()
        {
            var chain = _app.Routes.ChainForAction<MyController1>(x => x.Get1());
            chain.ShouldNotBeNull();
            chain.Route.HttpMethod.ShouldBe("GET");
            chain.Route.Pattern.ShouldBe("one");
        }

        [Fact]
        public void can_find_and_determine_route_from_HttpPost_marked_method_with_no_arguments()
        {
            var chain = _app.Routes.ChainForAction<MyController1>(x => x.Post1());

            chain.ShouldNotBeNull();
            chain.Route.HttpMethod.ShouldBe("POST");
            chain.Route.Pattern.ShouldBe("one");
        }

        [Fact]
        public void can_find_and_determine_route_from_HttpGet_marked_method_with_one_argument()
        {
            var chain = _app.Routes.ChainForAction<MyController1>(x => x.GetDog("Shiner"));
            chain.ShouldNotBeNull();
            chain.Route.Pattern.ShouldBe("dog/:name");
            chain.Route.HttpMethod.ShouldBe("GET");
            chain.Route.Segments.ElementAt(1).ShouldBeOfType<RouteArgument>()
                .MappedParameter.Name.ShouldBe("name");
        }

        [Fact]
        public void take_advantage_of_their_route_rules()
        {
            var chain = _app.Routes.ChainForAction<TodoController>(x => x.GetList());
            chain.ShouldNotBeNull();
            chain.Route.HttpMethod.ShouldBe("GET");
            chain.Route.Pattern.ShouldBe("api/todo");
        }

        [Fact]
        public void take_advantage_of_their_route_rules_empty_template()
        {
            var chain = _app.Routes.ChainForAction<TodoController>(x => x.Post());
            chain.ShouldNotBeNull();
            chain.Route.HttpMethod.ShouldBe("POST");
            chain.Route.Pattern.ShouldBe("api/todo");
        }

    }

    public class MyController1 : ControllerBase
    {
        public string get_stuff()
        {
            return "stuff";
        }

        [HttpPost("one")]
        public int Post1()
        {
            return 200;
        }

        [HttpGet("/one")]
        public string Get1()
        {
            return "one";
        }

        [HttpGet("/dog/{name}")]
        public string GetDog(string name)
        {
            return $"the dog is {name}";
        }
    }

    public class MyController2 : Controller
    {
        public string get_stuff_other()
        {
            return "other stuff";
        }
    }

    [Route("api/[controller]")]
    public class TodoController : ControllerBase
    {
        [HttpGet]
        public string GetList()
        {
            return "ok";
        }


        [HttpPost]
        public int Post()
        {
            return 200;
        }
    }
}

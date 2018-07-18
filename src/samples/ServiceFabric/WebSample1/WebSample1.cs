using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using KeyQuery;
using KeyQuery.ServiceFabric;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.ServiceFabric.Data;

namespace WebSample1
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance. 
    /// </summary>
    internal sealed class WebSample1 : StatefulService
    {
        public WebSample1(StatefulServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new ServiceReplicaListener[]
            {
                new ServiceReplicaListener(serviceContext =>
                    new KestrelCommunicationListener(serviceContext, "ServiceEndpoint", (url, listener) =>
                    {
                        ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting Kestrel on {url}");

                        //DataStore<Guid, Customer> store = this.StateManager.AddDocumentStore<Guid, Customer>(new Expression<Func<Customer, string>>[]
                        //{
                        //    model => model.FirstName,
                        //    model => model.Birth.Year.ToString()
                        //}).Result;

                        return new WebHostBuilder()
                                    .UseKestrel()
                                    .ConfigureServices(
                                        services => services
                                            .AddSingleton<StatefulServiceContext>(serviceContext)
                                            .AddSingleton(x => this.StateManager.AddDocumentStore<Guid, Customer>(new Expression<Func<Customer, string>>[]
                                            {
                                                model => model.FirstName,
                                                model => model.Birth.Year.ToString()
                                            }).Result)
                                            //.AddSingleton(store)
                                            .AddSingleton<IReliableStateManager>(this.StateManager))
                                    .UseContentRoot(Directory.GetCurrentDirectory())
                                    .UseStartup<Startup>()
                                    .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.UseReverseProxyIntegration)
                                    .UseUrls(url)
                                    .Build();
                    }))
            };
        }
    }

    //public static class ContainerHelpers
    //{
    //    public static IServiceCollection AddSingletonLazy<TService>(this IServiceCollection services)
    //        where TService : class
    //    {
    //        return services
    //            .AddSingleton<TService>()
    //            .AddSingleton(x => new Lazy<TService>(() => x.GetRequiredService<TService>()));
    //    }

    //    public static IServiceCollection AddSingletonLazy<TService>(this IServiceCollection services, Func<TService> f)
    //        where TService : class
    //    {
    //        return services
    //            .AddSingleton<TService>()
    //            .AddSingleton(x => f);
    //    }
    //}
}

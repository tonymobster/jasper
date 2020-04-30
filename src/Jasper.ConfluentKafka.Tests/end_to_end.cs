using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using AutoFixture.Xunit2;
using Baseline.Dates;
using Confluent.Kafka;
using Jasper.Tracking;
using Jasper.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using TestingSupport;
using TestingSupport.Compliance;
using Xunit;

namespace Jasper.ConfluentKafka.Tests
{
    [Obsolete("try to replace with compliance tests")]
    public class end_to_end
    {
        private static string KafkaServer = "b-1.jj-test.y7lv7k.c5.kafka.us-east-1.amazonaws.com:9094,b-2.jj-test.y7lv7k.c5.kafka.us-east-1.amazonaws.com:9094";
        private static ProducerConfig ProducerConfig = new ProducerConfig
        {
            BootstrapServers = KafkaServer,
            SecurityProtocol = SecurityProtocol.Ssl
        };

        private static ConsumerConfig ConsumerConfig = new ConsumerConfig
        {
            BootstrapServers = KafkaServer,
            SecurityProtocol = SecurityProtocol.Ssl,
            GroupId = nameof(end_to_end),
        };

        public class Sender : JasperOptions
        {

            public Sender()
            {
                Endpoints.ConfigureKafka();

            }

            public string QueueName { get; set; }
        }

        public class Receiver : JasperOptions
        {
            public Receiver(string queueName)
            {
                Endpoints.ConfigureKafka();
            }
        }


        public class KafkaSendingComplianceTests : SendingCompliance
        {
            public KafkaSendingComplianceTests() : base($"kafka://topic/messages".ToUri())
            {
                var sender = new Sender();

                SenderIs(sender);

                var receiver = new Receiver(sender.QueueName);

                ReceiverIs(receiver);
            }
        }





        // SAMPLE: can_stop_and_start_ASB
        [Fact]
        public async Task can_send_and_receive_from_kafka()
        {
            using var host = JasperHost.For<KafkaUsingApp>();
            await host
                // The TrackActivity() method starts a Fluent Interface
                // that gives you fine-grained control over the
                // message tracking
                .TrackActivity()
                .Timeout(30.Seconds())
                // Include the external transports in the determination
                // of "completion"
                .IncludeExternalTransports()
                .SendMessageAndWait(new ColorChosen { Name = "Red" });

            var colors = host.Get<ColorHistory>();

            colors.Name.ShouldBe("Red");
        }


        [Fact]
        public async Task send_multiple_messages_in_order()
        {
            var colorsChosens = Enumerable.Range(0, 100).Select(i => new ColorChosen {Name = i.ToString()});
            var sequence = Guid.NewGuid().ToString();
            using var host = JasperHost.For(host =>
            {
                host.Endpoints.ConfigureKafka();
                host.Endpoints.ListenToKafkaTopic("messages", ConsumerConfig).Sequential();
                host.Endpoints.Publish(pub => pub.Message<ColorChosen>().ToKafkaTopic("messages", ProducerConfig)
                    .CustomizeOutgoing(e => e.Headers.Add("MessageKey", sequence)) // use the same message key in Kafka
                );
                host.Handlers.IncludeType<ColorHandler>();
                host.Services.AddSingleton<ColorHistory>();
                host.Extensions.UseMessageTrackingTestingSupport();
            });

            ITrackedSession session = await host
                .TrackActivity()
                .Timeout(60.Seconds())
                .IncludeExternalTransports()
                .ExecuteAndWait(async ctx =>
                {
                    foreach (ColorChosen colorsChosen in colorsChosens)
                    {
                        await ctx.Publish(colorsChosen);
                    }
                });

            IEnumerable<string> colorsSent = session.AllRecordsInOrder()
                .Where(e => e.EventType == EventType.Sent)
                .Select(e => e.Envelope.Message).Cast<ColorChosen>().Select(c => c.Name);
            IEnumerable<string> colorsPublished = colorsChosens.Select(c => c.Name);

            colorsSent.ShouldBe(colorsPublished);
        }

        // ENDSAMPLE
        public class KafkaUsingApp : JasperOptions
        {
            public KafkaUsingApp()
            {
                Endpoints.ConfigureKafka();
                Endpoints.ListenToKafkaTopic("messages", ConsumerConfig);
                Endpoints.Publish(pub => pub.Message<ColorChosen>().ToKafkaTopic("messages", ProducerConfig));

                Handlers.IncludeType<ColorHandler>();

                Services.AddSingleton<ColorHistory>();

                Extensions.UseMessageTrackingTestingSupport();
            }

            public override void Configure(IHostEnvironment hosting, IConfiguration config)
            {
                //Endpoints.ConfigureAzureServiceBus(config.GetValue<string>("AzureServiceBusConnectionString"));
            }
        }
    }
}
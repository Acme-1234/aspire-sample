using System.Net.Sockets;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting
{
    public class RedpandaOptions
    {
        public string Image { get; set; } = "redpandadata/redpanda:v25.1.5";
        public int KafkaPort { get; set; } = 9092;
        public int KafkaExternalPort { get; set; } = 19092;
        public string DataPath { get; set; } = "./data/redpanda";
        public string ContainerDataPath { get; set; } = "/var/lib/redpanda";
        public Dictionary<string, string> Environment { get; set; } = new()
        {
            ["REDPANDA_AUTO_CREATE_TOPICS"] = "true"
        };
        public List<string> Args { get; set; } = new()
        {
            "start",
            "--overprovisioned",
            "--smp", "1",
            "--memory", "1G",
            "--reserve-memory", "0M",
            "--node-id", "0",
            "--check=false",
            "--kafka-addr", "PLAINTEXT://0.0.0.0:9092,OUTSIDE://0.0.0.0:19092",
            "--advertise-kafka-addr", "PLAINTEXT://redpanda:9092,OUTSIDE://localhost:19092",
            "--set", "redpanda.enable_transactions=true",
            "--set", "redpanda.enable_idempotence=true"
        };
        public List<(string HostPath, string ContainerPath)> BindMounts { get; set; } = new();
        public List<(int HostPort, int ContainerPort, string[] Schemes)> Endpoints { get; set; } = new();
        public ContainerLifetime Lifetime { get; set; } = ContainerLifetime.Persistent;
    }

    public static class RedpandaResourceExtensions
    {
        /// <summary>
        /// Adds a configurable Redpanda container to the distributed application.
        /// </summary>
        public static IResourceBuilder<ContainerResource> AddRedpanda(
            this IDistributedApplicationBuilder builder,
            string name,
            Action<RedpandaOptions>? configure = null)
        {
            var options = new RedpandaOptions();
            configure?.Invoke(options);

            var containerBuilder = builder.AddContainer(name, options.Image);

            foreach (var env in options.Environment)
                containerBuilder = containerBuilder.WithEnvironment(env.Key, env.Value);

            if (options.Args.Count > 0)
                containerBuilder = containerBuilder.WithArgs(options.Args.ToArray());

            // Default bind mount
            containerBuilder = containerBuilder.WithBindMount(options.DataPath, options.ContainerDataPath);
            // Additional bind mounts
            foreach (var (host, container) in options.BindMounts)
                containerBuilder = containerBuilder.WithBindMount(host, container);

            // Default endpoints
            containerBuilder = containerBuilder.WithEndpoint(options.KafkaPort, options.KafkaPort, "http","kafka");
            containerBuilder = containerBuilder.WithEndpoint(options.KafkaExternalPort, options.KafkaExternalPort, "http", "broker");
            // Additional endpoints
            foreach (var (hostPort, containerPort, schemes) in options.Endpoints)
            {
                foreach (var scheme in schemes)
                {
                    containerBuilder = containerBuilder.WithEndpoint(hostPort, containerPort, scheme);
                }
            }

            containerBuilder = containerBuilder.WithLifetime(options.Lifetime);

            return containerBuilder;
        }

        /// <summary>
        /// Adds a Redpanda Console container configured to connect to the specified Redpanda resource.
        /// </summary>
        public static IResourceBuilder<ContainerResource> AddRedpandaConsole(
            this IDistributedApplicationBuilder builder,
            string name,
            IResourceBuilder<ContainerResource> redpandaResource,
            Action<IResourceBuilder<ContainerResource>>? configure = null)
        {
            var consoleBuilder = builder.AddContainer(name, "docker.redpanda.com/redpandadata/console:latest")
                .WithEnvironment("KAFKA_BROKERS", $"{redpandaResource.Resource.Name}:9092")
                .WithEndpoint(8080, 8080, "http")
                .WaitFor(redpandaResource);

            configure?.Invoke(consoleBuilder);
            return consoleBuilder;
        }

        /// <summary>
        /// Chains a Redpanda Console container to the Redpanda resource.
        /// </summary>
        public static IResourceBuilder<ContainerResource> WithRedpandaConsole(
            this IResourceBuilder<ContainerResource> redpandaResource,
            string name = "redpanda-console",
            Action<IResourceBuilder<ContainerResource>>? configure = null)
        {
            var builder = redpandaResource.ApplicationBuilder;
            return builder.AddRedpandaConsole(name, redpandaResource, configure);
        }
    }
}

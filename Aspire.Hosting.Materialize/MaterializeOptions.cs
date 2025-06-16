using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting
{
    public class MaterializeOptions
    {
        public string Image { get; set; } = "materialize/materialized";
        public int PostgresPort { get; set; } = 6875;
        public int HttpPort { get; set; } = 6874;
        public string DataPath { get; set; } = "./data/materialize";
        public string ContainerDataPath { get; set; } = "/mzdata";
        public Dictionary<string, string> Environment { get; set; } = new();
        public List<string> Args { get; set; } = new();
        public List<(string HostPath, string ContainerPath)> BindMounts { get; set; } = new();
        public List<(int HostPort, int ContainerPort, string[] Schemes)> Endpoints { get; set; } = new();
        public ContainerLifetime Lifetime { get; set; } = ContainerLifetime.Persistent;
        // Integrations (e.g., Redpanda, PostgreSQL, Debezium, etc.)
        public List<IResourceBuilder<ContainerResource>> Integrations { get; set; } = new();
    }

    public static class MaterializeResourceExtensions
    {
        public static IResourceBuilder<ContainerResource> AddMaterialize(
            this IDistributedApplicationBuilder builder,
            string name,
            Action<MaterializeOptions>? configure = null)
        {
            var options = new MaterializeOptions();
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
            containerBuilder = containerBuilder.WithEndpoint(options.PostgresPort, options.PostgresPort, "postgres");
            containerBuilder = containerBuilder.WithEndpoint(options.HttpPort, options.HttpPort, "http");
            // Additional endpoints
            foreach (var (hostPort, containerPort, schemes) in options.Endpoints)
            {
                foreach (var scheme in schemes)
                {
                    containerBuilder = containerBuilder.WithEndpoint(hostPort, containerPort, scheme);
                }
            }

            containerBuilder = containerBuilder.WithLifetime(options.Lifetime);

            // Wait for integrations
            foreach (var integration in options.Integrations)
                containerBuilder = containerBuilder.WaitFor(integration);

            return containerBuilder;
        }
    }
}

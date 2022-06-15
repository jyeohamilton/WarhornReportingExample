using CommandLine;

namespace WarhornReporting
{
    class Program
    {
        public class CommandLineOptions
        {
            [Option('l', "eventSlug", Required = true, HelpText = "Event slug for the Warhorn event.")]
            public string? EventSlug { get; set; }

            [Option('s', "start", Required = false, HelpText = "Start datetime of reporting window.")]
            public DateTime? Start { get; set; }

            [Option('e', "end", Required = false, HelpText = "End datetime of reporting window.")]
            public DateTime? End { get; set; }

            [Option('o', "outputFile", Required = false, Default = "output.txt", HelpText = "Path to output file.")]
            public string? OutputFile { get; set; }

            [Option('c', "clientId", Required = true, HelpText = "Client ID assigned to app.")]
            public string? ClientId { get; set; }
        }

        static async Task<int> Main(string[] args)
        {
            return await Parser.Default.ParseArguments<CommandLineOptions>(args)
                .MapResult(
                    options => Run(options),
                    _ => Task.FromResult(1));
        }

        static async Task<int> Run(CommandLineOptions options)
        {
            var client = await OidcHelper.GetHttpClientAsync(options.ClientId!);
            var queryResponse = await GraphQuery.GetGraphInfo(client, options.EventSlug!, options.Start ?? DateTime.MinValue, options.End ?? DateTime.MaxValue);
            StatWriter.WriteStats(queryResponse, options.OutputFile!);

            return 0;
        }
    }
}
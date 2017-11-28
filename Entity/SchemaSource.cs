namespace Entity
{
    using System;
    using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
    using Microsoft.EntityFrameworkCore.Scaffolding;
    using System.Text;
    using Microsoft.Extensions.DependencyInjection;
    public class SchemaSource
    {
        static InMemoryFileService InMemoryFiles;
        public static string Get(string connectionString, string assmNamespace) 
        {
            // setup
            var services = new ServiceCollection()
                .AddLogging()
                .AddSingleton<ReverseEngineeringGenerator>()
                .AddSingleton<ScaffoldingUtilities>()
                .AddSingleton<CSharpUtilities>()
                .AddSingleton<ConfigurationFactory>()
                .AddSingleton<DbContextWriter>()
                .AddSingleton<EntityTypeWriter>()
                .AddSingleton<CodeWriter, StringBuilderCodeWriter>()
                .AddSingleton<CandidateNamingService, EntityNamingService>() 
                .AddSingleton(typeof(IFileService), sp => {
                    return InMemoryFiles = new InMemoryFileService();
                }); 
            new SqliteDesignTimeServices().ConfigureDesignTimeServices(services);
            var serviceProvider = services.BuildServiceProvider();
            var generator = serviceProvider.GetRequiredService<ReverseEngineeringGenerator>();
            var scaffoldingModelFactory = serviceProvider.GetRequiredService<IScaffoldingModelFactory>();
            
            var programName = "Ctx";
            var outputPath = Environment.GetEnvironmentVariable("TEMP");
            if (string.IsNullOrEmpty(outputPath)) 
            {
                outputPath = "/tmp";
            }
            var conf = new ReverseEngineeringConfiguration 
            {
                ConnectionString = connectionString,
                ContextClassName = programName,
                ProjectPath = "na",
                ProjectRootNamespace = assmNamespace,
                OutputPath = outputPath
            };
            var taskResult = generator.GenerateAsync(conf);
            taskResult.Wait();
            var resFiles = taskResult.Result;

            var output = new StringBuilder();
            output.Append(InMemoryFiles.RetrieveFileContents(outputPath, programName + ".cs"));
            foreach(var fpath in resFiles.EntityTypeFiles)
            {
                output.Append(InMemoryFiles.RetrieveFileContents(outputPath, System.IO.Path.GetFileName(fpath)).SkipLines(4));
            }
            var usings = @"using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
";
            
            return usings + output.ToString();
        }
    }
}
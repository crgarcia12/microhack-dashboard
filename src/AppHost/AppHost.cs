var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Api>("api")
    .WithEnvironment("ConnectionStrings__hackboxdb", "Data Source=hackbox.db");

var web = builder.AddJavaScriptApp("web", "../web")
    .WithNpm()
    .WithReference(api)
    .WaitFor(api)
    .WithEnvironment("API_URL", api.GetEndpoint("http"))
    .WithHttpEndpoint(env: "PORT");

builder.Build().Run();

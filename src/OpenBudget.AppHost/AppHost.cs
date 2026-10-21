var builder = DistributedApplication.CreateBuilder(args);

var postgresUser = builder.AddParameter("postgres-username", "budgetboard");
var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var postgres = builder.AddPostgres("postgres", postgresUser, postgresPassword, port: 5432)
    .WithImageTag("17-alpine")
    // Reuse the named volume created by the existing local database.
    .WithDataVolume("ynab-clone_budget-board-data");
var database = postgres.AddDatabase("budgetdatabase", "budgetboard");

var api = builder.AddProject<Projects.OpenBudget_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpEndpoint(port: 5080, name: "http");

builder.AddViteApp("ui", "../OpenBudget.UI")
    .WithHttpEndpoint(port: 5173, env: "PORT")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();

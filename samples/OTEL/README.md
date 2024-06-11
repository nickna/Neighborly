The IndexAPI project demonstrates how the Neighborly library can be integrated with OpenTelemetry. This project uses .NET Aspire,
but any OpenTelemetry compatible collector can be used.

## How to run

Ensure that your system matches the [.NET Aspire system requirements](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling#container-runtime). 

1. Start the Aspire App Host

   ```bash
   dotnet run --launch-profile http --project samples/OTEL/IndexAPI.AppHost/IndexAPI.AppHost.csproj 
   ```

1. Open the .NET Aspire dashboard on http://localhost:5034. You may have to use the login link from the AppHost's startup log.

1. Perform some requests from http://localhost:5135/swagger/index.html

1. Look at the different parts of the dashboard to see the traces, metrics, and logs that the APIs have produced.

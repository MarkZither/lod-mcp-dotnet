using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var transport = Environment.GetEnvironmentVariable("BOOKSTACK_MCP_TRANSPORT") ?? "stdio";
var adminPort = int.TryParse(
    Environment.GetEnvironmentVariable("BOOKSTACK_ADMIN_PORT"), out var ap) ? ap : 5174;

if (transport is not ("stdio" or "http" or "both"))
{
    Console.Error.WriteLine(
        $"Invalid BOOKSTACK_MCP_TRANSPORT value: '{transport}'. Valid values: stdio, http, both.");
    return 1;
}

if (transport == "stdio" && adminPort == 0)
{
    // Headless / CI path — no admin sidecar, no HTTP listener needed.
    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.AddConsole(options =>
        options.LogToStandardErrorThreshold = LogLevel.Trace);

    builder.Configuration.AddInMemoryCollection(MapBookStackEnvVars());
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly(Assembly.GetExecutingAssembly())
        .WithResourcesFromAssembly(Assembly.GetExecutingAssembly());

    var host = builder.Build();
    host.Services.GetRequiredService<ILogger<Program>>()
        .LogInformation("Admin sidecar is disabled (BOOKSTACK_ADMIN_PORT=0).");
    await host.RunAsync().ConfigureAwait(false);
}
else
{
    var mcpPort = int.TryParse(
        Environment.GetEnvironmentVariable("BOOKSTACK_MCP_HTTP_PORT"), out var p) ? p : 3000;

    var builder = WebApplication.CreateBuilder(args);

    if (transport is "stdio" or "both")
    {
        builder.Logging.AddConsole(options =>
            options.LogToStandardErrorThreshold = LogLevel.Trace);
    }

    builder.Configuration.AddInMemoryCollection(MapBookStackEnvVars());

    var mcpBuilder = builder.Services
        .AddMcpServer()
        .WithToolsFromAssembly(Assembly.GetExecutingAssembly())
        .WithResourcesFromAssembly(Assembly.GetExecutingAssembly());

    if (transport == "stdio")
    {
        // stdio + admin enabled: stdio MCP transport alongside the admin Kestrel listener.
        mcpBuilder.WithStdioServerTransport();
    }
    else
    {
        mcpBuilder.WithHttpTransport();
        if (transport == "both")
        {
            mcpBuilder.WithStdioServerTransport();
        }
    }

    // Explicit Kestrel listeners so the admin port can be added alongside the MCP port.
    // Once Listen() is called explicitly, ASPNETCORE_URLS is no longer honoured by Kestrel,
    // so we read and apply it manually.
    builder.WebHost.ConfigureKestrel(opts =>
    {
        if (transport != "stdio")
        {
            var aspnetUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
            if (string.IsNullOrEmpty(aspnetUrls))
            {
                opts.ListenAnyIP(mcpPort);
            }
            else
            {
                foreach (var urlString in aspnetUrls.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!Uri.TryCreate(urlString.Trim(), UriKind.Absolute, out var listenUri))
                    {
                        continue;
                    }

                    var address = listenUri.Host is "*" or "+" or "0.0.0.0"
                        ? IPAddress.Any
                        : IPAddress.Parse(listenUri.Host);
                    opts.Listen(address, listenUri.Port);
                }
            }
        }
    });

    var app = builder.Build();

    if (transport != "stdio")
    {
        var authToken = app.Configuration["BOOKSTACK_MCP_HTTP_AUTH_TOKEN"]
                        ?? Environment.GetEnvironmentVariable("BOOKSTACK_MCP_HTTP_AUTH_TOKEN");

        if (string.IsNullOrEmpty(authToken))
        {
            app.Logger.LogWarning(
                "HTTP authentication is disabled. Set BOOKSTACK_MCP_HTTP_AUTH_TOKEN to enable.");

            app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
            app.MapMcp();
        }
        else
        {
            var authTokenBytes = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(authToken));

            app.Use(async (ctx, next) =>
            {
                if (ctx.Request.Path.StartsWithSegments("/mcp"))
                {
                    var header = ctx.Request.Headers.Authorization.ToString();
                    if (!IsAuthorized(header, authTokenBytes))
                    {
                        ctx.Response.StatusCode = 401;
                        return;
                    }
                }

                await next(ctx).ConfigureAwait(false);
            });

            app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
            app.MapMcp();
        }
    }

    await app.RunAsync().ConfigureAwait(false);
}

return 0;

static bool IsAuthorized(string authorizationHeader, ReadOnlyMemory<byte> expected)
{
    const string bearerPrefix = "Bearer ";
    if (!authorizationHeader.StartsWith(bearerPrefix, StringComparison.Ordinal))
    {
        return false;
    }

    var provided = Encoding.UTF8.GetBytes(authorizationHeader[bearerPrefix.Length..]);
    return provided.Length == expected.Length
        && CryptographicOperations.FixedTimeEquals(expected.Span, provided);
}

static Dictionary<string, string?> MapBookStackEnvVars()
{
    var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    var baseUrl = Environment.GetEnvironmentVariable("BOOKSTACK_BASE_URL");
    if (baseUrl is not null)
    {
        map["BookStack:BaseUrl"] = baseUrl;
    }

    var tokenSecret = Environment.GetEnvironmentVariable("BOOKSTACK_TOKEN_SECRET");
    if (tokenSecret is not null)
    {
        var colonIndex = tokenSecret.IndexOf(':');
        if (colonIndex > 0)
        {
            map["BookStack:TokenId"] = tokenSecret[..colonIndex];
            map["BookStack:TokenSecret"] = tokenSecret[(colonIndex + 1)..];
        }
    }

    AddScopeEntries(map, "BOOKSTACK_SCOPED_BOOKS", "BookStack:ScopedBooks");
    AddScopeEntries(map, "BOOKSTACK_SCOPED_SHELVES", "BookStack:ScopedShelves");

    return map;
}

static void AddScopeEntries(Dictionary<string, string?> map, string envVar, string configPrefix)
{
    var raw = Environment.GetEnvironmentVariable(envVar);
    if (raw is null)
    {
        return;
    }

    var index = 0;
    foreach (var entry in raw.Split(',').Select(e => e.Trim()).Where(e => e.Length > 0))
    {
        if (!_scopeEntryRegex.IsMatch(entry))
        {
            continue;
        }

        map[$"{configPrefix}:{index++}"] = entry;
    }
}

public partial class Program
{
    private static readonly Regex _scopeEntryRegex = new(@"^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);
}

using System.Diagnostics;
using System.Text.Json;

namespace lod_mcp_dotnet.tests;

public class BasicTests
{
    [Before(Class)]
    public static Task BeforeClass(ClassHookContext context)
    {
        // Runs once before all tests in this class
        return Task.CompletedTask;
    }

    [After(Class)]
    public static Task AfterClass(ClassHookContext context)
    {
        // Runs once after all tests in this class
        return Task.CompletedTask;
    }

    [Before(Test)]
    public Task BeforeTest(TestContext context)
    {
        // Runs before each test in this class
        return Task.CompletedTask;
    }

    [After(Test)]
    public Task AfterTest(TestContext context)
    {
        // Runs after each test in this class
        return Task.CompletedTask;
    }

    [Test]
    public async Task Add_ReturnsSum()
    {
        var calculator = new Calculator();

        var result = calculator.Add(1, 2);

        await Assert.That(result).IsEqualTo(3);
    }

    [Test]
    public async Task Divide_ByZero_ThrowsException()
    {
        var calculator = new Calculator();

        var action = () => calculator.Divide(1, 0);

        await Assert.That(action).ThrowsException()
            .WithMessage("Attempted to divide by zero.");
    }

        [Test]
    public async Task GetRandomNumber_ReturnsValueWithinRange()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project ../../src/lod-mcp-dotnet",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        await Assert.That(process).IsNotNull();

        // Read handshake
        var handshakeLine = await process.StandardOutput.ReadLineAsync();
        await Assert.That(handshakeLine).IsNotNullOrWhiteSpace();

                // Prepare JSON-RPC request
        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/getRandomNumber",
            @params = new { min = 10, max = 20 }
        };

        var json = JsonSerializer.Serialize(request);
        await process.StandardInput.WriteLineAsync(json);

        // Read response
        var responseLine = await process.StandardOutput.ReadLineAsync();
        await Assert.That(responseLine).IsNotNullOrWhiteSpace();
        var nonNullResponse = responseLine!;

        using var doc = JsonDocument.Parse(nonNullResponse);
        var root = doc.RootElement;

        await Assert.That(root.GetProperty("jsonrpc").GetString()).IsEqualTo("2.0");
        await Assert.That(root.GetProperty("id").GetInt32()).IsEqualTo(1);

        var result = root.GetProperty("result").GetInt32();

        await Assert.That(result).IsGreaterThanOrEqualTo(10);
        await Assert.That(result).IsLessThan(20);

        process.Kill();
    }
}

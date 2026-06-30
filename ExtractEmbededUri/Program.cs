using Microsoft.Playwright;

namespace ExtractEmbededUri
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

			var app = builder.Build();

			// Endpoint: /extract?url=<embed-url>
			app.MapGet("/extract", async (string url) =>
			{
				try
				{
					if (string.IsNullOrWhiteSpace(url))
						return Results.BadRequest("URL is required");

					using var playwright = await Playwright.CreateAsync();

					await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
					{
						Headless = true,
						Args = new[]
						{
				"--no-sandbox",
				"--disable-setuid-sandbox",
				"--disable-dev-shm-usage"
			}
					});

					var page = await browser.NewPageAsync();

					string? streamUrl = null;

					// Capture network requests
					page.Request += (_, request) =>
					{
						if (request.Url.Contains(".m3u8") || request.Url.Contains(".mp4"))
						{
							streamUrl = request.Url;
							Console.WriteLine($"Found stream: {request.Url}");
						}
					};

					// Go to page with timeout
					await page.GotoAsync(url, new PageGotoOptions
					{
						WaitUntil = WaitUntilState.NetworkIdle,
						Timeout = 45000   // 45 seconds max
					});

					// Wait a bit for dynamic content (reduce this if possible)
					await page.WaitForTimeoutAsync(8000);

					await browser.CloseAsync();

					if (!string.IsNullOrEmpty(streamUrl))
						return Results.Ok(new { success = true, streamUrl });

					return Results.Ok(new { success = false, message = "No stream URL found" });
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Playwright Error: {ex.Message}");
					Console.WriteLine(ex.StackTrace);

					return Results.Problem(
						detail: ex.Message,
						statusCode: 500,
						title: "Playwright Extraction Failed"
					);
				}
			});
			app.Run();
        }
    }
}

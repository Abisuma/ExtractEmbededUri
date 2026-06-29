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
				using var playwright = await Playwright.CreateAsync();
				await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
				{
					Headless = true
				});
				var page = await browser.NewPageAsync();

				string? streamUrl = null;

				page.Request += (_, request) =>
				{
					if (request.Url.EndsWith(".m3u8") || request.Url.EndsWith(".mp4"))
					{
						streamUrl = request.Url;
					}
				};

				await page.GotoAsync(url);
				await page.WaitForTimeoutAsync(50000); // wait for JS to run

				return streamUrl ?? "No stream found";
			});

			app.Run();
        }
    }
}

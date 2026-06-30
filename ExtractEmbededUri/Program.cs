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
						Args = new[] { "--no-sandbox", "--disable-dev-shm-usage" }
					});

					var context = await browser.NewContextAsync();
					var page = await context.NewPageAsync();

					string? streamUrl = null;

					// Listen for .m3u8 requests after the play button clicks
					page.Response += async (_, response) =>
					{
						if (response.Url.EndsWith(".m3u8"))
						{
							streamUrl = response.Url;
							Console.WriteLine($"Captured stream: {streamUrl}");
						}
					};

					await page.GotoAsync(url, new PageGotoOptions
					{
						WaitUntil = WaitUntilState.NetworkIdle,
						Timeout = 45000
					});


					// Click play button (JW Player)
					await page.WaitForSelectorAsync("button.jw-icon-display, .jw-icon-display", new()
					{
						Timeout = 25000,
						State = WaitForSelectorState.Visible
					});

					await page.ClickAsync("button.jw-icon-display, .jw-icon-display");

					// Handle ad popup
					try
					{
						var popupTask = page.WaitForPopupAsync(new() { Timeout = 10000 });
						var popup = await popupTask;
						Console.WriteLine("Ad popup detected - closing");
						await popup.CloseAsync();
					}
					catch
					{
						Console.WriteLine("No ad popup or timeout");
					}

					// Second click to start stream
					await page.ClickAsync("button.jw-icon-display, .jw-icon-display");

					// Wait for stream
					await page.WaitForTimeoutAsync(10000);

					await browser.CloseAsync();

					if (!string.IsNullOrEmpty(streamUrl))
					{
						// Return both URL and headers needed by VLC
						return Results.Ok(new
						{
							success = true,
							streamUrl,
							headers = new Dictionary<string, string>
						{
							{ "referer", "https://embed.st/" },
							{ "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
											"AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36" },
							{ "sec-ch-ua", "\"Not/A)Brand\";v=\"99\", \"Chromium\";v=\"148\"" },
							{ "sec-ch-ua-mobile", "?0" },
							{ "sec-ch-ua-platform", "\"Windows\"" }
						}
						});
					}

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

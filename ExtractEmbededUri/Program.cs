using Microsoft.Playwright;
using System.ComponentModel;

namespace ExtractEmbededUri
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);
			var app = builder.Build();
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
						Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage" }
					});

					var page = await browser.NewPageAsync();
					string? streamUrl = null;

					// Capture m3u8
					page.Response += (_, response) =>
					{
						if (response.Url.Contains(".m3u8"))
						{
							streamUrl = response.Url;
							Console.WriteLine($" Captured stream: {response.Url}");
						}
					};

					await page.GotoAsync(url, new PageGotoOptions
					{
						WaitUntil = WaitUntilState.NetworkIdle,
						Timeout = 40000
					});

					Console.WriteLine("Page loaded. Looking for play button...");

					// First click - triggers ad popup
					await page.WaitForSelectorAsync(".jw-icon-display, button.jw-icon-playback, .jw-display-icon-container", new()
					{
						Timeout = 25000,
						State = WaitForSelectorState.Visible
					});

					await page.ClickAsync(".jw-icon-display, button.jw-icon-playback");

					// Handle ad popup
					try
					{
						var popupTask = page.WaitForPopupAsync(new() { Timeout = 12000 });
						var popup = await popupTask;
						Console.WriteLine("Ad popup detected - closing it");
						await popup.CloseAsync();
					}
					catch
					{
						Console.WriteLine("No ad popup or timeout");
					}

					// Second click - starts the stream
					Console.WriteLine("Clicking second time to start stream...");
					await page.ClickAsync(".jw-icon-display, button.jw-icon-playback");

					// Wait for stream to load
					await page.WaitForTimeoutAsync(15000);

					await browser.CloseAsync();

					if (!string.IsNullOrEmpty(streamUrl))
					{
						return Results.Ok(new
						{
							success = true,
							streamUrl,
							headers = new Dictionary<string, string>
							{
								["referer"] = "https://embed.st/",
								["user-agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
							}
						});
					}

					return Results.Ok(new { success = false, message = "No stream URL found" });
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Playwright Error: {ex.Message}");
					Console.WriteLine(ex.StackTrace);
					return Results.Problem(ex.Message, statusCode: 500);
				}
			});
			// Endpoint: /extract?url=<embed-url>
			//app.MapGet("/extract", async (string url) =>
			//{
			//	try
			//	{
			//		using var playwright = await Playwright.CreateAsync();

			//		await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
			//		{
			//			Headless = true,
			//			Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage" }
			//		});

			//		var page = await browser.NewPageAsync();
			//		string? streamUrl = null;

			//		page.Request += (_, request) =>
			//		{
			//			if (request.Url.Contains(".m3u8"))
			//			{
			//				streamUrl = request.Url;
			//				Console.WriteLine($" Captured: {request.Url}");
			//			}
			//		};

			//		await page.GotoAsync(url, new PageGotoOptions
			//		{
			//			WaitUntil = WaitUntilState.NetworkIdle,
			//			Timeout = 45000
			//		});

			//		// Long wait like your console app (but not 5 minutes)
			//		await page.WaitForTimeoutAsync(25000);

			//		await browser.CloseAsync();

			//		if (!string.IsNullOrEmpty(streamUrl))
			//		{
			//			return Results.Ok(new
			//			{
			//				success = true,
			//				streamUrl,
			//				headers = new Dictionary<string, string>
			//				{
			//					["referer"] = "https://embed.st/",
			//					["user-agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
			//				}
			//			});
			//		}

			//		return Results.Ok(new { success = false, message = "No m3u8 found" });
			//	}
			//	catch (Exception ex)
			//	{
			//		Console.WriteLine($"Playwright Error: {ex.Message}");
			//		Console.WriteLine(ex.StackTrace);
			//		return Results.Problem(ex.Message, statusCode: 500);
			//	}
			//});

			app.Run();
		}
	}

}

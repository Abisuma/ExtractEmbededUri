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
					//string? streamUrl = null;

					//page.Response += (_, response) =>
					//{
					//	if (response.Url.Contains(".m3u8"))
					//	{
					//		streamUrl = response.Url;
					//		Console.WriteLine($"Captured: {response.Url}");
					//	}
					//};

					//await page.GotoAsync(url, new PageGotoOptions
					//{
					//	WaitUntil = WaitUntilState.NetworkIdle,
					//	Timeout = 45000
					//});

					//Console.WriteLine("Page loaded - waiting for stream...");

					//// Just wait longer like your console app
					//await page.WaitForTimeoutAsync(30000);

					//await browser.CloseAsync();

					//if (!string.IsNullOrEmpty(streamUrl))
					//{
					//	return Results.Ok(new
					//	{
					//		success = true,
					//		streamUrl,
					//		headers = new Dictionary<string, string>
					//		{
					//			["referer"] = "https://embed.st/",
					//			["user-agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
					//		}
					//	});
					//}

					// Assumes you already created 'context' and 'page'
					string? streamUrl = null;

					// capture m3u8 requests/responses
					page.Request += (_, req) => {
						if (req.Url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase)) streamUrl ??= req.Url;
					};
					page.Response += (_, resp) => {
						if (resp.Url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase)) streamUrl ??= resp.Url;
					};

					// console logs (players sometimes log the URL)
					page.Console += (_, msg) => {
						var t = msg.Text;
						if (t.Contains(".m3u8", StringComparison.OrdinalIgnoreCase)) streamUrl ??= t;
					};

					// 1) goto
					await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 45000 });

					// 2) wait for iframe and get its frame
					var iframeHandle = await page.WaitForSelectorAsync("iframe[src*='/embed/']", new PageWaitForSelectorOptions { Timeout = 15000 });
					IFrame? playerFrame = iframeHandle != null ? await iframeHandle.ContentFrameAsync() : null;

					// helper to click inside frame or page
					async Task SafeClickAsync(IFrame? frame, string selector)
					{
						try
						{
							if (frame != null)
								await frame.ClickAsync(selector, new FrameClickOptions { Timeout = 5000 });
							else
								await page.ClickAsync(selector, new PageClickOptions { Timeout = 5000 });
						}
						catch { /* ignore */ }
					}

					// 3) first click (may open ad popup)
					await SafeClickAsync(playerFrame, ".jw-icon-display, .jw-icon-play");

					// 4) detect and close any popup (small delay to let it open)
					await Task.Delay(500);
					var popup = page.Context.Pages.FirstOrDefault(p => p != page);
					if (popup != null) { try { await popup.CloseAsync(); } catch { } }

					// 5) second click to initialize real player
					await SafeClickAsync(playerFrame, ".jw-icon-display, .jw-icon-play");

					// 6) Try JW Player API inside the frame (preferred)
					if (playerFrame != null)
					{
						try
						{
							// returns file URL or object; adjust if playlist structure differs
							var file = await playerFrame.EvaluateAsync<string>("() => (window.jwplayer && window.jwplayer().getPlaylist && window.jwplayer().getPlaylist()[0]?.file) || null");
							if (!string.IsNullOrEmpty(file)) streamUrl ??= file;
						}
						catch { /* ignore eval errors */ }
					}

					// 7) Wait a bit for network capture if JW API didn't yield
					var waited = 0;
					while (string.IsNullOrEmpty(streamUrl) && waited < 40000)
					{
						await page.WaitForTimeoutAsync(500);
						waited += 500;
					}

					// streamUrl now likely contains an m3u8 (or null)

					return Results.Ok(new { success = false, message = "No stream found" });
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

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
					// 1) Attach network / console handlers BEFORE navigating (you already do)
					// 2) Navigate (use NetworkIdle but still allow longer timeout)
					await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000 });

					// 3) Wait for iframe to be attached, not visible
					var iframeHandle = await page.WaitForSelectorAsync("iframe[src*='/embed/']", new PageWaitForSelectorOptions { Timeout = 15000, State = WaitForSelectorState.Attached });

					// 4a) Option A: use FrameLocator (recommended)
					var frameLocator = page.FrameLocator("iframe[src*='/embed/']");
					await frameLocator.Locator(".jw-icon-display, .jw-icon-play").First.ClickAsync(new LocatorClickOptions { Timeout = 5000 });

					// 4b) Option B: fallback to ContentFrame
					IFrame? playerFrame = iframeHandle != null ? await iframeHandle.ContentFrameAsync() : null;

					// 5) Try JW Player API inside the frame (if frame available)
					if (playerFrame != null)
					{
						try
						{
							var file = await playerFrame.EvaluateAsync<string>("() => (window.jwplayer && window.jwplayer().getPlaylist && window.jwplayer().getPlaylist()[0]?.file) || null");
							if (!string.IsNullOrEmpty(file)) streamUrl ??= file;
						}
						catch { /* ignore eval errors */ }
					}

					// 6) Wait for network-captured m3u8 (with short loop)
					var waited = 0;
					while (string.IsNullOrEmpty(streamUrl) && waited < 40000)
					{
						await page.WaitForTimeoutAsync(500);
						waited += 500;
					}

					// 7) Return result
					if (!string.IsNullOrEmpty(streamUrl))
						return Results.Ok(new { success = true, stream = streamUrl });

					return Results.Ok(new { success = false, message = "No stream found" });
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
			

			app.Run();
		}
	}

}

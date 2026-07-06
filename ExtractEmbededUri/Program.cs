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
				if (string.IsNullOrWhiteSpace(url))
					return Results.BadRequest("URL is required");

				try
				{
					using var playwright = await Playwright.CreateAsync();
					await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
					{
						Headless = true,
						Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage" }
					});

					var page = await browser.NewPageAsync();
					string? streamUrl = null;

					// Attach handlers BEFORE navigation
					page.Request += (_, req) =>
					{
						if (req.Url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
							streamUrl ??= req.Url;
					};
					page.Response += (_, resp) =>
					{
						if (resp.Url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
							streamUrl ??= resp.Url;
					};
					page.Console += (_, msg) =>
					{
						var t = msg.Text;
						if (t.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
							streamUrl ??= t;
					};

					// Navigate (give extra time on slow hosts)
					await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000 });

					// Debug: list iframe srcs so you can inspect what's present
					var iframeSrcs = await page.EvaluateAsync<string[]>("() => Array.from(document.querySelectorAll('iframe')).map(f => f.src)");
					Console.WriteLine("iframes:\n" + string.Join("\n", iframeSrcs ?? Array.Empty<string>()));

					// Wait for iframe attachment (not visible)
					var iframeHandle = await page.WaitForSelectorAsync("iframe[src*='/embed/']",
						new PageWaitForSelectorOptions { Timeout = 30000, State = WaitForSelectorState.Attached });

					// Prefer FrameLocator for clicks/waits inside iframe
					try
					{
						var fl = page.FrameLocator("iframe[src*='/embed/']");
						await fl.Locator(".jw-icon-display, .jw-icon-play").First.ClickAsync(new LocatorClickOptions { Timeout = 8000 });
					}
					catch (Exception fcEx)
					{
						Console.WriteLine("FrameLocator click failed: " + fcEx.Message);
						// fallback: try clicking via frame handle if available
						if (iframeHandle != null)
						{
							var frame = await iframeHandle.ContentFrameAsync();
							if (frame != null)
							{
								try { await frame.ClickAsync(".jw-icon-display, .jw-icon-play", new FrameClickOptions { Timeout = 5000 }); }
								catch { /* ignore */ }
							}
						}
					}

					// If same-origin, try JW player API inside the frame
					if (iframeHandle != null)
					{
						var playerFrame = await iframeHandle.ContentFrameAsync();
						if (playerFrame != null)
						{
							try
							{
								var file = await playerFrame.EvaluateAsync<string>(
									"() => (window.jwplayer && window.jwplayer().getPlaylist && window.jwplayer().getPlaylist()[0]?.file) || null");
								if (!string.IsNullOrEmpty(file)) streamUrl ??= file;
							}
							catch (Exception evalEx)
							{
								Console.WriteLine("Eval inside frame failed (possibly cross-origin): " + evalEx.Message);
							}
						}
					}

					// Wait briefly for network-captured m3u8 (adjust timeout as needed)
					var waited = 0;
					while (string.IsNullOrEmpty(streamUrl) && waited < 40000)
					{
						await page.WaitForTimeoutAsync(500);
						waited += 500;
					}

					if (!string.IsNullOrEmpty(streamUrl))
						return Results.Ok(new { success = true, stream = streamUrl });

					return Results.Ok(new { success = false, message = "No stream found" });
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Playwright Error: {ex.Message}\n{ex.StackTrace}");
					return Results.Problem(ex.Message, statusCode: 500);
				}
			});



			app.Run();
		}
	}

}

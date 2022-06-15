using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

using IdentityModel.OidcClient.Browser;

namespace WarhornReporting
{
    /// <summary>
    /// Use the default browser for Warhorn login.
    /// Slightly modified from code in <cref="https://github.com/IdentityModel/IdentityModel.OidcClient.Samples" />
    /// </summary>
    public class SystemBrowser : IBrowser
    {
        public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken)
        {
            using var listener = new LoopbackHttpListener();
            OpenBrowser(options.StartUrl);

            try
            {
                var result = await listener.WaitForCallbackAsync(DefaultTimeout);
                if (string.IsNullOrWhiteSpace(result))
                {
                    return new BrowserResult
                    {
                        ResultType = BrowserResultType.UnknownError,
                        Error = "Empty response."
                    };
                }

                return new BrowserResult
                {
                    Response = result,
                    ResultType = BrowserResultType.Success
                };
            }
            catch (TaskCanceledException ex)
            {
                return new BrowserResult
                {
                    ResultType = BrowserResultType.Timeout,
                    Error = ex.Message
                };
            }
            catch (Exception ex)
            {
                return new BrowserResult
                {
                    ResultType = BrowserResultType.UnknownError,
                    Error = ex.Message
                };
            }
        }

        public static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // see https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Use a Kestrel instance to handle the redirect.
    /// Slightly modified from code in <cref="https://github.com/IdentityModel/IdentityModel.OidcClient.Samples" />
    /// </summary>
    public class LoopbackHttpListener : IDisposable
    {
        public LoopbackHttpListener()
        {
            _host = new WebHostBuilder()
                // HTTPS is required for the redirect URI.
                // The following configuration assumes the  default dev cert
                // and is not suitable for a production environment.
                .UseKestrel().ConfigureKestrel(
                    opts => opts.ListenLocalhost(
                        Port,
                        lo => lo.UseHttps()))
                .UseUrls(RedirectUri)
                .Configure(Configure)
                .Build();
            _host.Start();
        }
#pragma warning disable CA1822 // Mark members as static
        public Task<string> WaitForCallbackAsync(TimeSpan timeout)
#pragma warning restore CA1822 // Mark members as static
        {
            Task.Run(async () =>
            {
                await Task.Delay((int)timeout.TotalMilliseconds);
                _source.TrySetCanceled();
            });

            return _source.Task;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Task.Run(async () =>
            {
                await Task.Delay(500);
                _host.Dispose();
            });
        }

        private async static Task SetResultAsync(string value, HttpContext ctx)
        {
            _source.TrySetResult(value);

            try
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/html";
                await ctx.Response.WriteAsync("<h1>You can now return to the application.</h1>");
                await ctx.Response.Body.FlushAsync();
            }
            catch
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = "text/html";
                await ctx.Response.WriteAsync("<h1>Invalid request.</h1>");
                await ctx.Response.Body.FlushAsync();
            }
        }
        private void Configure(IApplicationBuilder app)
        {
            app.UseHttpsRedirection();
            app.Run(async ctx =>
            {
                if (ctx.Request.Method == "GET")
                {
                    await SetResultAsync(ctx.Request.QueryString.Value!, ctx);
                }
                else
                {
                    ctx.Response.StatusCode = 405;
                }
            });
        }

        public static readonly string RedirectUri = $"https://{IPAddress.Loopback}:{Port}/";

        private static readonly TaskCompletionSource<string> _source = new();

        private const int Port = 5001;

        private readonly IWebHost _host;
    }
}
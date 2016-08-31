﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Microsoft.AspNetCore.Rewrite.Tests.ModRewrite
{
    public class ModRewriteMiddlewareTest
    {
        [Fact]
        public async Task Invoke_RewritePathWhenMatching()
        {   
            var options = new RewriteOptions().AddApacheModRewrite(new StringReader("RewriteRule /hey/(.*) /$1 "));
            var builder = new WebHostBuilder()
                .Configure(app =>
                {
                    app.UseRewriter(options);
                    app.Run(context => context.Response.WriteAsync(context.Request.Path));
                });
            var server = new TestServer(builder);

            var response = await server.CreateClient().GetStringAsync("/hey/hello");

            Assert.Equal(response, "/hello");
        }

        [Fact]
        public async Task Invoke_RewritePathTerminatesOnFirstSuccessOfRule()
        {
            var options = new RewriteOptions().AddApacheModRewrite(new StringReader("RewriteRule /hey/(.*) /$1 [L]"))
                            .AddApacheModRewrite(new StringReader("RewriteRule /hello /what"));
            var builder = new WebHostBuilder()
                 .Configure(app =>
                 {
                     app.UseRewriter(options);
                     app.Run(context => context.Response.WriteAsync(context.Request.Path));
                 });
            var server = new TestServer(builder);

            var response = await server.CreateClient().GetStringAsync("/hey/hello");

            Assert.Equal(response, "/hello");
        }

        [Fact]
        public async Task Invoke_RewritePathDoesNotTerminateOnFirstSuccessOfRule()
        {
            var options = new RewriteOptions().AddApacheModRewrite(new StringReader("RewriteRule /hey/(.*) /$1"))
                                       .AddApacheModRewrite(new StringReader("RewriteRule /hello /what"));
            var builder = new WebHostBuilder()
                 .Configure(app =>
                 {
                     app.UseRewriter(options);
                     app.Run(context => context.Response.WriteAsync(context.Request.Path));
                 });
            var server = new TestServer(builder);

            var response = await server.CreateClient().GetStringAsync("/hey/hello");

            Assert.Equal(response, "/what");
        }

        [Fact]
        public async Task Invoke_ShouldIgnoreComments()
        {
            var options = new RewriteOptions().AddApacheModRewrite(new StringReader("#RewriteRule ^/hey/(.*) /$1 "));
            var builder = new WebHostBuilder()
                 .Configure(app =>
                 {
                     app.UseRewriter(options);
                     app.Run(context => context.Response.WriteAsync(context.Request.Path));
                 });
            var server = new TestServer(builder);

            var response = await server.CreateClient().GetStringAsync("/hey/hello");

            Assert.Equal(response, "/hey/hello");
        }
        
        [Fact]
        public async Task Invoke_ShouldRewriteHomepage()
        {
            var options = new RewriteOptions().AddApacheModRewrite(new StringReader(@"RewriteRule ^/$ /homepage.html"));
            var builder = new WebHostBuilder()
                 .Configure(app =>
                 {
                     app.UseRewriter(options);
                     app.Run(context => context.Response.WriteAsync(context.Request.Path));
                 });
            var server = new TestServer(builder);

            var response = await server.CreateClient().GetStringAsync("http://www.foo.org/");

            Assert.Equal(response, "/homepage.html");
        }

        [Fact]
        public async Task Invoke_ShouldIgnorePorts()
        {
            var options = new RewriteOptions().AddApacheModRewrite(new StringReader(@"RewriteRule ^/$ /homepage.html"));
            var builder = new WebHostBuilder()
              .Configure(app =>
              {
                  app.UseRewriter(options);
                  app.Run(context => context.Response.WriteAsync(context.Request.Path));
              });
            var server = new TestServer(builder);

            var response = await server.CreateClient().GetStringAsync("http://www.foo.org:42/");

            Assert.Equal(response, "/homepage.html");
        }

        [Fact]
        public async Task Invoke_HandleNegatedRewriteRules()
        {
            var options = new RewriteOptions().AddApacheModRewrite(new StringReader(@"RewriteRule !^/$ /homepage.html"));
            var builder = new WebHostBuilder()
              .Configure(app =>
              {
                  app.UseRewriter(options);
                  app.Run(context => context.Response.WriteAsync(context.Request.Path));
              });
            var server = new TestServer(builder);

            var response = await server.CreateClient().GetStringAsync("http://www.foo.org/");

            Assert.Equal(response, "/");
        }

        [Fact]
        public async Task Invoke_BackReferencesShouldBeApplied()
        {
            var options = new RewriteOptions().AddApacheModRewrite(new StringReader(@"RewriteRule (.*)\.aspx $1.php"));
            var builder = new WebHostBuilder()
             .Configure(app =>
                 {
                     app.UseRewriter(options);
                     app.Run(context => context.Response.WriteAsync(context.Request.Path));
                 });
            var server = new TestServer(builder);

            var response = await server.CreateClient().GetStringAsync("http://www.foo.org/homepage.aspx");

            Assert.Equal(response, "/homepage.php");
        }

        [Theory]
        [InlineData("http://www.foo.org/homepage.aspx", @"RewriteRule (.*)\.aspx $1.php", "/homepage.php")]
        [InlineData("http://www.foo.org/homepage.ASPX", @"RewriteRule (.*)\.aspx $1.php", "/homepage.ASPX")]
        [InlineData("http://www.foo.org/homepage.aspx", @"RewriteRule (.*)\.aspx $1.php [NC]", "/homepage.php")]
        [InlineData("http://www.foo.org/homepage.ASPX", @"RewriteRule (.*)\.aspx $1.php [NC]", "/homepage.php")]
        [InlineData("http://www.foo.org/homepage.aspx", @"RewriteRule (.*)\.aspx $1.php [nocase]", "/homepage.php")]
        [InlineData("http://www.foo.org/homepage.ASPX", @"RewriteRule (.*)\.aspx $1.php [nocase]", "/homepage.php")]
        public async Task Invoke_ShouldHandleFlagNoCase(string url, string rule, string expected)
        {
            var options = new RewriteOptions().AddApacheModRewrite(new StringReader(rule));
            var builder = new WebHostBuilder()
             .Configure(app =>
             {
                 app.UseRewriter(options);
                 app.Run(context => context.Response.WriteAsync(context.Request.Path));
             });
            var server = new TestServer(builder);

            var response = await server.CreateClient().GetStringAsync(url);

            Assert.Equal(response, expected);
        }

        [Fact]
        public async Task Invoke_CheckFullUrlWithOnlyPath()
        {
            var options = new RewriteOptions()
                .AddApacheModRewrite(new StringReader(@"RewriteRule (.+) http://www.example.com$1/"));
            var builder = new WebHostBuilder()
              .Configure(app =>
              {
                  app.UseRewriter(options);
                  app.Run(context => context.Response.WriteAsync(context.Request.Path));
              });
            var server = new TestServer(builder);

            var response = await server.CreateClient().GetStringAsync("http://www.foo.org/blog/2016-jun");

            Assert.Equal(response, @"/blog/2016-jun/");
        }

        [Fact]
        public async Task Invoke_CheckFullUrlWithUFlag()
        {
            var options = new RewriteOptions()
                .AddApacheModRewrite(new StringReader(@"RewriteRule (.+) http://www.example.com$1/"));
            var builder = new WebHostBuilder()
              .Configure(app =>
              {
                  app.UseRewriter(options);
                  app.Run(context => context.Response.WriteAsync(context.Request.Scheme + "://" + context.Request.Host.Host + context.Request.Path + context.Request.QueryString));
              });
            var server = new TestServer(builder);

            var response = await server.CreateClient().GetStringAsync("http://www.foo.org/blog/2016-jun");

            Assert.Equal(response, @"http://www.example.com/blog/2016-jun/");
        }

        [Fact]
        public async Task Invoke_CheckModFileConditions()
        {
            var options = new RewriteOptions()
                .AddApacheModRewrite(new StringReader(@"RewriteRule (.+) http://www.example.com$1/"));
            var builder = new WebHostBuilder()
              .Configure(app =>
              {
                  app.UseRewriter(options);
                  app.Run(context => context.Response.WriteAsync(context.Request.Scheme + "://" + context.Request.Host.Host + context.Request.Path + context.Request.QueryString));
              });
            var server = new TestServer(builder);

            var response = await server.CreateClient().GetStringAsync("http://www.foo.org/blog/2016-jun");

            Assert.Equal(response, @"http://www.example.com/blog/2016-jun/");
        }

        [Theory]
        [InlineData("http://www.example.com/foo/")]
        public async Task Invoke_EnsureHttps(string input)
        {
            var options = new RewriteOptions()
                .AddApacheModRewrite(new StringReader("RewriteCond %{REQUEST_URI} /foo/  \nRewriteCond %{HTTPS} !on   \nRewriteRule ^(.*)$ https://www.example.com$1 [R=301,L]"));
            var builder = new WebHostBuilder()
              .Configure(app =>
              {
                  app.UseRewriter(options);
                  app.Run(context => context.Response.WriteAsync(context.Request.Scheme + "://" + context.Request.Host.Host + context.Request.Path + context.Request.QueryString));
              });
            var server = new TestServer(builder);

            var response = await server.CreateClient().GetAsync(input);

            Assert.Equal(response.StatusCode, (HttpStatusCode)301);
            Assert.Equal(response.Headers.Location.AbsoluteUri, @"https://www.example.com/foo/");
        }

        [Theory]
        [InlineData("http://www.example.com/")]
        public async Task Invoke_CaptureEmptyStringInRegexAssertRedirectLocationHasForwardSlash(string input)
        {
            var options = new RewriteOptions()
                .AddApacheModRewrite(new StringReader("RewriteRule ^(.*)$ $1 [R=301,L]"));
            var builder = new WebHostBuilder()
              .Configure(app =>
              {
                  app.UseRewriter(options);
                  app.Run(context => context.Response.WriteAsync(context.Request.Scheme + "://" + context.Request.Host.Host + context.Request.Path + context.Request.QueryString));
              });
            var server = new TestServer(builder);

            var response = await server.CreateClient().GetAsync(input);

            Assert.Equal(response.StatusCode, (HttpStatusCode)301);
            Assert.Equal(response.Headers.Location.OriginalString, "/");
        }

        [Theory]
        [InlineData("http://www.example.com/")]
        public async Task Invoke_CaptureEmptyStringInRegexAssertRewriteHasForwardSlash(string input)
        {
            var options = new RewriteOptions()
                .AddApacheModRewrite(new StringReader("RewriteRule ^(.*)$ $1 [L]"));
            var builder = new WebHostBuilder()
              .Configure(app =>
              {
                  app.UseRewriter(options);
                  app.Run(context => context.Response.WriteAsync(context.Request.Path + context.Request.QueryString));
              });
            var server = new TestServer(builder);

            var response = await server.CreateClient().GetStringAsync(input);
            Assert.Equal(response, "/");
        }
    }
}
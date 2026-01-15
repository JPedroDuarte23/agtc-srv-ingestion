using AgtcSrvIngestion.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Xunit;

namespace AgtcSrvIngestion.Test
{
    public class CorrelationIdMiddlewareTests
    {
        [Fact]
        public async Task Invoke_ShouldUseExistingCorrelationId_WhenHeaderIsPresent()
        {
            // Arrange
            var correlationId = Guid.NewGuid().ToString();
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-Correlation-ID"] = correlationId;

            var nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new CorrelationIdMiddleware(next);

            // Act
            await middleware.Invoke(httpContext);

            // Assert
            Assert.True(nextCalled);
            Assert.Equal(correlationId, httpContext.Items["X-Correlation-ID"]);
            Assert.Equal(correlationId, httpContext.Response.Headers["X-Correlation-ID"]);
        }

        [Fact]
        public async Task Invoke_ShouldGenerateNewGuid_WhenHeaderIsNotPresent()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();

            var nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new CorrelationIdMiddleware(next);

            // Act
            await middleware.Invoke(httpContext);

            // Assert
            Assert.True(nextCalled);
            Assert.NotNull(httpContext.Items["X-Correlation-ID"]);
            Assert.True(Guid.TryParse(httpContext.Items["X-Correlation-ID"].ToString(), out _));
            Assert.Equal(httpContext.Items["X-Correlation-ID"].ToString(), httpContext.Response.Headers["X-Correlation-ID"].ToString());
        }
    }
}

using AgtcSrvIngestion.Application.Exceptions;
using AgtcSrvIngestion.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace AgtcSrvIngestion.Test
{
    public class ExceptionHandlerTests
    {
        private readonly Mock<ILogger<ExceptionHandler>> _loggerMock;
        private readonly Mock<RequestDelegate> _nextMock;
        private readonly ExceptionHandler _exceptionHandler;
        private readonly DefaultHttpContext _httpContext;

        public ExceptionHandlerTests()
        {
            _loggerMock = new Mock<ILogger<ExceptionHandler>>();
            _nextMock = new Mock<RequestDelegate>();
            _exceptionHandler = new ExceptionHandler(_nextMock.Object, _loggerMock.Object);
            _httpContext = new DefaultHttpContext();
            _httpContext.Response.Body = new MemoryStream();
        }

        [Fact]
        public async Task InvokeAsync_ShouldCallNext_WhenNoException()
        {
            _nextMock.Setup(next => next(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);
            await _exceptionHandler.InvokeAsync(_httpContext);
            _nextMock.Verify(next => next(_httpContext), Times.Once);
        }


        [Fact]
        public async Task InvokeAsync_ShouldHandleHttpException()
        {
            var httpException = new UnexpectedException(new Exception());
            _nextMock.Setup(next => next(It.IsAny<HttpContext>())).ThrowsAsync(httpException);

            await _exceptionHandler.InvokeAsync(_httpContext);

            _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(_httpContext.Response.Body);
            var responseBody = await reader.ReadToEndAsync();
            var response = JsonDocument.Parse(responseBody);

            Assert.Equal((int)HttpStatusCode.InternalServerError, _httpContext.Response.StatusCode);
            Assert.Equal("application/json", _httpContext.Response.ContentType);
            Assert.Equal(httpException.Error, response.RootElement.GetProperty("error").GetString());
            Assert.Equal(httpException.Message, response.RootElement.GetProperty("message").GetString());
        }

        [Fact]
        public async Task InvokeAsync_ShouldHandleGenericException()
        {
            var exception = new Exception("Something went wrong");
            _nextMock.Setup(next => next(It.IsAny<HttpContext>())).ThrowsAsync(exception);

            await _exceptionHandler.InvokeAsync(_httpContext);

            _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(_httpContext.Response.Body);
            var responseBody = await reader.ReadToEndAsync();
            var response = JsonDocument.Parse(responseBody);

            Assert.Equal((int)HttpStatusCode.InternalServerError, _httpContext.Response.StatusCode);
            Assert.Equal("application/json", _httpContext.Response.ContentType);
            Assert.Equal("Internal Server Error", response.RootElement.GetProperty("error").GetString());
            Assert.Equal(exception.Message, response.RootElement.GetProperty("message").GetString());
        }
    }
}

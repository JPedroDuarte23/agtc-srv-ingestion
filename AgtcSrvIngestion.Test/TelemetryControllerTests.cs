using AgtcSrvIngestion.API.Controllers;
using AgtcSrvIngestion.Application.Dtos;
using AgtcSrvIngestion.Application.Exceptions;
using AgtcSrvIngestion.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;

namespace AgtcSrvIngestion.Test;

public class TelemetryControllerTests
{
    private readonly Mock<ILogger<TelemetryController>> _mockLogger;
    private readonly Mock<ITelemetryService> _mockTelemetryService;
    private readonly TelemetryController _controller;

    public TelemetryControllerTests()
    {
        _mockLogger = new Mock<ILogger<TelemetryController>>();
        _mockTelemetryService = new Mock<ITelemetryService>();
        
        _controller = new TelemetryController(_mockLogger.Object, _mockTelemetryService.Object);
        
        // Setup the controller with mocked service via reflection since there's no public constructor
        var serviceField = typeof(TelemetryController).GetField("_service", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        serviceField?.SetValue(_controller, _mockTelemetryService.Object);

        var loggerField = typeof(TelemetryController).GetField("_logger",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        loggerField?.SetValue(_controller, _mockLogger.Object);
    }

    #region Valid Request Tests

    [Fact]
    public async Task PostTelemetry_WithValidRequest_ShouldReturnAccepted()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        SetupControllerUser(deviceId);
        
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 25.5,
            Timestamp: DateTime.UtcNow
        );

        _mockTelemetryService
            .Setup(x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PostTelemetry(request);

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task PostTelemetry_WithValidRequest_ShouldCallServiceWithCorrectDeviceId()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        SetupControllerUser(deviceId);
        var fieldName = "fieldName";
        var farmerName = "farmerName";
        var propertyName = "propertyName";

        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Humidity",
            Value: 65.0,
            Timestamp: DateTime.UtcNow
        );

        _mockTelemetryService
            .Setup(x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.PostTelemetry(request);

        // Assert
        _mockTelemetryService.Verify(
            x => x.ProcessTelemetryAsync(deviceId, farmerName, fieldName, propertyName, request),
            Times.Once);
    }

    [Fact]
    public async Task PostTelemetry_WithValidRequest_ShouldCallServiceExactlyOnce()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        SetupControllerUser(deviceId);
        var fieldName = "fieldName";
        var farmerName = "farmerName";
        var propertyName = "propertyName";

        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 25.0,
            Timestamp: DateTime.UtcNow
        );

        _mockTelemetryService
            .Setup(x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.PostTelemetry(request);

        // Assert
        _mockTelemetryService.Verify(
            x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()),
            Times.Once);
    }

    [Fact]
    public async Task PostTelemetry_WithValidRequest_ShouldPassRequestToService()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        SetupControllerUser(deviceId);
        
        var fieldId = Guid.NewGuid();
        var sensorType = "Soil Moisture";
        var value = 75.5;
        var timestamp = DateTime.UtcNow;

        var request = new TelemetryRequest(
            FieldId: fieldId,
            SensorType: sensorType,
            Value: value,
            Timestamp: timestamp
        );

        TelemetryRequest capturedRequest = null;
        _mockTelemetryService
            .Setup(x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()))
            .Callback<Guid, TelemetryRequest>((id, req) => capturedRequest = req)
            .Returns(Task.CompletedTask);

        // Act
        await _controller.PostTelemetry(request);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(fieldId, capturedRequest.FieldId);
        Assert.Equal(sensorType, capturedRequest.SensorType);
        Assert.Equal(value, capturedRequest.Value);
        Assert.Equal(timestamp, capturedRequest.Timestamp);
    }

    [Fact]
    public async Task PostTelemetry_WithValidRequest_ShouldReturnStatusCodeAccepted()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        SetupControllerUser(deviceId);
        
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 20.0,
            Timestamp: DateTime.UtcNow
        );

        _mockTelemetryService
            .Setup(x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PostTelemetry(request) as AcceptedResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(202, result.StatusCode);
    }

    #endregion

    #region Multiple Requests Tests

    [Fact]
    public async Task PostTelemetry_WithMultipleRequests_ShouldProcessEachRequest()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        SetupControllerUser(deviceId);

        var requests = new[]
        {
            new TelemetryRequest(Guid.NewGuid(), "Temperature", 25.0, DateTime.UtcNow),
            new TelemetryRequest(Guid.NewGuid(), "Humidity", 65.0, DateTime.UtcNow),
            new TelemetryRequest(Guid.NewGuid(), "pH", 6.8, DateTime.UtcNow)
        };

        _mockTelemetryService
            .Setup(x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        foreach (var request in requests)
        {
            await _controller.PostTelemetry(request);
        }

        // Assert
        _mockTelemetryService.Verify(
            x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task PostTelemetry_WithDifferentDevices_ShouldProcessWithCorrectDeviceIds()
    {
        // Arrange
        var deviceId1 = Guid.NewGuid();
        var deviceId2 = Guid.NewGuid();

        var fieldName = "fieldName";
        var farmerName = "farmerName";
        var propertyName = "propertyName";

        var request1 = new TelemetryRequest(Guid.NewGuid(), "Temperature", 25.0, DateTime.UtcNow);
        var request2 = new TelemetryRequest(Guid.NewGuid(), "Temperature", 30.0, DateTime.UtcNow);

        _mockTelemetryService
            .Setup(x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()))
            .Returns(Task.CompletedTask);

        // Act - First request with device 1
        SetupControllerUser(deviceId1);
        await _controller.PostTelemetry(request1);

        // Act - Second request with device 2
        SetupControllerUser(deviceId2);
        await _controller.PostTelemetry(request2);

        // Assert
        _mockTelemetryService.Verify(
            x => x.ProcessTelemetryAsync(deviceId1, farmerName, fieldName, propertyName, request1),
            Times.Once);
        _mockTelemetryService.Verify(
            x => x.ProcessTelemetryAsync(deviceId2, farmerName, fieldName, propertyName, request2),
            Times.Once);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public async Task PostTelemetry_WithNegativeValue_ShouldProcessRequest()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        SetupControllerUser(deviceId);
        var fieldName = "fieldName";
        var farmerName = "farmerName";
        var propertyName = "propertyName";

        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: -50.0,
            Timestamp: DateTime.UtcNow
        );

        _mockTelemetryService
            .Setup(x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PostTelemetry(request);

        // Assert
        Assert.IsType<AcceptedResult>(result);
        _mockTelemetryService.Verify(
            x => x.ProcessTelemetryAsync(deviceId, farmerName, fieldName, propertyName, request),
            Times.Once);
    }

    [Fact]
    public async Task PostTelemetry_WithZeroValue_ShouldProcessRequest()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        SetupControllerUser(deviceId);

        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Humidity",
            Value: 0.0,
            Timestamp: DateTime.UtcNow
        );

        _mockTelemetryService
            .Setup(x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PostTelemetry(request);

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task PostTelemetry_WithLargeValue_ShouldProcessRequest()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        SetupControllerUser(deviceId);
        
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 199.99,
            Timestamp: DateTime.UtcNow
        );

        _mockTelemetryService
            .Setup(x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PostTelemetry(request);

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task PostTelemetry_WithCurrentTimestamp_ShouldProcessRequest()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        SetupControllerUser(deviceId);
        var currentTime = DateTime.UtcNow;
        var fieldName = "fieldName";
        var farmerName = "farmerName";
        var propertyName = "propertyName";

        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 25.0,
            Timestamp: currentTime
        );

        _mockTelemetryService
            .Setup(x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.PostTelemetry(request);

        // Assert
        _mockTelemetryService.Verify(
            x => x.ProcessTelemetryAsync(deviceId, farmerName, fieldName, propertyName, request),
            Times.Once);
    }

    [Fact]
    public async Task PostTelemetry_WithFutureTimestamp_ShouldProcessRequest()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        SetupControllerUser(deviceId);
        var futureTime = DateTime.UtcNow.AddHours(1);

        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 25.0,
            Timestamp: futureTime
        );

        _mockTelemetryService
            .Setup(x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PostTelemetry(request);

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task PostTelemetry_WithPastTimestamp_ShouldProcessRequest()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        SetupControllerUser(deviceId);
        var pastTime = DateTime.UtcNow.AddDays(-1);

        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 25.0,
            Timestamp: pastTime
        );

        _mockTelemetryService
            .Setup(x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PostTelemetry(request);

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }

    #endregion

    #region Service Exception Handling Tests

    [Fact]
    public async Task PostTelemetry_WhenServiceThrowsBadRequestException_ShouldPropagatException()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        SetupControllerUser(deviceId);
        
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 300.0, // Invalid value
            Timestamp: DateTime.UtcNow
        );

        var exception = new BadRequestException("Valor fora dos limites operacionais");
        _mockTelemetryService
            .Setup(x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()))
            .ThrowsAsync(exception);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(
            () => _controller.PostTelemetry(request));
    }

    [Fact]
    public async Task PostTelemetry_WhenServiceThrowsUnexpectedException_ShouldPropagatException()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        SetupControllerUser(deviceId);
        
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 25.0,
            Timestamp: DateTime.UtcNow
        );

        var exception = new UnexpectedException(new HttpRequestException("SNS unavailable"));
        _mockTelemetryService
            .Setup(x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()))
            .ThrowsAsync(exception);

        // Act & Assert
        await Assert.ThrowsAsync<UnexpectedException>(
            () => _controller.PostTelemetry(request));
    }

    [Fact]
    public async Task PostTelemetry_WhenServiceThrowsException_ShouldNotReturnAccepted()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        SetupControllerUser(deviceId);
        
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 25.0,
            Timestamp: DateTime.UtcNow
        );

        _mockTelemetryService
            .Setup(x => x.ProcessTelemetryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelemetryRequest>()))
            .ThrowsAsync(new Exception("Service error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(
            () => _controller.PostTelemetry(request));
    }

    #endregion

    #region Helper Methods

    private void SetupControllerUser(Guid deviceId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, deviceId.ToString())
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    #endregion
}

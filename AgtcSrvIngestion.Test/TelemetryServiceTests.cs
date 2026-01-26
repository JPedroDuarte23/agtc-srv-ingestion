using AgtcSrvIngestion.Application.Dtos;
using AgtcSrvIngestion.Application.Exceptions;
using AgtcSrvIngestion.Application.Interfaces;
using AgtcSrvIngestion.Application.Services;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Text.Json;

namespace AgtcSrvIngestion.Test;

public class TelemetryServiceTests
{
    private readonly Mock<IAmazonSimpleNotificationService> _mockSnsClient;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly ITelemetryService _service;

    public TelemetryServiceTests()
    {
        _mockSnsClient = new Mock<IAmazonSimpleNotificationService>();
        _mockConfiguration = new Mock<IConfiguration>();
        
        // Mock para retornar o Topic ARN
        _mockConfiguration
            .Setup(x => x["SnsTopics:TelemetryTopicArn"])
            .Returns("arn:aws:sns:us-east-1:123456789:test-topic");

        _service = new TelemetryService(_mockSnsClient.Object, _mockConfiguration.Object);
    }

    #region Valid Telemetry Tests

    [Fact]
    public async Task ProcessTelemetryAsync_WithValidData_ShouldPublishToSns()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 25.5,
            Timestamp: DateTime.UtcNow
        );

        _mockSnsClient
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishResponse { MessageId = "test-message-id" });

        // Act
        await _service.ProcessTelemetryAsync(deviceId, request);

        // Assert
        _mockSnsClient.Verify(
            x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessTelemetryAsync_WithValidData_ShouldIncludeCorrectMessageAttributes()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var sensorType = "Humidity";
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: sensorType,
            Value: 65.0,
            Timestamp: DateTime.UtcNow
        );

        PublishRequest capturedRequest = null;
        _mockSnsClient
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new PublishResponse { MessageId = "test-message-id" });

        // Act
        await _service.ProcessTelemetryAsync(deviceId, request);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(sensorType, capturedRequest.MessageAttributes["SensorType"].StringValue);
        Assert.Equal("String", capturedRequest.MessageAttributes["SensorType"].DataType);
    }

    [Fact]
    public async Task ProcessTelemetryAsync_WithValidData_ShouldIncludeDeviceIdInMessage()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var request = new TelemetryRequest(
            FieldId: fieldId,
            SensorType: "Temperature",
            Value: 20.0,
            Timestamp: DateTime.UtcNow
        );

        PublishRequest capturedRequest = null;
        _mockSnsClient
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new PublishResponse { MessageId = "test-message-id" });

        // Act
        await _service.ProcessTelemetryAsync(deviceId, request);

        // Assert
        Assert.NotNull(capturedRequest);
        var messageBody = JsonSerializer.Deserialize<JsonElement>(capturedRequest.Message);
        Assert.Equal(deviceId.ToString(), messageBody.GetProperty("SensorDeviceId").GetGuid().ToString());
    }

    [Fact]
    public async Task ProcessTelemetryAsync_WithBoundaryValueMinus100_ShouldPublishToSns()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: -100,
            Timestamp: DateTime.UtcNow
        );

        _mockSnsClient
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishResponse { MessageId = "test-message-id" });

        // Act & Assert - Should not throw
        await _service.ProcessTelemetryAsync(deviceId, request);
        _mockSnsClient.Verify(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessTelemetryAsync_WithBoundaryValue200_ShouldPublishToSns()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 200,
            Timestamp: DateTime.UtcNow
        );

        _mockSnsClient
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishResponse { MessageId = "test-message-id" });

        // Act & Assert - Should not throw
        await _service.ProcessTelemetryAsync(deviceId, request);
        _mockSnsClient.Verify(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessTelemetryAsync_WithValidData_ShouldIncludeProcessingId()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 25.0,
            Timestamp: DateTime.UtcNow
        );

        PublishRequest capturedRequest = null;
        _mockSnsClient
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new PublishResponse { MessageId = "test-message-id" });

        // Act
        await _service.ProcessTelemetryAsync(deviceId, request);

        // Assert
        Assert.NotNull(capturedRequest);
        var messageBody = JsonSerializer.Deserialize<JsonElement>(capturedRequest.Message);
        var processingId = messageBody.GetProperty("ProcessingId").GetGuid();
        Assert.NotEqual(Guid.Empty, processingId);
    }

    #endregion

    #region Invalid Value Tests

    [Fact]
    public async Task ProcessTelemetryAsync_WithValueBelowMinus100_ShouldThrowBadRequestException()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: -100.1,
            Timestamp: DateTime.UtcNow
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _service.ProcessTelemetryAsync(deviceId, request));
        
        Assert.Contains("Valor fora dos limites operacionais", exception.Message);
    }

    [Fact]
    public async Task ProcessTelemetryAsync_WithValueAbove200_ShouldThrowBadRequestException()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 200.1,
            Timestamp: DateTime.UtcNow
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _service.ProcessTelemetryAsync(deviceId, request));
        
        Assert.Contains("Valor fora dos limites operacionais", exception.Message);
    }

    [Fact]
    public async Task ProcessTelemetryAsync_WithValueMinus500_ShouldThrowBadRequestException()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: -500,
            Timestamp: DateTime.UtcNow
        );

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(
            () => _service.ProcessTelemetryAsync(deviceId, request));
    }

    [Fact]
    public async Task ProcessTelemetryAsync_WithValue300_ShouldThrowBadRequestException()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 300,
            Timestamp: DateTime.UtcNow
        );

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(
            () => _service.ProcessTelemetryAsync(deviceId, request));
    }

    #endregion

    #region SNS Exception Tests

    [Fact]
    public async Task ProcessTelemetryAsync_WhenSnsThrowsException_ShouldThrowUnexpectedException()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 25.0,
            Timestamp: DateTime.UtcNow
        );

        _mockSnsClient
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("SNS service unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<UnexpectedException>(
            () => _service.ProcessTelemetryAsync(deviceId, request));
    }

    [Fact]
    public async Task ProcessTelemetryAsync_WhenSnsThrowsAmazonException_ShouldThrowUnexpectedException()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 25.0,
            Timestamp: DateTime.UtcNow
        );

        _mockSnsClient
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("AWS credentials invalid"));

        // Act & Assert
        await Assert.ThrowsAsync<UnexpectedException>(
            () => _service.ProcessTelemetryAsync(deviceId, request));
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task ProcessTelemetryAsync_ShouldUseConfiguredTopicArn()
    {
        // Arrange
        var topicArn = "arn:aws:sns:us-west-2:999999999:custom-topic";
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(x => x["SnsTopics:TelemetryTopicArn"]).Returns(topicArn);
        
        var service = new TelemetryService(_mockSnsClient.Object, mockConfig.Object);
        
        var deviceId = Guid.NewGuid();
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: "Temperature",
            Value: 25.0,
            Timestamp: DateTime.UtcNow
        );

        PublishRequest capturedRequest = null;
        _mockSnsClient
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new PublishResponse { MessageId = "test-message-id" });

        // Act
        await service.ProcessTelemetryAsync(deviceId, request);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(topicArn, capturedRequest.TopicArn);
    }

    #endregion

    #region Message Content Tests

    [Theory]
    [InlineData("Soil Moisture")]
    [InlineData("PH")]
    [InlineData("Nitrogen")]
    public async Task ProcessTelemetryAsync_WithVariousSensorTypes_ShouldIncludeSensorType(string sensorType)
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var request = new TelemetryRequest(
            FieldId: Guid.NewGuid(),
            SensorType: sensorType,
            Value: 50.0,
            Timestamp: DateTime.UtcNow
        );

        PublishRequest capturedRequest = null;
        _mockSnsClient
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new PublishResponse { MessageId = "test-message-id" });

        // Act
        await _service.ProcessTelemetryAsync(deviceId, request);

        // Assert
        Assert.NotNull(capturedRequest);
        var messageBody = JsonSerializer.Deserialize<JsonElement>(capturedRequest.Message);
        Assert.Equal(sensorType, messageBody.GetProperty("SensorType").GetString());
    }

    [Fact]
    public async Task ProcessTelemetryAsync_WithValidData_ShouldIncludeAllRequiredFieldsInMessage()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var request = new TelemetryRequest(
            FieldId: fieldId,
            SensorType: "Temperature",
            Value: 25.5,
            Timestamp: timestamp
        );

        PublishRequest capturedRequest = null;
        _mockSnsClient
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new PublishResponse { MessageId = "test-message-id" });

        // Act
        await _service.ProcessTelemetryAsync(deviceId, request);

        // Assert
        Assert.NotNull(capturedRequest);
        var messageBody = JsonSerializer.Deserialize<JsonElement>(capturedRequest.Message);
        
        Assert.True(messageBody.TryGetProperty("FieldId", out _));
        Assert.True(messageBody.TryGetProperty("SensorType", out _));
        Assert.True(messageBody.TryGetProperty("Value", out _));
        Assert.True(messageBody.TryGetProperty("Timestamp", out _));
        Assert.True(messageBody.TryGetProperty("ProcessingId", out _));
        Assert.True(messageBody.TryGetProperty("SensorDeviceId", out _));
    }

    #endregion
}

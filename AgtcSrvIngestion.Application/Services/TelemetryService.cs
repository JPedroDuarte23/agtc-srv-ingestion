using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AgtcSrvIngestion.Application.Dtos;
using AgtcSrvIngestion.Application.Exceptions;
using AgtcSrvIngestion.Application.Interfaces;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Configuration;

namespace AgtcSrvIngestion.Application.Services;

public class TelemetryService : ITelemetryService
{

    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly IConfiguration _configuration;

    public TelemetryService(IAmazonSimpleNotificationService snsClient, IConfiguration configuration)
    {
        _snsClient = snsClient;
        _configuration = configuration;
    }

    public async Task ProcessTelemetryAsync(Guid deviceId, string farmerName, string fieldName, string propertyName, TelemetryRequest request)
    {
        if (request.Value < -100 || request.Value > 10000)
            throw new BadRequestException("Valor fora dos limites operacionais.");

        var messageBody = JsonSerializer.Serialize(new
        {
            request.FieldId,
            fieldName,
            propertyName,
            farmerName,
            request.SensorType,
            request.Value,
            request.Timestamp,
            ProcessingId = Guid.NewGuid(),
            SensorDeviceId = deviceId
        });

        var topicArn = _configuration["SnsTopics:TelemetryTopicArn"];

        var publishRequest = new PublishRequest
        {
            TopicArn = topicArn,
            Message = messageBody,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                { "SensorType", new MessageAttributeValue { DataType = "String", StringValue = request.SensorType } }
            }
        };

        try
        {
            await _snsClient.PublishAsync(publishRequest);
        }
        catch (Exception ex)
        {
            throw new UnexpectedException(ex);
        }
    }
}

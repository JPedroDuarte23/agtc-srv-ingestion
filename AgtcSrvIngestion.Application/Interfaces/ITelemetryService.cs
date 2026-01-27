using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AgtcSrvIngestion.Application.Dtos;

namespace AgtcSrvIngestion.Application.Interfaces;

public interface ITelemetryService
{
    Task ProcessTelemetryAsync(Guid deviceId, string farmerName, string fieldName, string propertyName, TelemetryRequest request);
}

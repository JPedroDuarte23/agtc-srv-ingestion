using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgtcSrvIngestion.Application.Dtos;

public record TelemetryRequest
(
    Guid FieldId,        
    string SensorType,    
    double Value,         
    DateTime Timestamp
);

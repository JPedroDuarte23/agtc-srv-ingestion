# 📡 Agro.Ingestion.API (IoT Gateway)

Gateway de alta performance para recepção de telemetria. Projetado para suportar alta carga de requisições.

## 📋 Responsabilidades
- Receber dados dos sensores (HTTP POST).
- Validar Token JWT (Role `Device`).
- **Fire-and-forget:** Publicar mensagem no tópico AWS SNS e responder imediatamente.
- Não realiza conexão com banco de dados (Stateless).

## 🛠️ Stack Tecnológica
- .NET 8 Web API
- AWS SDK (Simple Notification Service - SNS)

## ⚙️ Configuração
```json
{
  "AWS": {
    "Region": "us-east-1",
    "SnsTopicArn": "arn:aws:sns:us-east-1:123456789:sensor-events"
  }
}
```